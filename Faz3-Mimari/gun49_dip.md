# Gün 49 — Dependency Inversion Principle (DIP)

---

## 1. Günlük Hayat Analojisi

Bir lamba düşün. Duvar prizine direkt kablo ile bağlıysa — priz değişince lambayı da değiştirmek zorundasın. Ama aradaki soket standardı (interface) sayesinde lamba değişebilir, priz değişebilir, birbirlerini tanımaları gerekmiyor.

**DIP:** Yüksek seviyeli modül (lamba) düşük seviyeli modüle (priz) direkt bağlı olmamalı. İkisi de soyutlamaya (soket standardı = interface) bağımlı olmalı.

---

## 2. Tanım

> "High-level modules should not depend on low-level modules. Both should depend on abstractions."
> — Robert C. Martin

```
❌ Yanlış:  KitapServisi → EfKitapRepository (somut sınıfa bağımlı)
✅ Doğru:   KitapServisi → IKitapRepository ← EfKitapRepository
```

**Kritik nokta:** `IKitapRepository` interface'i **yüksek seviyeli modülde** (domain/application) yaşar — `EfKitapRepository`'nin yanında değil. Bu "inversion" kelimesinin sebebi: bağımlılık yönü ters döndü.

```
Olmadan (DI olmadan):   Servis → Repository (somut)
Olunca  (DIP ile):      Servis → Interface ← Repository (somut)
                               ↑
                        interface servisin yanında yaşıyor
```

---

## 3. DIP ≠ Dependency Injection

Sık karıştırılan iki kavram:

| | DIP | DI (Dependency Injection) |
|---|---|---|
| **Ne?** | Prensip — bağımlılık yönü kuralı | Teknik — bağımlılıkları dışarıdan verme yöntemi |
| **Kim tanımladı?** | Robert C. Martin (SOLID'in D'si) | Design pattern |
| **İlişki** | DI, DIP'i uygulamanın bir yoludur | DIP olmadan DI yapılabilir ama prensip ihlal edilebilir |

Kısaca: **DIP ne yapılacağını söyler, DI nasıl yapılacağını gösterir.**

---

## 4. Faz2'de Böyle Yaptık

`Faz2-ASPNET-MVC/KitabeviMVC/Controllers/KitapController.cs`:

```csharp
public class KitapController : Controller
{
    private readonly IKitapServisi _kitapServisi;        // interface — somut tip değil
    private readonly IKitapSorguServisi _sorguServisi;   // interface
    private readonly IKitapBatchServisi _batchServisi;   // interface

    public KitapController(
        IKitapServisi kitapServisi,
        IKitapSorguServisi sorguServisi,
        IKitapBatchServisi batchServisi, ...)
    {
        _kitapServisi = kitapServisi;
        // Controller EfKitapServisi'ni bilmiyor — sadece interface'i biliyor
        // DIP: yüksek seviyeli modül (Controller) soyutlamaya bağımlı
    }
}
```

`Program.cs`'de kayıt:

```csharp
builder.Services.AddScoped<IKitapServisi>(sp =>
    new CachedKitapServisi(
        sp.GetRequiredService<EfKitapServisi>(), ...));
// Controller hangi implementasyonun geldiğini bilmiyor
// Sadece Program.cs biliyor — bu DI container'ın işi
```

**500 kullanıcıda:** `CachedKitapServisi` yerine `EfKitapServisi` koysaydın — controller'a hiç dokunmazdın. Tek değişiklik `Program.cs`.

---

## 5. DIP İhlali — Olmadan Ne Olurdu?

```csharp
// ❌ Controller doğrudan somut tipe bağımlı
public class KitapController : Controller
{
    private readonly EfKitapServisi _servis;   // somut sınıf — DIP ihlali

    public KitapController()
    {
        _servis = new EfKitapServisi(...);     // new → bağımlılık içeride üretiliyor
        // bunu yazmasaydık → test için gerçek DB şart
        // CachedKitapServisi'ne geçmek istersen controller'ı açmak zorundasın
        // EfKitapServisi constructor'ı değişince controller da değişiyor
    }
}
```

