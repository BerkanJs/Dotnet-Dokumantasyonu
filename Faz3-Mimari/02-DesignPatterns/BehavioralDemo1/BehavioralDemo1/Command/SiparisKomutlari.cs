namespace BehavioralDemo1.Command;

// Command Pattern: isteği nesne olarak temsil et
// Undo/redo, queue, log — hepsi mümkün çünkü komut nesne
// CQRS'in temel fikri buradan geliyor (Faz3 Gün 35+)

public interface IKomut
{
    void Calistir();
    void GeriAl();   // undo desteği
}

// Her komut kendi verisi + mantığını taşıyor
public class KitapSiparisKomutu : IKomut
{
    private readonly string _baslik;
    private readonly SiparisDepo _depo;
    private int _siparisId;

    public KitapSiparisKomutu(string baslik, SiparisDepo depo)
    {
        _baslik = baslik;
        _depo   = depo;
    }

    public void Calistir()
    {
        _siparisId = _depo.SiparisKaydet(_baslik);
        // komutu çalıştır — sonucu sakla (undo için)
    }

    public void GeriAl()
    {
        _depo.SiparisIptal(_siparisId);
        // bunu yazmasaydık → undo mümkün olmazdı
    }
}

// Invoker: komutları sıraya alıp çalıştırır, undo stack tutar
public class SiparisIslemcisi
{
    private readonly Stack<IKomut> _gecmis = new();
    // bunu yazmasaydık → undo için hangi komutun çalıştığını bilemezdik

    public void Calistir(IKomut komut)
    {
        komut.Calistir();
        _gecmis.Push(komut);  // geçmişe ekle
    }

    public void SonGeriAl()
    {
        if (_gecmis.TryPop(out var komut))
            komut.GeriAl();
        else
            Console.WriteLine("[UNDO] Geri alınacak işlem yok");
    }
}

// Receiver: asıl işi yapan nesne
public class SiparisDepo
{
    private int _sayac = 0;

    public int SiparisKaydet(string baslik)
    {
        _sayac++;
        Console.WriteLine($"[SİPARİŞ] #{_sayac} oluşturuldu: '{baslik}'");
        return _sayac;
    }

    public void SiparisIptal(int id)
        => Console.WriteLine($"[İPTAL] #{id} iptal edildi");
}
