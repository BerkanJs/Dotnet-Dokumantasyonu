# Gün 61 — Faz3 Hafta Özeti: Clean Architecture Tam Resim

Faz3'te şu yolculuğu yaptık:

```
SOLID (Gün 50)
    ↓
Design Patterns (Gün 51–55)
    ↓
Anti-Patterns (Gün 56)
    ↓
DDD Taktiksel (Gün 57)
    ↓
Onion Mimari — Teori (Gün 58)
    ↓
Onion Mimari — Uygulama (Gün 59)
    ↓
CQRS + MediatR (Gün 60)
    ↓
Bugün: Hepsini birleştir, ne öğrendik, ne zaman kullanılır
```

---

## Katmanlar Arası Bağımlılık — Tek Resim

```
┌──────────────────────────────────────────────────┐
│  API (Presentation)                              │
│  SiparisController, KitapController              │
│  GlobalExceptionHandler, Program.cs              │
│                                                  │
│  Biliyor: Application (IMediator)                │
│  Bilmiyor: Domain entity detayı, EF Core         │
└──────────────────────┬───────────────────────────┘
                       │ _mediator.Send()
┌──────────────────────▼───────────────────────────┐
│  Application                                     │
│  SiparisOlusturHandler, KitapListeleHandler      │
│  LoggingBehavior, ValidationBehavior             │
│  IEmailService (interface tanımı)                │
│                                                  │
│  Biliyor: Domain (entity, interface, exception)  │
│  Bilmiyor: EF Core, SmtpClient, DbContext        │
└──────────────────────┬───────────────────────────┘
                       │ IKitapRepository, ISiparisRepository
┌──────────────────────▼───────────────────────────┐
│  Domain (merkez)                                 │
│  Kitap, Siparis, SiparisKalemi (entity)          │
│  Fiyat, Isbn (value object)                      │
│  IKitapRepository, ISiparisRepository (interface)│
│  DomainException                                 │
│                                                  │
│  Biliyor: hiçbir şey — sıfır dış bağımlılık     │
└──────────────────────────────────────────────────┘
                       ▲
                       │ implement eder
┌──────────────────────┴───────────────────────────┐
│  Infrastructure                                  │
│  KitapRepository, SiparisRepository (EF Core)   │
│  AppDbContext                                    │
│  EmailService (SmtpClient / SendGrid)            │
│                                                  │
│  Biliyor: Domain + Application (interface için)  │
│  Bilmiyor: Controller, HTTP, MediatR             │
└──────────────────────────────────────────────────┘
```

---

## Bir Request'in Tam Yolculuğu

`POST /api/siparis {"kullaniciId": "u1", "kitapId": 3, "adet": 2}`

```
1. SiparisController
   │  [FromBody] → SiparisOlusturCommand { "u1", 3, 2 }
   │  _mediator.Send(cmd)

2. LoggingBehavior
   │  "→ SiparisOlusturCommand başladı"
   │  next()

3. ValidationBehavior
   │  KullaniciId boş mu? → Hayır ✓
   │  KitapId > 0 mu? → 3 > 0 ✓
   │  Adet 1-10 arası mı? → 2 ✓
   │  next()

4. SiparisOlusturHandler
   │  kitapRepo.BulByIdAsync(3) → Kitap { Fiyat(150), Stok: 10 }
   │  kitap.StokAzalt(2) → Stok: 8 [Domain kuralı: 2 <= 10 ✓]
   │  new Siparis("u1") → Durum: Beklemede
   │  siparis.KalemEkle(3, "Clean Code", Fiyat(150), 2)
   │  siparis.Onayla() → Durum: Onaylandi, DomainEvent toplandı
   │  siparisRepo.EkleAsync(siparis)
   │  siparisRepo.KaydetAsync() → SaveChanges() — tek transaction
   │  mediator.Publish(SiparisOlusturulduNotification)
   │      ├─ SiparisEmailHandler → log "email gönderildi"
   │      └─ SiparisBildirimHandler → log "bildirim"
   │  return SiparisOlusturResult { SiparisId: 1, ToplamTutar: 300 }

5. LoggingBehavior
   │  "← SiparisOlusturCommand tamamlandı: 38ms"

6. SiparisController
   │  201 Created
   │  { "siparisId": 1, "toplamTutar": 300.00 }
```

---

## Faz2 → Faz3 Dönüşüm Tablosu

