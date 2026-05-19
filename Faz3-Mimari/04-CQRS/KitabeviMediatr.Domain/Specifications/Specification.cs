using System.Linq.Expressions;

namespace KitabeviMediatr.Domain.Specifications;

public abstract class Specification<T> : ISpecification<T>
// ↑ abstract class: ISpecification'ı uygular ama Criteria'yı somut class'lara bırakır
//   bunu yazmasaydık → her specification ISpecification'ı doğrudan uygulardı,
//   ilerleyen günlerde buraya Include/OrderBy/Pagination eklenecek ortak yer olmaz
{
    public abstract Expression<Func<T, bool>> Criteria { get; }
    //              ↑ abstract: alt class override etmek zorunda
    //                bunu yazmasaydık → Specification<T> somutlaştırılabilir, anlamsız boş instance üretilebilir
}
