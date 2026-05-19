namespace KitabeviMediatr.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string mesaj) : base(mesaj) { }
}
