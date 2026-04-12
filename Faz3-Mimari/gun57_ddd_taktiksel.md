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

**Aggregate Root:** Gruba tek giriş noktası. Dışarıdan sadece root'a erişilir, alt entity'lere direkt erişilmez.

### Günlük hayat analojisi

Bir banka hesabı düşün. Hesap sahibi, bakiye, işlem geçmişi birlikte bir aggregate. Dışarıdan doğrudan "işlem geçmişine kayıt ekle" diyemezsin — "hesaba para yatır" diyorsun, hesap kendi geçmişini güncelliyor. Hesap aggregate root.

### Faz2'deki durum

Faz2'de Aggregate kavramı yoktu — `Kitap` entity'si tek başınaydı. `Siparis` domain modeli hiç yazılmadı. Onion bölümünde yazacağız.

### Büyük projede böyle yapmalısın

```csharp
// Siparis.cs
public class Siparis  // Aggregate Root
{
    public int Id { get; }
    public SiparisDurumu Durum { get; private set; }

    private readonly List<SiparisKalemi> _kalemler = [];
    public IReadOnlyList<SiparisKalemi> Kalemler => _kalemler;
    // IReadOnlyList: dışarıdan Add/Remove yapılamaz
    // bunu yazmasaydık → herkes _kalemler.Add() çağırabilirdi → tutarlılık bozulurdu

    public void KalemEkle(int kitapId, string baslik, Fiyat fiyat, int adet)
    {
        if (Durum != SiparisDurumu.Bekliyor)
            throw new InvalidOperationException("Onaylanmış siparişe kalem eklenemez");
        // invariant: iş kuralı aggregate içinde korunuyor
        // bunu yazmasaydık → onaylı siparişe kalem eklenebilirdi
        _kalemler.Add(new SiparisKalemi(kitapId, baslik, fiyat, adet));
    }

    public void Onayla()
    {
        if (!_kalemler.Any())
            throw new InvalidOperationException("Boş sipariş onaylanamaz");
        // invariant: kalemi olmayan sipariş onaylanamaz
        Durum = SiparisDurumu.Onaylandi;
    }
}
```

**Invariant:** Aggregate'in her zaman geçerli tutması gereken kural. `Onayla()` içindeki "boş sipariş onaylanamaz" bir invariant.

### 500 vs 50k

| | 500 | 50k |
|---|---|---|
| **Basit CRUD** | Entity yeterli, aggregate overkill | — |
| **Karmaşık iş kuralları, çoklu entity** | ✅ Aggregate düşün | ✅ Şart — tutarlılık garantisi |
| **Overengineering** | Tek entity için aggregate root | — |

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
