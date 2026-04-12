# Gün 56 — Anti-Patterns

Anti-pattern: çalışıyor gibi görünen ama uzun vadede zarar veren kod yapısı. Pattern'ların "ne yapmalısın" listesi varsa, anti-pattern'lar "ne yapmamalısın" listesi.

---

## 1. Anemic Domain Model

### Ne?

Entity sadece getter/setter içeriyor — hiç iş mantığı yok. Tüm logic servis katmanına dağılmış.

### Faz2'deki durum

`Faz2-ASPNET-MVC/KitabeviMVC/Models/Entities/Kitap.cs`:

```csharp
public class Kitap
{
    public int Id { get; set; }
    public string Baslik { get; set; } = string.Empty;
    public decimal Fiyat { get; set; }
    public int StokAdedi { get; set; }
    // Hiç metod yok — sadece veri torbası
}
```

Stok düşme mantığı `EfKitapServisi.StokGuncelleAsync()` içinde:

```csharp
// Servis
if (kitap.StokAdedi <= 0) throw new Exception("Stok yok");
kitap.StokAdedi--;
```

**500 kullanıcı için:** Çalışır — proje küçük, tek geliştirici.

**Sorun:** "Stok 0'ın altına inemesin" kuralı nerede yaşıyor?
- `EfKitapServisi`'nde mi?
- `KitapController`'da da aynı kontrol var mı?
- `KitapApiController`'da da?
- Yarın batch işlemde de aynı kontrolü yazmayı unuttun mu?

### Büyük projede böyle yapmalısın — Rich Domain Model

```csharp
// Onion bölümünde KitabeviOnion'a böyle geçeceğiz
public class Kitap
{
    public int Id { get; private set; }
    public string Baslik { get; private set; }
    public decimal Fiyat { get; private set; }
    public int StokAdedi { get; private set; }
    // private set: dışarıdan doğrudan atanamaz — iş mantığı metodlar üzerinden geçer

    public void StokDus()
    {
        if (StokAdedi <= 0)
            throw new InvalidOperationException("Stok yok");
        StokAdedi--;
        // kural tek yerde — Servis, Controller, BatchJob hepsi bu metodu çağırır
    }

    public void FiyatGuncelle(decimal yeniFiyat)
    {
        if (yeniFiyat <= 0)
            throw new ArgumentException("Fiyat 0'dan büyük olmalı");
        Fiyat = yeniFiyat;
        // validasyon entity'de — servis bilmek zorunda değil
    }
}
```

### 500 vs 50k

| | 500 | 50k |
|---|---|---|
| **Anemic bırak?** | Küçük proje, kural az → çalışır | ❌ Kural birden fazla yerde kopyalanıyor |
| **Rich domain?** | Kural 3+ yerde tekrarlanıyorsa geç | ✅ Şart — tek doğru kaynak |
| **Overengineering** | Her entity için metod açmak | — |

---

## 2. God Class

### Ne?

Her şeyi bilen, her şeyi yapan tek sınıf. SRP'nin tam tersi.

### Faz2'deki durum

`EfKitapServisi` Gün 33 itibarıyla `IKitapServisi` + `IKitapSorguServisi` + `IKitapBatchServisi` implement ediyor:

```csharp
public class EfKitapServisi : IKitapServisi, IKitapSorguServisi, IKitapBatchServisi
```

3 interface + 300+ satır → God Class sınırına yaklaşıyor. Faz2'de bunu bilerek kabul ettik çünkü demo amaçlıydı. Onion bölümünde bunu bölüyoruz.

**Sinyal:** Dosyayı açtığında "hangi satıra bakacağım?" diye arama yapıyorsun.

---

## 3. Service Locator

### Ne?

Bağımlılıkları constructor'dan almak yerine içeride DI container'dan çekiyorsun. Hangi bağımlılığın kullanıldığı dışarıdan görünmüyor.

