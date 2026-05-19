using KitabeviMediatr.Domain.Entities;
using KitabeviMediatr.Domain.Specifications;
using KitabeviMediatr.Domain.ValueObjects;

namespace KitabeviMediatr.Domain.Interfaces;

public interface IKitapRepository
{
    Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct = default);
    Task EkleAsync(Kitap kitap, CancellationToken ct = default);
    Task<bool> IsbnMevcutMu(Isbn isbn, CancellationToken ct = default);
    //          ↑ duplikat ISBN kontrolü için — Domain interface'i, DB bilgisi yok
    Task KaydetAsync(CancellationToken ct = default);
    //   ↑ Gün59'da sadece SiparisRepository'de vardı — Kitap için de gerekli
    //     bunu yazmasaydık → EkleAsync sonrası SaveChanges nereye yazılacak?

    Task<IReadOnlyList<Kitap>> ListAsync(ISpecification<Kitap> spec, CancellationToken ct = default);
    //                          ↑ Gün67: Specification Pattern — filtre detayını repository dışında tut
    //                            spec nesnesi kim → AktifKitaplar, FiyatAraligi, And(aktif, fiyat)...
    //                            bunu yazmasaydık → her filtre kombinasyonu için yeni repository metodu yazardık
    //                            10 farklı filtre = 10 farklı metot vs. 10 farklı specification
}
