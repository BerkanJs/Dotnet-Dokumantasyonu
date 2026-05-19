using KitabeviMediatr.Domain.Entities;
using KitabeviMediatr.Domain.Interfaces;
using KitabeviMediatr.Domain.Specifications;
using KitabeviMediatr.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;

namespace KitabeviMediatr.Infrastructure.Persistence.Repositories;

public class CachingKitapRepository : IKitapRepository
//                                     ↑ aynı interface — Handler için şeffaf
//                                       Handler IKitapRepository görüyor, cache'in varlığından haberi yok
//                                       bunu yazmasaydık → Handler'a cache kodu eklemek zorunda kalırdık
{
    private readonly IKitapRepository _inner;
    //               ↑ asıl repository: CachingRepo başarısız olursa inner'a düşer
    //                 bunu yazmasaydık → DB çağrısını da buraya yazmak zorunda kalırdık,
    //                 decorator değil başka bir implementasyon olurdu

    private readonly IMemoryCache _cache;
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);
    //                                        ↑ 5 dakika cache — değişecekse tek yer
    //                                          bunu yazmasaydık → her metotta ayrı süre yazılırdı

    private const string TumKitaplarKey = "kitaplar:tumü";
    //                                     ↑ cache key sabiti — string literal dağılmasın
    //                                       bunu yazmasaydık → "kitaplar" yazım hatası fark edilmezdi

    public CachingKitapRepository(IKitapRepository inner, IMemoryCache cache)
    //                             ↑ inner inject: KitapRepository (asıl DB katmanı)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(TumKitaplarKey, out IReadOnlyList<Kitap>? cached) && cached is not null)
        {
            return cached;
            // ↑ cache hit: DB'ye gitme
            //   bunu yazmasaydık → her istekte DB sorgusu, 50k'da gereksiz yük
        }

        var kitaplar = await _inner.TumunuGetirAsync(ct);
        //                          ↑ cache miss: asıl DB çağrısı

        _cache.Set(TumKitaplarKey, kitaplar, _ttl);
        //         ↑ 5 dakika sakla
        //           bunu yazmasaydık → bir sonraki istekte tekrar DB'ye gidilirdi

        return kitaplar;
    }

    public async Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default)
    {
        var cacheKey = $"kitap:{id}";
        //              ↑ her kitap için ayrı key — "kitap:42", "kitap:7"

        if (_cache.TryGetValue(cacheKey, out Kitap? cached))
            return cached;

        var kitap = await _inner.BulByIdAsync(id, ct);

        if (kitap is not null)
            _cache.Set(cacheKey, kitap, _ttl);
        //            ↑ sadece bulunanı cache'le — null'ı saklama
        //              bunu yazmasaydık → olmayan Id'ler için de DB'ye gidilirdi

        return kitap;
    }

    public async Task EkleAsync(Kitap kitap, CancellationToken ct = default)
    {
        await _inner.EkleAsync(kitap, ct);
        //            ↑ önce DB'ye ekle — cache burada değişmez, KaydetAsync'te temizlenir
    }

    public async Task KaydetAsync(CancellationToken ct = default)
    {
        await _inner.KaydetAsync(ct);

        _cache.Remove(TumKitaplarKey);
        //             ↑ yeni kitap eklendi → cache stale (bayatladı) → temizle
        //               bunu yazmasaydık → eski liste 5 dakika daha serviste kalırdı,
        //               yeni eklenen kitap listede görünmezdi
    }

    public Task<bool> IsbnMevcutMu(Isbn isbn, CancellationToken ct = default)
        => _inner.IsbnMevcutMu(isbn, ct);
    //   ↑ bu metodu cache'lemiyoruz — duplikat kontrolü her zaman taze veri istiyor
    //     bunu cache'lesek → yeni eklenen ISBN 5 dakika "yok" görünebilirdi

    public Task<IReadOnlyList<Kitap>> ListAsync(ISpecification<Kitap> spec, CancellationToken ct = default)
        => _inner.ListAsync(spec, ct);
    //   ↑ specification sorgularını cache'lemiyoruz — her farklı filtre kombinasyonu farklı key gerektirir
    //     FiyatAraligi(10,50) AND AktifKitaplar için key ne olacak? Kompleks, riski yüksek
    //     bunu cache'lemek isteseydik → ICacheableQuery marker interface (Gün63) + CachingBehavior daha iyi yer
}
