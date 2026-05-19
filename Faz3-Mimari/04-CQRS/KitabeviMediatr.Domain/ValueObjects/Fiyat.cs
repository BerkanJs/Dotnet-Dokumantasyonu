using KitabeviMediatr.Domain.Exceptions;

namespace KitabeviMediatr.Domain.ValueObjects;

public record Fiyat
{
    public decimal Deger { get; }
    public string ParaBirimi { get; }

    public Fiyat(decimal deger, string paraBirimi = "TRY")
    {
        if (deger <= 0)
            throw new DomainException("Fiyat 0'dan büyük olmalı");

        Deger = deger;
        ParaBirimi = paraBirimi;
    }

    public Fiyat KdvEkle(decimal oran = 0.18m)
        => new(Deger * (1 + oran), ParaBirimi);
}
