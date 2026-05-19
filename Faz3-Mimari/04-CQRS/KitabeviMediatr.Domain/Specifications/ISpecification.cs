using System.Linq.Expressions;

namespace KitabeviMediatr.Domain.Specifications;

public interface ISpecification<T>
// ↑ generic interface: Specification<Kitap>, Specification<Siparis> gibi farklı entity'ler için kullanılabilir
//   bunu yazmasaydık → abstract class yazardık, C# tek kalıtım kısıtı bazı tasarımları kısıtlardı
{
    Expression<Func<T, bool>> Criteria { get; }
    //          ↑ Lambda değil Expression — EF Core bunu SQL'e çevirebilir
    //            bunu yazmasaydık (Func<T,bool> kullansaydık) → EF Core SQL üretmez,
    //            tüm tabloyu belleğe çeker, sonra filtreler — 50k satırda felaket
}
