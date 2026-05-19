# Gün 57 — DDD Taktiksel Kavramlar

DDD (Domain-Driven Design) stratejik ve taktiksel olmak üzere iki katmanda incelenir. Stratejik = bounded context, ubiquitous language. Taktiksel = kod içindeki yapı taşları. Biz taktiksel kısmı işliyoruz — doğrudan Onion Architecture'ın temeli.

---

## 1. Entity vs Value Object

### Fark nedir?

| | Entity | Value Object |
|---|---|---|
| **Kimlik** | Id ile tanımlanır | Değeri ile tanımlanır |
| **Eşitlik** | `Id == Id` | Tüm alanlar eşitse eşit |
| **Mutability** | Durum değişebilir | Immutable — değişince yeni nesne |
| **Örnek** | Kitap, Siparis, Kullanici | Fiyat, Isbn, Adres, Email |

### Günlük hayat analojisi

**Entity:** Pasaportunun süresi dolup yenilediğinde hâlâ aynı kişisin — kimliğin (TC no) değişmedi.

**Value Object:** 100 TL banknotunu başka bir 100 TL ile değiştirdiğinde fark etmez — değer önemli, nesnenin kimliği değil.

### Faz2'deki durum

`Faz2-ASPNET-MVC/KitabeviMVC/Models/Entities/Kitap.cs`:

```csharp
public class Kitap
{
    public int Id { get; set; }          // Entity — kimlik var
    public decimal Fiyat { get; set; }   // Primitive — value object olmalı
    public string Kategori { get; set; } // Primitive — value object adayı
}
```

`Fiyat` alanı `decimal` — format/kural yok, para birimi yok. 50k kullanıcıda çoklu para birimi gelince tüm kodu değiştirmek zorundasın.

### Büyük projede böyle yapmalısın

```csharp
// ValueObjects.cs
public record Fiyat
{
    public decimal Deger { get; }
    public string ParaBirimi { get; }

    public Fiyat(decimal deger, string paraBirimi = "TRY")
    {
        if (deger <= 0)
            throw new ArgumentException("Fiyat 0'dan büyük olmalı");
        // kural tek yerde — servis, controller bilmek zorunda değil
        Deger = deger;
        ParaBirimi = paraBirimi;
    }

    public Fiyat KdvEkle(decimal oran = 0.18m) => new(Deger * (1 + oran), ParaBirimi);
    // yeni Fiyat döner — mevcut değişmez (immutable)
}

public record Isbn
{
    public string Deger { get; }

    public Isbn(string deger)
    {
        var temiz = deger.Replace("-", "").Replace(" ", "");
        if (temiz.Length != 13 || !temiz.All(char.IsDigit))
            throw new ArgumentException($"Geçersiz ISBN: {deger}");
        Deger = temiz;
    }
}
```

Value semantics — `record` ile ücretsiz:

```csharp
new Fiyat(100) == new Fiyat(100)  // → true
new Kitap(1)   == new Kitap(1)    // → false (farklı nesne, aynı Id)
```

---

## 2. Aggregate ve Aggregate Root

### Ne?

**Aggregate:** Birlikte tutarlı olması gereken entity + value object grubu.  
**Aggregate Root:** O gruba tek giriş noktası. Dışarıdan alt entity'lere direkt erişilmez — root üzerinden geçilir.

### Günlük hayat analogu

Alışveriş sepeti düşün. Sepete ürün eklersin, çıkarırsın, miktarı değiştirirsin. Ama şu kurallar var:

- Aynı kitabı iki kez ekleyemezsin — miktar artar
- Boş sepeti ödemeye gönderemezsin
- Bir kitaptan maksimum 5 adet eklenebilir

Bu kurallar **sepetin kuralları** — her ürün kaleminin değil. Kim yönetmeli? Sepet yönetmeli.

---

### Önce sorun: kural yok, her şey public

```csharp
// ❌ Aggregate yok — Sepet sadece liste tutan çanta
public class Sepet
{
    public int Id { get; set; }
    public string KullaniciId { get; set; }
    public List<SepetKalemi> Kalemler { get; set; } = new();
    // List<> public → dışarıdan serbestçe .Add() / .Remove() yapılabiliyor
}

public class SepetKalemi
{
    public int KitapId { get; set; }
    public string KitapBaslik { get; set; }
    public int Adet { get; set; }         // public set → dışarıdan -99 yazılabilir
    public decimal BirimFiyat { get; set; }
}
```

