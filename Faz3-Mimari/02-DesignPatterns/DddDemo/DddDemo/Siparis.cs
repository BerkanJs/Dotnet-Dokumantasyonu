namespace DddDemo;

// DOMAIN EVENT: aggregate içinde önemli bir şey oldu
// Observer pattern — aggregate dışı kod tetiklenir
public record SiparisOlusturulduEvent(int SiparisId, string MusteriEmail, decimal Tutar);
public record SiparisIptalEdildiEvent(int SiparisId, string Sebep);

// ENTITY: kimliği olan nesne
// Id aynıysa aynı entity — değerleri farklı olsa bile
public class SiparisKalemi
{
    public int KitapId { get; }
    public string KitapBaslik { get; }
    public Fiyat BirimFiyat { get; }
    public int Adet { get; private set; }

    public SiparisKalemi(int kitapId, string kitapBaslik, Fiyat birimFiyat, int adet)
    {
        if (adet <= 0) throw new ArgumentException("Adet 0'dan büyük olmalı");
        KitapId = kitapId;
        KitapBaslik = kitapBaslik;
        BirimFiyat = birimFiyat;
        Adet = adet;
    }

    public Fiyat ToplamFiyat => new(BirimFiyat.Deger * Adet, BirimFiyat.ParaBirimi);
}

// AGGREGATE ROOT: transaction boundary
// Siparis tüm kalemlerinin tutarlılığından sorumlu
// Dışarıdan SiparisKalemi'ne direkt erişilmez — Siparis üzerinden geçilir
public class Siparis
{
    public int Id { get; }
    public string MusteriEmail { get; }
    public SiparisDurumu Durum { get; private set; }

    private readonly List<SiparisKalemi> _kalemler = [];
    public IReadOnlyList<SiparisKalemi> Kalemler => _kalemler;
    // IReadOnlyList: dışarıdan Add/Remove yapılamaz — sadece Siparis kontrolünde
    // bunu yazmasaydık → _kalemler.Add() dışarıdan çağrılabilirdi → invariant bozulurdu

    private readonly List<object> _domainEvents = [];
    public IReadOnlyList<object> DomainEvents => _domainEvents;
    // bunu yazmasaydık → domain event'leri doğrudan fırlatmak zorunda kalırdık
    // SaveChanges sonrası dispatch etmek için önce toplamak gerekiyor

    public Siparis(int id, string musteriEmail)
    {
        if (string.IsNullOrWhiteSpace(musteriEmail))
            throw new ArgumentException("Email boş olamaz");
        Id = id;
        MusteriEmail = musteriEmail;
        Durum = SiparisDurumu.Bekliyor;
    }

    public void KalemEkle(int kitapId, string baslik, Fiyat fiyat, int adet)
    {
        if (Durum != SiparisDurumu.Bekliyor)
            throw new InvalidOperationException("Onaylanmış siparişe kalem eklenemez");
        // invariant: onaylanmış siparişe dokunulamaz
        // bunu yazmasaydık → onaylandıktan sonra kalem eklenebilirdi

        var mevcutKalem = _kalemler.FirstOrDefault(k => k.KitapId == kitapId);
        if (mevcutKalem is not null)
        {
            _kalemler.Remove(mevcutKalem);
            _kalemler.Add(new SiparisKalemi(kitapId, baslik, fiyat, mevcutKalem.Adet + adet));
            // aynı kitap varsa adedi artır — aggregate tutarlılığı burada sağlanıyor
        }
        else
        {
            _kalemler.Add(new SiparisKalemi(kitapId, baslik, fiyat, adet));
        }
    }

    public void Onayla()
    {
        if (!_kalemler.Any())
            throw new InvalidOperationException("Boş sipariş onaylanamaz");
        // invariant: kalemi olmayan sipariş onaylanamaz

        Durum = SiparisDurumu.Onaylandi;

        _domainEvents.Add(new SiparisOlusturulduEvent(Id, MusteriEmail, ToplamTutar()));
        // domain event: "sipariş onaylandı" — email servisi bunu dinleyecek
        // bunu yazmasaydık → Onayla() içinde email kodu olurdu (SRP ihlali)
    }

    public void Iptal(string sebep)
    {
        if (Durum == SiparisDurumu.Teslim)
            throw new InvalidOperationException("Teslim edilmiş sipariş iptal edilemez");

        Durum = SiparisDurumu.Iptal;
        _domainEvents.Add(new SiparisIptalEdildiEvent(Id, sebep));
    }

    public decimal ToplamTutar() => _kalemler.Sum(k => k.ToplamFiyat.Deger);
}

public enum SiparisDurumu { Bekliyor, Onaylandi, Teslim, Iptal }
