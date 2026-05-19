using System.Linq.Expressions;

namespace KitabeviMediatr.Domain.Specifications;

public sealed class AndSpecification<T> : Specification<T>
// ↑ generic: Kitap, Siparis vb. — her entity için ayrı And yazmak gerekmez
//   bunu yazmasaydık → AndKitapSpecification, AndSiparisSpecification gibi tekrar yazardık
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;
    //                ↑ iki specification — AND ile birleştiriliyor
    //                  bunu yazmasaydık → AndSpecification başka specification'ları kapsayamazdı

    public AndSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> Criteria
        => x => _left.Criteria.Compile()(x) && _right.Criteria.Compile()(x);
    //                         ↑ Compile(): Expression → Func'a çeviriyor, sonra çağırıyor
    //                           Bu yaklaşım in-memory (LINQ to Objects) için çalışır
    //                           EF Core ile doğrudan kullanılırsa SQL'e çevrilemeyebilir
    //                           Production'da LinqKit veya expression stitching kütüphanesi gerekir
    //                           bunu yazmasaydık → iki Expression'ı doğrudan birleştiremezdik
    //                           (C# expression tree'leri "+ operatörü" ile birleştirilemiyor)
}
