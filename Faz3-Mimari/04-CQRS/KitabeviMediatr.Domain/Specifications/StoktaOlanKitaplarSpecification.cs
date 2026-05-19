using System.Linq.Expressions;
using KitabeviMediatr.Domain.Entities;

namespace KitabeviMediatr.Domain.Specifications;

public sealed class StoktaOlanKitaplarSpecification : Specification<Kitap>
{
    public override Expression<Func<Kitap, bool>> Criteria
        => kitap => kitap.StokAdedi > 0;
    //              ↑ "stokta var" tanımı tek yer — StokAdedi > 0
    //                eşik değeri değişirse (>= 5 gibi) sadece burası güncellenir
    //                bunu yazmasaydık → handler'lar kendi if'lerini yazardı, eşik tutarsızlaşırdı
}
