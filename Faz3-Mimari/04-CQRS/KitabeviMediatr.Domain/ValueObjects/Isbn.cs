using KitabeviMediatr.Domain.Exceptions;

namespace KitabeviMediatr.Domain.ValueObjects;

public record Isbn
{
    public string Deger { get; }

    public Isbn(string deger)
    {
        var temiz = deger.Replace("-", "").Replace(" ", "");

        if (temiz.Length != 13 || !temiz.All(char.IsDigit))
            throw new DomainException($"Geçersiz ISBN: {deger}");

        Deger = temiz;
    }

    public override string ToString() => Deger;
}
