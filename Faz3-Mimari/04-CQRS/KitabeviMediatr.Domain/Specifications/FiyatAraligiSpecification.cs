using System.Linq.Expressions;
using KitabeviMediatr.Domain.Entities;

namespace KitabeviMediatr.Domain.Specifications;

public sealed class FiyatAraligiSpecification : Specification<Kitap>
// ↑ parametre alan specification — constructor'da değerler alınır, Criteria'da kullanılır
//   bunu yazmasaydık → min/max'ı lambda içine hard-code etmek zorunda kalırdık, yeniden kullanılamaz
{
    private readonly decimal _min;
    private readonly decimal _max;
    //                ↑ private: dışarıdan min/max değiştirilemez
    //                  bunu yazmasaydık → specification oluştuktan sonra değiştirilebilir, tutarsızlık riski

    public FiyatAraligiSpecification(decimal min, decimal max)
    {
        if (min < 0)
            throw new ArgumentException("Minimum fiyat negatif olamaz", nameof(min));
        //  ↑ constructor'da guard — geçersiz specification hiç oluşmasın
        //    bunu yazmasaydık → negatif min ile repository sorgusu gider, beklenmedik sonuç döner

        if (max < min)
            throw new ArgumentException("Maksimum fiyat minimumdan küçük olamaz", nameof(max));
        //  ↑ min > max tutarsız aralık — sıfır sonuç dönmesi yerine hata daha açık
        //    bunu yazmasaydık → boş liste dönüp neden boş olduğu anlaşılmaz

        _min = min;
        _max = max;
    }

    public override Expression<Func<Kitap, bool>> Criteria
        => kitap => kitap.Fiyat.Deger >= _min && kitap.Fiyat.Deger <= _max;
    //              ↑ kitap.Fiyat.Deger: Fiyat bir Value Object — EF Core Owned Entity
    //                SQL: WHERE Fiyat >= @min AND Fiyat <= @max
    //                bunu yazmasaydık (kitap.Fiyat >= _min) → operator overload yoksa derleme hatası
}