Bu yapıyla şunları önleyemiyorsun:

```csharp
var sepet = await _sepetRepo.GetByIdAsync(kullaniciId);

// Aynı kitabı iki kez ekle — Liste buna izin veriyor
sepet.Kalemler.Add(new SepetKalemi { KitapId = 3, Adet = 1, BirimFiyat = 150 });
sepet.Kalemler.Add(new SepetKalemi { KitapId = 3, Adet = 1, BirimFiyat = 150 });
// Şimdi sepette KitapId=3 iki kez var — tutarsız

// Adeti negatif yap
sepet.Kalemler[0].Adet = -5;
// Çalışır — hiçbir şey engellemez

// Boş sepeti ödemeye gönder
await _odemeServisi.OdemeAl(sepet);
// Kaç TL? Kalem yok — 0 TL ödeme mi yapacağız?
```

Kural yazmak için her servise aynı if'i koyuyorsun:

```csharp
// SepetServisi
if (sepet.Kalemler.Any(k => k.KitapId == kitapId))  // aynı kitap var mı?
    sepet.Kalemler.First(k => k.KitapId == kitapId).Adet++;
else
    sepet.Kalemler.Add(...);

// MobilApiSepetServisi — aynı kontrol kopyalandı
if (sepet.Kalemler.Any(k => k.KitapId == kitapId))  // kopya
    sepet.Kalemler.First(k => k.KitapId == kitapId).Adet++;
else
    sepet.Kalemler.Add(...);
```

Birini unutursan sepette aynı kitaptan iki satır oluyor — ödeme tutarsız hesaplanıyor.

---

### Çözüm: Sepet Aggregate Root

```csharp
// ✅ Sepet — Aggregate Root
public class Sepet
{
    public int Id { get; private set; }
    public string KullaniciId { get; private set; }

    private readonly List<SepetKalemi> _kalemler = [];
    public IReadOnlyList<SepetKalemi> Kalemler => _kalemler;
    //     ↑ IReadOnlyList: dışarıdan .Add() / .Remove() çağrılamaz
    //       bunu yazmasaydık → herkes sepet.Kalemler.Add() yapabilirdi, kural devre dışı

    public Sepet(string kullaniciId)
    {
        KullaniciId = kullaniciId;
    }

    public void UrunEkle(int kitapId, string kitapBaslik, Fiyat birimFiyat, int adet)
    {
        if (adet <= 0)
            throw new ArgumentException("Adet sıfırdan büyük olmalı");

        if (adet > 5)
            throw new InvalidOperationException("Bir üründen en fazla 5 adet eklenebilir");
        //  ↑ iş kuralı burada — SepetServisi if yazmadı

        var mevcutKalem = _kalemler.FirstOrDefault(k => k.KitapId == kitapId);

        if (mevcutKalem is not null)
        {
            mevcutKalem.AdetArtir(adet);
            //          ↑ aynı kitap var → miktar artar, yeni satır açılmaz
            //            bu kural burada — her servis ayrı yazmak zorunda değil
        }
        else
        {
            _kalemler.Add(new SepetKalemi(kitapId, kitapBaslik, birimFiyat, adet));
            //          ↑ private listeye ekleme sadece bu metod üzerinden
        }
    }

    public void UrunCikar(int kitapId)
    {
        var kalem = _kalemler.FirstOrDefault(k => k.KitapId == kitapId);

        if (kalem is null)
            throw new InvalidOperationException("Sepette böyle bir ürün yok");
        //  ↑ olmayan ürünü çıkarmaya çalışınca burada patlar — servis kontrol etmek zorunda değil

        _kalemler.Remove(kalem);
    }

    public void Temizle() => _kalemler.Clear();

    public decimal ToplamTutar()
        => _kalemler.Sum(k => k.BirimFiyat.Deger * k.Adet);
    //  ↑ toplam hesabı root'ta — her zaman güncel, dışarıdan değiştirilemiyor

    public void OdemeIcinDogrula()
    {
        if (!_kalemler.Any())
            throw new InvalidOperationException("Sepet boş, ödeme yapılamaz");
        //  ↑ boş sepeti ödemeye gönderme kuralı burada
    }
}

// SepetKalemi — alt entity, sadece Sepet içinden yönetiliyor
public class SepetKalemi
{
    public int KitapId { get; private set; }
    public string KitapBaslik { get; private set; }
    public Fiyat BirimFiyat { get; private set; }
    public int Adet { get; private set; }

    internal SepetKalemi(int kitapId, string kitapBaslik, Fiyat birimFiyat, int adet)
    //       ↑ internal: sadece aynı assembly içinden çağrılabilir (Sepet.UrunEkle)
    //         bunu yazmasaydık → dışarıdan new SepetKalemi() ile root bypass edilirdi
    {
        KitapId = kitapId;
        KitapBaslik = kitapBaslik;
        BirimFiyat = birimFiyat;
        Adet = adet;
    }

    internal void AdetArtir(int eklenecek)
    //         ↑ internal: sadece Sepet.UrunEkle çağırabilir — dışarıdan kalem.AdetArtir() yok
    {
        if (Adet + eklenecek > 5)
            throw new InvalidOperationException("Bir üründen en fazla 5 adet olabilir");
        Adet += eklenecek;
    }
}
```