```csharp
// ❌ Service Locator
public class KitapServisi
{
    private readonly IServiceProvider _provider;

    public KitapServisi(IServiceProvider provider)
        => _provider = provider;

    public void KitapEkle(KitapFormViewModel model)
    {
        var repo = _provider.GetRequiredService<IKitapRepository>();
        // bağımlılık gizli — test yazan kişi neyin mock'lanması gerektiğini bilemiyor
        var validator = _provider.GetRequiredService<IValidator<KitapFormViewModel>>();
        // constructor'a bakarak bağımlılıkları göremiyorsun
    }
}
```

```csharp
// ✅ Constructor Injection
public class KitapServisi
{
    private readonly IKitapRepository _repo;
    private readonly IValidator<KitapFormViewModel> _validator;

    public KitapServisi(IKitapRepository repo, IValidator<KitapFormViewModel> validator)
    {
        _repo = repo;
        _validator = validator;
        // bağımlılıklar constructor'da görünür → test için ne mock'lanacak belli
    }
}
```

**Neden MediatR Service Locator değil?**

`_mediator.Send(command)` tek bağımlılık: `IMediator`. Handler'ları MediatR buluyor ama bu DI container'ın kayıtlı handler'larından yapıyor — gizli değil, `Program.cs`'te kayıtlı. Test ederken `IMediator`'ı mock'layabilirsin.

---

## 4. Shotgun Surgery

### Ne?

Tek bir değişiklik için 5-10 farklı dosyaya dokunmak zorunda kalıyorsun.

### Örnek

"Kitap başlığı en az 3 karakter olsun" kuralı değişti:

```
❌ Shotgun Surgery:
  KitapController.cs → if (model.Baslik.Length < 3)
  KitapApiController.cs → aynı kontrol
  EfKitapServisi.cs → aynı kontrol
  KitapFormViewModel.cs → [MinLength(3)] attribute
  ImportServisi.cs → aynı kontrol
→ 5 yer değişiyor, birini unutma ihtimali var
```

```
✅ Tek nokta:
  KitapValidator.cs → kural burada
  Diğerleri KitapValidator'ı çağırıyor
→ 1 yer değişiyor
```

**Sinyal:** "Bu kuralı değiştireceğim" dediğinde `Ctrl+Shift+F` ile aramak zorunda kalıyorsun.

---

## 5. Primitive Obsession

### Ne?

Domain kavramını `int`, `string`, `decimal` gibi primitif tip olarak temsil etmek.

### Faz2'deki durum

```csharp
public class Kitap
{
    public decimal Fiyat { get; set; }   // hangi para birimi?
    public string Isbn { get; set; }     // format kontrolü yok
    public int StokAdedi { get; set; }  // ekside olabilir mi?
}
```

```csharp
// ❌ Her yerde primitive kontrol
if (fiyat <= 0) throw ...
if (isbn.Length != 13) throw ...
if (stok < 0) throw ...
```

```csharp
// ✅ Value Object — Onion bölümünde böyle yapacağız
public record Fiyat
{
    public decimal Deger { get; }
    public string ParaBirimi { get; }

    public Fiyat(decimal deger, string paraBirimi = "TRY")
    {
        if (deger <= 0) throw new ArgumentException("Fiyat 0'dan büyük olmalı");
        Deger = deger;
        ParaBirimi = paraBirimi;
        // kontrol tek yerde — entity her oluşturulduğunda geçerli
    }
}

public record Isbn
{
    public string Deger { get; }

    public Isbn(string deger)
    {
        if (deger.Length != 13) throw new ArgumentException("ISBN 13 karakter olmalı");
        Deger = deger;
    }
}
```

### 500 vs 50k

| | 500 | 50k |
|---|---|---|
| **1-2 kontrol** | Primitive + if yeterli | — |
| **Aynı kontrol 3+ yerde** | Value Object düşün | ✅ Şart |
| **Overengineering** | `string Ad` için record açmak | — |

---

## 6. Feature Envy

