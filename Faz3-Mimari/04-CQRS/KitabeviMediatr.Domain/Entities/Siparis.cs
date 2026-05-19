using KitabeviMediatr.Domain.Exceptions;
using KitabeviMediatr.Domain.ValueObjects;

namespace KitabeviMediatr.Domain.Entities;

public enum SiparisDurumu { Beklemede, Onaylandi, Iptal }

public class Siparis
{
    public int Id { get; private set; }
    public string KullaniciId { get; private set; } = null!;
    public SiparisDurumu Durum { get; private set; }

    private readonly List<SiparisKalemi> _kalemler = [];
    public IReadOnlyList<SiparisKalemi> Kalemler => _kalemler;

    private readonly List<string> _domainEvents = [];
    public IReadOnlyList<string> DomainEvents => _domainEvents;

    protected Siparis() { }

    public Siparis(string kullaniciId)
    {
        if (string.IsNullOrWhiteSpace(kullaniciId))
            throw new DomainException("Kullanıcı Id boş olamaz");

        KullaniciId = kullaniciId;
        Durum = SiparisDurumu.Beklemede;
    }

    public void KalemEkle(int kitapId, string kitapBaslik, Fiyat birimFiyat, int adet)
    {
        if (Durum != SiparisDurumu.Beklemede)
            throw new DomainException("Onaylanmış veya iptal siparişe kalem eklenemez");
        if (adet <= 0)
            throw new DomainException("Adet sıfırdan büyük olmalı");

        _kalemler.Add(new SiparisKalemi(kitapId, kitapBaslik, birimFiyat, adet));
    }

    public void Onayla()
    {
        if (!_kalemler.Any())
            throw new DomainException("Boş sipariş onaylanamaz");
        if (Durum != SiparisDurumu.Beklemede)
            throw new DomainException("Sadece beklemedeki sipariş onaylanabilir");

        Durum = SiparisDurumu.Onaylandi;
        _domainEvents.Add($"SiparisOnaylandi:{Id}:{KullaniciId}");
    }

    public decimal ToplamTutar()
        => _kalemler.Sum(k => k.BirimFiyat.Deger * k.Adet);
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
    {
        KitapId = kitapId;
        KitapBaslik = kitapBaslik;
        BirimFiyat = birimFiyat;
        Adet = adet;
    }

    public decimal SatirToplami() => BirimFiyat.Deger * Adet;
}