Şimdi servis kodu:

```csharp
// SepetServisi — iş kuralı yazmıyor, Sepet'e soruyor
public class SepetServisi
{
    private readonly ISepetRepository _sepetRepo;

    public async Task UrunEkleAsync(string kullaniciId, int kitapId, string baslik, decimal fiyat, int adet)
    {
        var sepet = await _sepetRepo.GetByKullaniciAsync(kullaniciId)
                    ?? new Sepet(kullaniciId);

        sepet.UrunEkle(kitapId, baslik, new Fiyat(fiyat), adet);
        //   ↑ "aynı kitap var mı?", "adet 5'i geçti mi?" — Sepet kontrol ediyor
        //     SepetServisi bunları bilmek zorunda değil

        await _sepetRepo.KaydetAsync(sepet);
    }

    public async Task OdemeBaslatAsync(string kullaniciId)
    {
        var sepet = await _sepetRepo.GetByKullaniciAsync(kullaniciId);

        sepet.OdemeIcinDogrula();
        //   ↑ boş mu? Sepet söyler — servis if yazmadı

        var tutar = sepet.ToplamTutar();
        await _odemeServisi.OdemeAl(kullaniciId, tutar);
    }
}

// MobilApiSepetServisi — aynı aggregate, ayrı if yok
public class MobilApiSepetServisi
{
    public async Task UrunEkleAsync(string kullaniciId, int kitapId, string baslik, decimal fiyat, int adet)
    {
        var sepet = await _sepetRepo.GetByKullaniciAsync(kullaniciId) ?? new Sepet(kullaniciId);
        sepet.UrunEkle(kitapId, baslik, new Fiyat(fiyat), adet);
        // aynı kurallar — kopyalamadık
        await _sepetRepo.KaydetAsync(sepet);
    }
}
```

---

### Invariant nedir?

Aggregate'in **hiçbir koşulda ihlal edilemeyecek kuralları.**

| Invariant | Nerede korunuyor |
|---|---|
| Aynı kitap iki kez eklenemez — miktar artar | `UrunEkle()` |
| Bir üründen max 5 adet | `UrunEkle()` + `AdetArtir()` |
| Boş sepet ödemeye gönderilemez | `OdemeIcinDogrula()` |
| Kalemler dışarıdan doğrudan değiştirilemez | `IReadOnlyList` + `internal` constructor |
| Kalem adeti dışarıdan değiştirilemez | `AdetArtir()` internal |

### 500 vs 50k

| | 500 | 50k |
|---|---|---|
| **Basit CRUD** | Entity yeterli, aggregate overkill | — |
| **Birden fazla entity birlikte tutarlı olmalı** | ✅ Aggregate düşün | ✅ Şart |
| **Overengineering sinyali** | Tek entity, iş kuralı yok → aggregate açma | — |

---

## 3. Domain Event

### Ne?

Aggregate içinde önemli bir şey oldu — dış dünyayı haberdar et. Observer pattern'in domain katmanındaki uygulaması.

### Neden aggregate içinde event fırlatmıyoruz?

```csharp
// ❌ Aggregate içinde doğrudan email gönderme
public void Onayla()
{
    Durum = SiparisDurumu.Onaylandi;
    _emailServisi.Gonder(MusteriEmail, "Siparişiniz alındı");
    // SRP ihlali: Siparis email servisini biliyor
    // Test: Siparis'i test etmek için EmailServisi mock gerekiyor
}
```

```csharp
// ✅ Domain event topla, SaveChanges sonrası dispatch et
public void Onayla()
{
    Durum = SiparisDurumu.Onaylandi;
    _domainEvents.Add(new SiparisOlusturulduEvent(Id, MusteriEmail, ToplamTutar()));
    // Siparis EmailServisi'ni bilmiyor — sadece "onaylandım" diyor
    // Kim dinlerse o tepki verir (Observer/Mediator)
}
```

