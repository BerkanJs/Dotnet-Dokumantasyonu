using KitabeviOnion.Domain.Exceptions;
using KitabeviOnion.Domain.ValueObjects;

namespace KitabeviOnion.Domain.Entities;

public enum SiparisDurumu { Beklemede, Onaylandi, Iptal }
//           ↑ string değil enum — geçersiz durum derlemede yakalanır
//             bunu yazmasaydık → siparis.Durum = "ONAYLNDI" typo fark edilmezdi

public class Siparis
{
    public int Id { get; private set; }
    public string KullaniciId { get; private set; } = null!;
    public SiparisDurumu Durum { get; private set; }

    private readonly List<SiparisKalemi> _kalemler = [];
    public IReadOnlyList<SiparisKalemi> Kalemler => _kalemler;
    //     ↑ IReadOnlyList: dışarıdan .Add() / .Remove() çağrılamaz
    //       bunu yazmasaydık → herkes siparis.Kalemler.Add() yapabilirdi, kural devre dışı

    private readonly List<string> _domainEvents = [];
    public IReadOnlyList<string> DomainEvents => _domainEvents;
    //                            ↑ basit string event — gerçek projede INotification

    protected Siparis() { }

    public Siparis(string kullaniciId)
    {
        if (string.IsNullOrWhiteSpace(kullaniciId))
            throw new DomainException("Kullanıcı Id boş olamaz");

        KullaniciId = kullaniciId;
        Durum = SiparisDurumu.Beklemede;
        //      ↑ yeni sipariş her zaman Beklemede başlar — bu kural burada
    }

    public void KalemEkle(int kitapId, string kitapBaslik, Fiyat birimFiyat, int adet)
    {
        if (Durum != SiparisDurumu.Beklemede)
            throw new DomainException("Onaylanmış veya iptal siparişe kalem eklenemez");
        //  ↑ durum geçişi kuralı — Application veya Controller yazmak zorunda değil
        //    bunu yazmasaydık → onaylanmış siparişe kalem eklenebilirdi

        if (adet <= 0)
            throw new DomainException("Adet sıfırdan büyük olmalı");

        _kalemler.Add(new SiparisKalemi(kitapId, kitapBaslik, birimFiyat, adet));
    }

    public void Onayla()
    {
        if (!_kalemler.Any())
            throw new DomainException("Boş sipariş onaylanamaz");
        //  ↑ invariant: içeriksiz sipariş onaylanamaz
        //    bunu yazmasaydık → 0 TL sipariş ödeme servisine gidebilirdi

        if (Durum != SiparisDurumu.Beklemede)
            throw new DomainException("Sadece beklemedeki sipariş onaylanabilir");

        Durum = SiparisDurumu.Onaylandi;

        _domainEvents.Add($"SiparisOnaylandi:{Id}:{KullaniciId}");
        //             ↑ "ne oldu" haberi — kim dinlerse o tepki verir (email, bildirim)
        //               bunu yazmasaydık → email göndermeyi burada yapmak zorunda kalırdık,
        //               Siparis email servisine bağımlı hale gelirdi (SRP ihlali)
    }

    public decimal ToplamTutar()
        => _kalemler.Sum(k => k.BirimFiyat.Deger * k.Adet);
    //  ↑ hesap root'ta — her zaman güncel, dışarıdan değiştirilemez
}

public class SiparisKalemi
{
    public int Id { get; private set; }
    public int KitapId { get; private set; }
    public string KitapBaslik { get; private set; } = null!;
    public Fiyat BirimFiyat { get; private set; } = null!;
    public int Adet { get; private set; }

    protected SiparisKalemi() { }

    internal SiparisKalemi(int kitapId, string kitapBaslik, Fiyat birimFiyat, int adet)
    //       ↑ internal: sadece Siparis.KalemEkle çağırabilir
    //         bunu yazmasaydık → dışarıdan new SiparisKalemi() ile Siparis bypass edilirdi
    {
        KitapId = kitapId;
        KitapBaslik = kitapBaslik;
        BirimFiyat = birimFiyat;
        Adet = adet;
    }

    public decimal SatirToplami() => BirimFiyat.Deger * Adet;
}