---

## 6. Büyük Projede Böyle Yapmalısın

```csharp
// IKitapRepository.cs — domain katmanında yaşar, EF Core bilmez
public interface IKitapRepository
{
    List<string> HepsiniGetir();
    void Ekle(string baslik);
    // abstraction yüksek seviyeli modülün yanında — "inversion" bu
}
```

```csharp
// KitapServisi.cs — yüksek seviyeli modül, iş kuralları burada
public class KitapServisi
{
    private readonly IKitapRepository _repository;
    // interface tipinde — somut tip değil
    // bunu yazmasaydık → Dapper'a geçince bu class açılırdı

    public KitapServisi(IKitapRepository repository)
    {
        _repository = repository;
        // dışarıdan geliyor — test: InMemory ver, prod: EF ver
        // KitapServisi kim olduğunu bilmiyor, önemsemiyor
    }

    public void KitapEkle(string baslik)
    {
        if (string.IsNullOrWhiteSpace(baslik))
            throw new ArgumentException("Başlık boş olamaz");
        // iş kuralı burada — repository'ye taşınmaz

        _repository.Ekle(baslik);
        // nasıl kaydedileceği repository'nin sorumluluğu
    }
}
```

```csharp
// EfKitapRepository.cs — infrastructure katmanında yaşar
// domain'e bağımlı (IKitapRepository implement ediyor)
// domain EF Core'u bilmiyor — bağımlılık yönü: infrastructure → domain
public class EfKitapRepository : IKitapRepository { ... }

// InMemoryKitapRepository.cs — test için
// KitapServisi bunu bilmiyor, IKitapRepository gördüğü için kabul eder
public class InMemoryKitapRepository : IKitapRepository { ... }
```

Kullanım:

```csharp
// Production
var servis = new KitapServisi(new EfKitapRepository());

// Test — KitapServisi kodu değişmedi, repository değişti
var servis = new KitapServisi(new InMemoryKitapRepository());
```

---

## 7. Katman Bağımlılık Yönü

```
┌─────────────────────────────┐
│   Controller (Presentation) │
│   KitapServisi (Application)│  ← yüksek seviyeli
│   IKitapRepository          │  ← abstraction burada yaşıyor
├─────────────────────────────┤
│   EfKitapRepository         │  ← düşük seviyeli (infrastructure)
│   InMemoryKitapRepository   │  ← test implementasyonu
└─────────────────────────────┘

Bağımlılık yönü: aşağı → yukarı (infrastructure → domain)
Hiçbir zaman: yukarı → aşağı (domain → infrastructure)
```

Bu diagram Faz3'ün ilerleyen günlerinde Onion Architecture'ın tam temeli olacak.

---

## 8. 500 vs 50k Kullanıcı

| | 500 kullanıcı/ay | 50k kullanıcı/ay |
|---|---|---|
| **`new SomutServis()` bırak?** | Küçük, tek katmanlı proje → çalışır | ❌ Test edilemez, ORM değiştirilemez |
| **DIP uygula?** | Unit test yazacaksan → şart | ✅ Kesinlikle — ekip + uzun ömür + test coverage |
| **Overengineering sinyali** | Script / tek seferlik araç için interface açmak | — |

---

## Sorular

1. `IKitapRepository`'nin `EfKitapRepository` yanında değil, `KitapServisi` yanında yaşaması neden önemli?
2. Faz2'de `Program.cs`'te `builder.Services.AddScoped<IKitapServisi, EfKitapServisi>()` yazdığımızda hangi SOLID prensibi devreye giriyor?
3. DIP olmadan Faz2'deki `CachedKitapServisi`'ni ekleyebilir miydin? Ne değişmek zorunda kalırdı?