### Ne?

Bir metod kendi sınıfının verilerinden çok başka sınıfın verilerini kullanıyorsa — o metod yanlış sınıfta.

```csharp
// ❌ Feature Envy: SiparisServisi, Kitap'ın verilerini aşırı kullanıyor
public class SiparisServisi
{
    public decimal ToplamHesapla(Kitap kitap, int adet)
    {
        decimal kdv = kitap.Fiyat * 0.18m;
        decimal indirim = kitap.StokAdedi > 100 ? kitap.Fiyat * 0.05m : 0;
        return (kitap.Fiyat + kdv - indirim) * adet;
        // Kitap'ın fiyat, stok bilgilerini burada hesaplıyoruz
        // Bu mantık Kitap class'ına ait
    }
}

// ✅ Mantık doğru yere taşındı
public class Kitap
{
    public decimal KdvliFiyat() => Fiyat * 1.18m;
    public decimal IndirimliFiyat() => StokAdedi > 100 ? Fiyat * 0.95m : Fiyat;
}

public class SiparisServisi
{
    public decimal ToplamHesapla(Kitap kitap, int adet)
        => kitap.IndirimliFiyat() * adet;
    // Kitap kendi hesabını biliyor
}
```

---

## 7. Magic Numbers / Strings

### Ne?

Kodun içine gömülü açıklamasız sabit değerler.

```csharp
// ❌ Magic numbers
if (kitap.StokAdedi < 5) UyarıGonder();     // neden 5?
if (fiyat > 1000) UygulaOzelIndirim();      // neden 1000?
Thread.Sleep(30000);                         // neden 30000?
```

```csharp
// ✅ Adlandırılmış sabitler
private const int KritikStokEsigi = 5;
private const decimal OzelIndirimEsigi = 1000m;
private static readonly TimeSpan BeklemeSupresi = TimeSpan.FromSeconds(30);

if (kitap.StokAdedi < KritikStokEsigi) UyarıGonder();
```

**Faz2'de:** `CachedKitapServisi` içinde `TumKitaplarKey = "kitaplar:hepsi"` — magic string'i sabite çekerek doğru yaptık.

---

## 8. Tüm Anti-Pattern'ların SOLID Bağlantısı

```
Anemic Domain Model   → SRP ihlali: iş mantığı entity yerine serviste
God Class             → SRP ihlali: tek sınıf çok sorumluluk
Service Locator       → DIP ihlali: bağımlılıklar gizli, test edilemez
Shotgun Surgery       → SRP + OCP ihlali: değişim dağınık
Feature Envy          → SRP ihlali: metod yanlış sınıfta
Primitive Obsession   → DRY ihlali + test edilemezlik
Magic Numbers         → okunabilirlik + bakım sorunu
```

**Kural:** Anti-pattern görünce hemen düzeltme. Önce hangi SOLID prensibini ihlal ettiğini belirle — düzeltme yolu oradan çıkar.

---

## 500 vs 50k — Genel Tablo

| Anti-pattern | 500 kullanıcı | 50k kullanıcı |
|---|---|---|
| Anemic model | Kural az → tolere edilebilir | ❌ Kural dağılıyor, bug riski artar |
| God class | Tek geliştirici → idare edilir | ❌ Ekip büyüyünce merge conflict felaketi |
| Service Locator | — | ❌ Her ölçekte test edilemezlik |
| Shotgun surgery | 2-3 yer → dikkatli ol | ❌ 10+ yer → refactor kaçınılmaz |
| Primitive obsession | 1-2 kontrol | ❌ Aynı kontrol her yerde |

---

## Sorular

1. Faz2'deki `Kitap` entity'si Anemic Domain Model mi? `StokDus()` metodu entity'de mi olmalı, serviste mi?
2. MediatR neden Service Locator değil? Farkı ne?
3. Faz2'de Shotgun Surgery örneği var mı? Hangi kural birden fazla yerde yazılmış olabilir?