| Faz2 Yapısı | Faz3 Karşılığı | Kazanım |
|---|---|---|
| `KitapController` → `KitabeviDbContext` | Controller → MediatR → Handler → IRepository | DB değişirse Controller dokunulmuyor |
| `decimal Fiyat` field | `Fiyat` Value Object | Format/kural tek yerde |
| `public int StokAdedi { get; set; }` | `private set` + `StokAzalt()` | Stok kuralı domain'de |
| `KitapServisi` iş mantığı taşıyor | `SiparisOlusturHandler` | Tek sorumluluk |
| Her Controller'da try/catch | `GlobalExceptionHandler` | Tutarlı hata formatı |
| Controller'da validation | `ValidationBehavior` pipeline | Her handler için otomatik |
| Controller'da logging | `LoggingBehavior` pipeline | Her request için otomatik |
| Doğrudan `SmtpClient` çağrısı | `IEmailService` → `EmailService` | Test'te mock, prod'da gerçek |

---

## Anti-Pattern Kontrol Listesi (Gün 56 + Gün 58)

**Bunlardan birini görüyorsan → yanlış yaptın:**

```csharp
// ❌ Handler sadece repo çağırıyor — neden var?
public async Task<Kitap> Handle(KitapGetQuery q, ...)
    => await _repo.GetByIdAsync(q.Id);
// Düzeltme: Bu query'ye handler gerekmiyor, direkt repo yeterli

// ❌ Domain entity'de hiç method yok
public class Siparis { public string Durum { get; set; } }
// Düzeltme: Onayla(), Iptal(), durum geçiş kuralları buraya

// ❌ Controller business logic içeriyor
public IActionResult Olustur(...)
{
    if (adet > 10) return BadRequest();  // bu domain kuralı
    if (stok < adet) return BadRequest(); // bu da domain kuralı
}
// Düzeltme: Kitap.StokAzalt() ve validator'a taşı

// ❌ Infrastructure interface'i Application'da implement ediyor
// (DbContext Application katmanında kullanılıyor)
// Düzeltme: DbContext sadece Infrastructure

// ❌ Domain'de using Infrastructure; yazıyor
using KitabeviOnion.Infrastructure.Persistence;
// Düzeltme: Domain hiçbir dış assembly import etmez
```

---

## Ne Zaman Hangi Katman — Karar Ağacı

```
Yeni bir iş kuralı var:
    │
    ├─ Tek entity ile ilgili mi? (ör: stok negatife düşemez)
    │       → Entity method'u (Kitap.StokAzalt)
    │
    ├─ Birden fazla entity koordinasyonu mu? (ör: sipariş + stok)
    │       → Domain Service
    │
    ├─ Dış servise bağımlı mı? (ör: email gönder, ödeme al)
    │       → Application Handler → Infrastructure interface
    │
    └─ Sadece input formatı mı? (ör: adet 1-10 arası)
            → FluentValidation → ValidationBehavior

Yeni bir endpoint:
    │
    ├─ Veri okuyorsa → Query + Handler
    └─ Veri değiştiriyorsa → Command + Handler
```

---

## Faz3 Kontrol Soruları

1. `IKitapRepository` neden Domain katmanında tanımlanıyor, Application'da değil?

2. Şu kodu gördün — hangi katmanda olmamalı, neden, nereye taşımalısın?
   ```csharp
   public class SiparisHandler
   {
       public async Task Handle(...)
       {
           if (cmd.Adet > kitap.StokAdedi)
               throw new Exception("Yetersiz stok");
       }
   }
   ```

3. `INotification` ile `IRequest<T>` farkı ne? `Publish()` ile `Send()` ne zaman?

4. `ValidationBehavior` domain kuralını da içerebilir mi? Neden içermemeli?

5. SQL Server'dan PostgreSQL'e geçiyorsunuz. Hangi dosyaları değiştirirsiniz, hangilerine dokunmazsınız?

6. `GlobalExceptionHandler` olmasaydı `DomainException` nasıl davranırdı?

7. Yeni bir bildirim kanalı (SMS) ekliyorsunuz. Hangi dosyayı oluşturursunuz, hangisine dokunmazsınız?

---

## Faz4'e Geçiş

Faz3'te mimariyi oturdurduk. Faz4'te bu mimarinin altında ne olduğuna bakıyoruz:

```
Faz4 — Performans & Gözlemlenebilirlik
├── pprof / BenchmarkDotNet  ← darboğaz nerede?
├── EF Core sorgu optimizasyonu ← N+1, compiled query, AsNoTracking
├── Redis cache ← okuma baskısını azalt
├── OpenTelemetry ← trace, metric, log birlikte
└── Health check ← Kubernetes probe'ları
```

Faz3'te inşa ettiğimiz katmanlı yapı Faz4'te avantaj sağlayacak: cache eklemek için sadece `KitapRepository`'ye `CachingKitapRepository` decorator yazıyoruz, handler'a dokunmuyoruz.
