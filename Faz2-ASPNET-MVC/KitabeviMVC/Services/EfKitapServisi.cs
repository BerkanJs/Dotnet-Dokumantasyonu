using KitabeviMVC.Data;
using KitabeviMVC.Models.Entities;
using KitabeviMVC.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Services;

// Gün 29: EF Core tabanlı servis implementasyonu.
//
// Bu sınıf IKitapServisi'ni implement eder ve tüm veri erişimini
// KitabeviDbContext üzerinden yapar. Gün 18'deki in-memory KitapServisi'nin
// "production-ready" EF Core versiyonudur.
//
// DI kaydı: Scoped → her HTTP request'te taze DbContext alır.
// DbContext Scoped olduğu için bu servis de Scoped OLMAK ZORUNDA.
// (Singleton yapsan "Cannot consume scoped service" hatası alırsın.)
public class EfKitapServisi : IKitapServisi
{
    private readonly KitabeviDbContext _context;
    private readonly ILogger<EfKitapServisi> _logger;

    public EfKitapServisi(KitabeviDbContext context, ILogger<EfKitapServisi> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────
    // HepsiniGetirAsync — AsNoTracking + Projection
    //
    // AsNoTracking(): dönen nesneler Change Tracker'a eklenmez.
    //   Neden? Sadece okuyoruz, güncelleme yok.
    //   Faydası: %10-30 daha hızlı, daha az bellek.
    //
    // Select(k => new KitapListeViewModel(...)): Projection.
    //   EF Core bunu SQL'e çevirir → sadece gerekli kolonları seçer.
    //   SELECT Id, Baslik, Yazar, Fiyat, Kategori, StokAdedi FROM Kitaplar
    //   (tüm kolonları değil — bant genişliği kazancı)
    // ─────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<KitapListeViewModel>> HepsiniGetirAsync()
    {
        return await _context.Kitaplar
            .AsNoTracking()
            .Where(k => k.StokAdedi > 0) // stokta olmayan kitapları listede gösterme
            .OrderBy(k => k.Baslik)
            .Select(k => new KitapListeViewModel(
                k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────
    // KategoriyeGoreGetirAsync — parametreli filtre
    //
    // WHERE filtresi server-side (SQL'de) çalışır:
    //   SELECT ... FROM Kitaplar WHERE Kategori = @kategori
    //
    // Dikkat: EF Core string karşılaştırmasını DB collation'a göre yapar.
    // Büyük/küçük harf duyarlılığı DB ayarına bağlı; case-insensitive
    // istiyorsan EF.Functions.Like veya ToLower() kullan.
    // ─────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<KitapListeViewModel>> KategoriyeGoreGetirAsync(string kategori)
    {
        return await _context.Kitaplar
            .AsNoTracking()
            .Where(k => k.Kategori == kategori)
            .OrderBy(k => k.Baslik)
            .Select(k => new KitapListeViewModel(
                k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────
    // BulByIdAsync — tracking açık (güncelleme senaryosunu destekler)
    //
    // FindAsync: PRIMARY KEY araması — EF Core önce Change Tracker'a bakar,
    // yoksa DB'ye gider. SingleOrDefaultAsync'ten daha verimli PK sorgularında.
    //
    // Tracking AÇIK bırakıldı: Detay sayfasından düzenleme sayfasına geçilebilir.
    // Sadece görüntüleyeceksen AsNoTracking eklenebilir — ama bunu bilmiyoruz.
    // ─────────────────────────────────────────────────────────────────────
    public async Task<KitapFormViewModel?> BulByIdAsync(int id)
    {
        // FindAsync → PK araması, önce 1. seviye cache (Change Tracker)
        var kitap = await _context.Kitaplar.FindAsync(id);
        if (kitap is null) return null;

        return new KitapFormViewModel
        {
            Id        = kitap.Id,
            Baslik    = kitap.Baslik,
            Yazar     = kitap.Yazar,
            Fiyat     = kitap.Fiyat,
            Kategori  = kitap.Kategori,
            StokAdedi = kitap.StokAdedi
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // EkleAsync — Change Tracker: Added → SaveChanges → INSERT
    //
    // context.Kitaplar.Add(kitap):
    //   Entity State: Added
    //   SaveChangesAsync(): INSERT INTO Kitaplar (...) VALUES (...)
    //   SaveChanges sonrası: DB'nin atadığı Id, kitap.Id'ye otomatik yansır.
    // ─────────────────────────────────────────────────────────────────────
    public async Task<int> EkleAsync(KitapFormViewModel model)
    {
        var kitap = new Kitap
        {
            Baslik    = model.Baslik,
            Yazar     = model.Yazar,
            Fiyat     = model.Fiyat,
            Kategori  = model.Kategori,
            StokAdedi = model.StokAdedi,
            EklemeTarihi = DateTime.UtcNow
        };

        _context.Kitaplar.Add(kitap);        // State: Added
        await _context.SaveChangesAsync();    // INSERT — kitap.Id artık dolu

        _logger.LogInformation("EF: Kitap eklendi. Id={Id}, Baslik={Baslik}",
            kitap.Id, kitap.Baslik);

        return kitap.Id;
    }

    // ─────────────────────────────────────────────────────────────────────
    // GuncelleAsync — Change Tracker: Unchanged → Modified → UPDATE
    //
    // Yaklaşım: entity'yi DB'den çek (Unchanged), property'leri değiştir
    // (Modified), SaveChanges() yap (UPDATE).
    //
    // Alternatif: context.Update(detachedEntity) → tüm kolonları günceller.
    // Bu yaklaşım daha verimli: sadece değişen kolonlar UPDATE edilir.
    // ─────────────────────────────────────────────────────────────────────
    public async Task<bool> GuncelleAsync(KitapFormViewModel model)
    {
        var kitap = await _context.Kitaplar.FindAsync(model.Id);
        if (kitap is null) return false;

        // State: Unchanged → bu satırdan sonra EF Core değişiklikleri izler
        kitap.Baslik    = model.Baslik;
        kitap.Yazar     = model.Yazar;
        kitap.Fiyat     = model.Fiyat;
        kitap.Kategori  = model.Kategori;
        kitap.StokAdedi = model.StokAdedi;
        // State: Modified (EF Core farkı tespit etti)

        await _context.SaveChangesAsync();
        // UPDATE Kitaplar SET Baslik=@p0, Yazar=@p1, ... WHERE Id=@p5

        _logger.LogInformation("EF: Kitap güncellendi. Id={Id}", model.Id);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // SilAsync — Change Tracker: Unchanged → Deleted → DELETE
    // ─────────────────────────────────────────────────────────────────────
    public async Task<bool> SilAsync(int id)
    {
        var kitap = await _context.Kitaplar.FindAsync(id);
        if (kitap is null) return false;

        _context.Kitaplar.Remove(kitap); // State: Deleted
        await _context.SaveChangesAsync(); // DELETE FROM Kitaplar WHERE Id=@id

        _logger.LogInformation("EF: Kitap silindi. Id={Id}", id);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // BaslikVarMiAsync — veri bütünlüğü kontrolü, her zaman DB'ye gider.
    //
    // AnyAsync: SQL'de EXISTS kullanır — tüm satırları çekmez, çok verimli.
    //   SELECT CASE WHEN EXISTS (SELECT 1 FROM Kitaplar WHERE ...) THEN 1 ELSE 0
    //
    // haricId: güncelleme senaryosunda kendi ID'si hariç tutulur.
    // ─────────────────────────────────────────────────────────────────────
    public async Task<bool> BaslikVarMiAsync(string baslik, int haricId = 0)
    {
        return await _context.Kitaplar
            .AnyAsync(k => k.Baslik == baslik && k.Id != haricId);
    }
}
