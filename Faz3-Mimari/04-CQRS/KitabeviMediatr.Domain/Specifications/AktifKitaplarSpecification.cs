using System.Linq.Expressions;
using KitabeviMediatr.Domain.Entities;

namespace KitabeviMediatr.Domain.Specifications;

public sealed class AktifKitaplarSpecification : Specification<Kitap>
// ↑ sealed: kalıtım kapatıldı — "aktif kitaplar" kuralı değişmez, genişletilmesi mantıksız
//   bunu yazmasaydık → başkası miras alıp Criteria'yı override ederek spesifikasyonu bozabilirdi
{
    public override Expression<Func<Kitap, bool>> Criteria
        => kitap => kitap.Aktif;
    //              ↑ EF Core bu expression'ı SQL'e çevirir: WHERE Aktif = 1
    //                bunu yazmasaydık → her repository metodu kendi .Where(k => k.Aktif) yazardı,
    //                "aktif" tanımı değişince kaç dosyayı güncellermen lazım?
}
