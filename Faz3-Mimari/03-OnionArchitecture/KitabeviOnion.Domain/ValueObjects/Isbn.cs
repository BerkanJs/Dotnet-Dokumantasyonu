using KitabeviOnion.Domain.Exceptions;

namespace KitabeviOnion.Domain.ValueObjects;

public record Isbn
{
    public string Deger { get; }

    public Isbn(string deger)
    {
        var temiz = deger.Replace("-", "").Replace(" ", "");
        //          ↑ kullanıcı "978-0-13-110362-7" yazsa da çalışsın

        if (temiz.Length != 13 || !temiz.All(char.IsDigit))
            throw new DomainException($"Geçersiz ISBN: {deger}");
        //  ↑ format kuralı tek yerde — Controller, Service bilmek zorunda değil
        //    bunu yazmasaydık → "abc" ISBN olarak DB'ye girebilirdi

        Deger = temiz;
    }

    public override string ToString() => Deger;
}
