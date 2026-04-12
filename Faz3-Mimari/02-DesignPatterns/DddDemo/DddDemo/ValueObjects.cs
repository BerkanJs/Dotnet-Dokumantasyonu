namespace DddDemo;

// VALUE OBJECT: kimliği yok, değeri önemli
// İki value object değerleri aynıysa eşittir
// Immutable: oluşturulunca değiştirilemez

public record Fiyat
{
    public decimal Deger { get; }
    public string ParaBirimi { get; }

    public Fiyat(decimal deger, string paraBirimi = "TRY")
    {
        if (deger <= 0)
            throw new ArgumentException("Fiyat 0'dan büyük olmalı");
        // bunu yazmasaydık → negatif fiyatlı kitap oluşturulabilirdi
        // kontrol tek yerde — servis, controller bilmek zorunda değil
        Deger = deger;
        ParaBirimi = paraBirimi;
    }

    public Fiyat KdvEkle(decimal oran = 0.18m) => new(Deger * (1 + oran), ParaBirimi);
    // yeni Fiyat döner — mevcut değişmez (immutable)
    // bunu yazmasaydık → KDV hesabı her yerde tekrarlanırdı

    public override string ToString() => $"{Deger:N2} {ParaBirimi}";
}

public record Isbn
{
    public string Deger { get; }

    public Isbn(string deger)
    {
        var temiz = deger.Replace("-", "").Replace(" ", "");
        if (temiz.Length != 13 || !temiz.All(char.IsDigit))
            throw new ArgumentException($"Geçersiz ISBN: {deger}");
        // format kontrolü tek yerde
        Deger = temiz;
    }

    public override string ToString() => Deger;
}

// record: referans tipi ama value semantics
// iki Fiyat(100, "TRY") → eşit (== true)
// iki Kitap(1) → aynı Id'ye sahip ama farklı nesne olabilir (entity semantics)