Gerçek projede `SaveChanges()` sonrası:

```csharp
// Handler veya UnitOfWork içinde
foreach (var aggregate in degisenler)
    foreach (var domainEvent in aggregate.DomainEvents)
        await _mediator.Publish(domainEvent);
// MediatR INotificationHandler<SiparisOlusturulduEvent> bulur, çalıştırır
```

### Faz2 ile bağlantı

Gün 35'te MediatR `INotification` + `INotificationHandler` — domain event dispatch mekanizması tam bu. Onion bölümünde Siparis aggregate'ini yazarken bu pattern'i oturacak.

---

## 4. Repository (DDD perspektifinden)

DDD'de repository **aggregate erişim kapısı**. Tek tek entity'ler için değil.

```csharp
// ✅ DDD repository: aggregate bazında
public interface ISiparisRepository
{
    Task<Siparis?> BulByIdAsync(int id);
    Task EkleAsync(Siparis siparis);
    Task GuncelleAsync(Siparis siparis);
    // SiparisKalemi için ayrı repository YOK
    // Kalemler Siparis üzerinden yönetilir
}

// ❌ Yanlış: her entity için ayrı repository
public interface ISiparisKalemiRepository  // bunu YAZMA
```

Faz2'de `IKitapRepository` var — doğru yaklaşım. `IKitapSorguServisi` ise repository'nin dışına çıkan sorgu metodları — CQRS'te Query tarafına geçecek.

---

## 5. Domain Service

Aggregate'e sığmayan, birden fazla aggregate'i ilgilendiren iş mantığı.

```csharp
// Stok kontrolü: Siparis + Kitap aggregate'lerini birlikte kullanıyor
// İkisine de ait değil — ayrı bir domain service
public class StokKontrolServisi
{
    public bool SiparisIcinStokYeterli(Siparis siparis, IEnumerable<Kitap> kitaplar)
    {
        foreach (var kalem in siparis.Kalemler)
        {
            var kitap = kitaplar.FirstOrDefault(k => k.Id == kalem.KitapId);
            if (kitap is null || kitap.StokAdedi < kalem.Adet)
                return false;
        }
        return true;
    }
}
```

**Kural:** Bir iş mantığı tek aggregate'e ait değilse → Domain Service.

---

## 6. Ubiquitous Language

Kod, domain uzmanıyla aynı dili konuşmalı.

```csharp
// ❌ Teknik dil
public void UpdateStatus(int statusCode) { ... }
public bool CheckIfAvailable(int qty) { ... }

// ✅ Domain dili (Ubiquitous Language)
public void Onayla() { ... }
public bool StokYeterli(int istenenAdet) { ... }
```

Faz2'deki `KategoriyeGoreGetirAsync`, `AyniKategoridekilerAsync` — Türkçe domain dili, doğru yöntem.

---

## DDD Yapı Taşları Özet

```
Entity        → Id'si olan, zamanla değişen nesne (Kitap, Siparis)
Value Object  → Değeri önemli, immutable (Fiyat, Isbn, Adres)
Aggregate     → Birlikte tutarlı olması gereken grup
Aggregate Root → Gruba tek giriş noktası, invariantları koruyor
Domain Event  → "Şu oldu" haberi — aggregate SRP'yi korumak için fırlatır
Repository    → Aggregate'e erişim kapısı
Domain Service → Birden fazla aggregate'i ilgilendiren iş mantığı
```

---

## 500 vs 50k

| Kavram | 500 | 50k |
|---|---|---|
| Entity + Value Object | Value object sadece tekrar eden kural varsa | ✅ Para birimi, format kuralları için şart |
| Aggregate | Basit CRUD → overkill | ✅ Karmaşık domain kurallarında şart |
| Domain Event | Tek bildirim → direkt çağır | ✅ Loose coupling, birden fazla alıcı |
| Domain Service | — | ✅ Birden fazla aggregate koordinasyonu |

---

## Sorular

1. `Fiyat` neden Entity değil Value Object? İki farklı `Fiyat(100)` nesnesi eşit mi?
2. `Siparis` neden `SiparisKalemi`'ne direkt erişim vermiyor?
3. Domain event neden aggregate içinde dispatch edilmez, `SaveChanges` sonrasına bırakılır?
