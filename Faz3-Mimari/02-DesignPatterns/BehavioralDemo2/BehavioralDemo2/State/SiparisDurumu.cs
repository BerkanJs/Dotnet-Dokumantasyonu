namespace BehavioralDemo2.State;

// State Pattern: nesnenin davranışı durumuna göre değişir
// if/switch zinciri yerine her durum kendi class'ında

public interface ISiparisDurumu
{
    void OdemeAl(Siparis siparis);
    void Gonder(Siparis siparis);
    void Teslim(Siparis siparis);
    string Ad { get; }
}

public class Siparis
{
    public ISiparisDurumu Durum { get; set; } = new BeklemeDurumu();
    // bunu yazmasaydık → her metotta if (durum == "bekleme") else if (...) zinciri olurdu
    // yeni durum eklenince tüm if blokları güncellenmek zorunda kalırdı

    public void OdemeAl() => Durum.OdemeAl(this);
    public void Gonder()   => Durum.Gonder(this);
    public void Teslim()   => Durum.Teslim(this);

    public override string ToString() => $"Sipariş durumu: {Durum.Ad}";
}

public class BeklemeDurumu : ISiparisDurumu
{
    public string Ad => "Ödeme Bekleniyor";

    public void OdemeAl(Siparis siparis)
    {
        Console.WriteLine("[ÖDEME] Alındı → Hazırlanıyor");
        siparis.Durum = new HazirlaniyorDurumu();
        // durum geçişi burada — Siparis class'ına if eklemiyoruz
    }

    public void Gonder(Siparis siparis)  => Console.WriteLine("[HATA] Önce ödeme alınmalı");
    public void Teslim(Siparis siparis)  => Console.WriteLine("[HATA] Önce ödeme alınmalı");
}

public class HazirlaniyorDurumu : ISiparisDurumu
{
    public string Ad => "Hazırlanıyor";

    public void OdemeAl(Siparis siparis) => Console.WriteLine("[HATA] Ödeme zaten alındı");

    public void Gonder(Siparis siparis)
    {
        Console.WriteLine("[KARGO] Gönderildi → Yolda");
        siparis.Durum = new YoldaDurumu();
    }

    public void Teslim(Siparis siparis) => Console.WriteLine("[HATA] Önce gönderilmeli");
}

public class YoldaDurumu : ISiparisDurumu
{
    public string Ad => "Yolda";

    public void OdemeAl(Siparis siparis) => Console.WriteLine("[HATA] Ödeme zaten alındı");
    public void Gonder(Siparis siparis)  => Console.WriteLine("[HATA] Zaten gönderildi");

    public void Teslim(Siparis siparis)
    {
        Console.WriteLine("[TESLİM] Tamamlandı");
        siparis.Durum = new TeslimEdildiDurumu();
    }
}

public class TeslimEdildiDurumu : ISiparisDurumu
{
    public string Ad => "Teslim Edildi";
    public void OdemeAl(Siparis siparis) => Console.WriteLine("[HATA] Sipariş tamamlandı");
    public void Gonder(Siparis siparis)  => Console.WriteLine("[HATA] Sipariş tamamlandı");
    public void Teslim(Siparis siparis)  => Console.WriteLine("[HATA] Zaten teslim edildi");
}
