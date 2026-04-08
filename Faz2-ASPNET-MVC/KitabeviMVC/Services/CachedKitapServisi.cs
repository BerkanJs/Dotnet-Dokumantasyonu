using Microsoft.Extensions.Caching.Memory;
using KitabeviMVC.Models.ViewModels;

namespace KitabeviMVC.Services;

// ─────────────────────────────────────────────────────────────────────
// Gün 26: Decorator Pattern ile Cache Katmanı
//
// CachedKitapServisi, IKitapServisi'ni implement eder VE içeride
// başka bir IKitapServisi alır. Bu sayede:
//
//   Controller → CachedKitapServisi → EfKitapServisi (DB)
//
// EfKitapServisi caching'den tamamen habersiz kalır (Tek Sorumluluk).
//
// Gün 29 notu: CachedKitapServisi artık Scoped olarak kayıtlı.
// Neden? İçteki EfKitapServisi Scoped → Singleton'a inject edilemez.
// Scoped cache servis performans açısından ideal değil (her request'te
// taze instance = cache paylaşılmaz), ancak bu projenin boyutunda
// fark etmez. Gerçek production'da Redis (distributed cache) kullanılır.
// ─────────────────────────────────────────────────────────────────────
public class CachedKitapServisi : IKitapServisi
{
    private readonly IKitapServisi _gercekServis;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedKitapServisi> _logger;

    private const string TumKitaplarKey = "kitaplar:hepsi";
    private static string KategoriKey(string kategori) =>
        $"kitaplar:kategori:{kategori.ToLowerInvariant()}";
    private static string KitapKey(int id) => $"kitap:{id}";

    // Gün 26: Thundering Herd koruması
    private readonly SemaphoreSlim _kilit = new(1, 1);

    public CachedKitapServisi(
        IKitapServisi gercekServis,
        IMemoryCache cache,
        ILogger<CachedKitapServisi> logger)
    {
        _gercekServis = gercekServis;
        _cache = cache;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────
    // HepsiniGetirAsync — stampede korumalı, async
    //
    // SemaphoreSlim.WaitAsync(): kilit alırken thread'i bloklamaz.
    // .Wait() senkron versiyonu thread'i bloklar — async kodda kaçın.
    // ─────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<KitapListeViewModel>> HepsiniGetirAsync()
    {
        if (_cache.TryGetValue(TumKitaplarKey, out IReadOnlyList<KitapListeViewModel>? cached))
        {
            _logger.LogDebug("Cache HIT → {Key}", TumKitaplarKey);
            return cached!;
        }

        await _kilit.WaitAsync(); // async kilit — thread bloklanmaz
        try
        {
            if (_cache.TryGetValue(TumKitaplarKey, out cached))
                return cached!;

            _logger.LogDebug("Cache MISS → {Key}", TumKitaplarKey);
            var kitaplar = await _gercekServis.HepsiniGetirAsync();

            _cache.Set(TumKitaplarKey, kitaplar, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                .SetSlidingExpiration(TimeSpan.FromMinutes(2))
                .SetSize(1));

            return kitaplar;
        }
        finally
        {
            _kilit.Release();
        }
    }

    public async Task<IReadOnlyList<KitapListeViewModel>> KategoriyeGoreGetirAsync(string kategori)
    {
        var key = KategoriKey(kategori);

        if (_cache.TryGetValue(key, out IReadOnlyList<KitapListeViewModel>? cached))
            return cached!;

        var liste = await _gercekServis.KategoriyeGoreGetirAsync(kategori);

        _cache.Set(key, liste, new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
            .SetSlidingExpiration(TimeSpan.FromMinutes(2))
            .SetSize(1));

        return liste;
    }

    public async Task<KitapFormViewModel?> BulByIdAsync(int id)
    {
        var key = KitapKey(id);

        if (_cache.TryGetValue(key, out KitapFormViewModel? cached))
        {
            _logger.LogDebug("Cache HIT → {Key}", key);
            return cached;
        }

        var kitap = await _gercekServis.BulByIdAsync(id);
        var options = new MemoryCacheEntryOptions().SetSize(1);

        if (kitap is not null)
            options.SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                   .SetSlidingExpiration(TimeSpan.FromMinutes(5));
        else
            options.SetAbsoluteExpiration(TimeSpan.FromSeconds(30));

        _cache.Set(key, kitap, options);
        return kitap;
    }

    public async Task<int> EkleAsync(KitapFormViewModel model)
    {
        var yeniId = await _gercekServis.EkleAsync(model);
        _cache.Remove(TumKitaplarKey);
        _cache.Remove(KategoriKey(model.Kategori));
        return yeniId;
    }

    public async Task<bool> GuncelleAsync(KitapFormViewModel model)
    {
        var basarili = await _gercekServis.GuncelleAsync(model);
        if (basarili)
        {
            _cache.Remove(KitapKey(model.Id));
            _cache.Remove(TumKitaplarKey);
            _cache.Remove(KategoriKey(model.Kategori));
        }
        return basarili;
    }

    public async Task<bool> SilAsync(int id)
    {
        var kitap = await _gercekServis.BulByIdAsync(id);
        var basarili = await _gercekServis.SilAsync(id);
        if (basarili)
        {
            _cache.Remove(KitapKey(id));
            _cache.Remove(TumKitaplarKey);
            if (kitap is not null)
                _cache.Remove(KategoriKey(kitap.Kategori));
        }
        return basarili;
    }

    // Cache'lenmez — veri bütünlüğü kontrolü anlık tutarlılık gerektirir.
    public Task<bool> BaslikVarMiAsync(string baslik, int haricId = 0) =>
        _gercekServis.BaslikVarMiAsync(baslik, haricId);
}
