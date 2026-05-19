using KitabeviOnion.Domain.Exceptions;

namespace KitabeviOnion.Domain.ValueObjects;

public record Fiyat
// ↑ record: value semantics — iki Fiyat(100) nesnesi otomatik eşit
//   bunu yazmasaydık → class yazardık, == operatörünü elle override etmek zorunda kalırdık
{
    public decimal Deger { get; }
    public string ParaBirimi { get; }

    public Fiyat(decimal deger, string paraBirimi = "TRY")
    {
        if (deger <= 0)
            throw new DomainException("Fiyat 0'dan büyük olmalı");
        //  ↑ kural Domain'de — Controller veya Service yazmak zorunda değil
        //    bunu yazmasaydık → negatif fiyatlı kitap DB'ye gidebilirdi

        Deger = deger;
        ParaBirimi = paraBirimi;
    }

    public Fiyat KdvEkle(decimal oran = 0.18m)
        => new(Deger * (1 + oran), ParaBirimi);
    //  ↑ yeni Fiyat döner — mevcut Fiyat değişmez (immutable)
    //    bunu yazmasaydık → Fiyat.Deger * 1.18 her serviste tekrar yazılırdı,
    //    oran değişince her yeri bulmak zorunda kalırdık
}
