namespace KitabeviOnion.Domain.Exceptions;

public class DomainException : Exception
//           ↑ Exception'dan kalıtım — catch bloklarında ayrı yakalanabilmesi için
//             bunu yazmasaydık → ArgumentException veya InvalidOperationException
//             fırlatırdık, Domain'den gelen hatayı Infrastructure hatasından ayırt edemezdik
{
    public DomainException(string mesaj) : base(mesaj) { }
    //                                    ↑ mesajı base Exception'a ilet
    //                                      bunu yazmasaydık → mesaj kaybedilirdi
}
