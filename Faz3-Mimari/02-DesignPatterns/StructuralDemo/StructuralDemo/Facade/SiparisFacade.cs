namespace StructuralDemo.Facade;

// Alt sistemler: her biri kendi işini yapıyor
public class StokServisi
{
    public bool StokDus(string isbn)
    {
        Console.WriteLine($"[STOK] {isbn} stoğu düşüldü");
        return true;
    }
}

public class OdemeServisi
{
    public bool OdemeAl(decimal tutar)
    {
        Console.WriteLine($"[ÖDEME] {tutar:C0} tahsil edildi");
        return true;
    }
}

public class KargoServisi
{
    public string KargoOlustur(string adres)
    {
        var takipNo = Guid.NewGuid().ToString()[..8].ToUpper();
        Console.WriteLine($"[KARGO] {adres} → takip: {takipNo}");
        return takipNo;
    }
}

public class BildirimServisi
{
    public void GonderAsync(string email, string mesaj)
        => Console.WriteLine($"[EMAIL] {email} → {mesaj}");
}

// Facade: 4 alt sistemi tek basit interface arkasına gizliyor
// Controller sadece SiparisFacade biliyor — 4 servisi inject etmek zorunda değil
// bunu yazmasaydık → controller'da stok + ödeme + kargo + bildirim koordinasyonu olurdu
// her controller sipariş işleminde aynı koordinasyonu tekrarlardı
public class SiparisFacade
{
    private readonly StokServisi _stok = new();
    private readonly OdemeServisi _odeme = new();
    private readonly KargoServisi _kargo = new();
    private readonly BildirimServisi _bildirim = new();
    // gerçek projede constructor injection kullanılır

    public string SiparisVer(string isbn, decimal fiyat, string adres, string email)
    {
        if (!_stok.StokDus(isbn))
            throw new InvalidOperationException("Stok yok");

        if (!_odeme.OdemeAl(fiyat))
            throw new InvalidOperationException("Ödeme başarısız");

        var takipNo = _kargo.KargoOlustur(adres);

        _bildirim.GonderAsync(email, $"Siparişiniz alındı. Takip: {takipNo}");

        return takipNo;
        // 4 adım, 1 çağrı — çağıran karmaşıklığı görmüyor
    }
}
