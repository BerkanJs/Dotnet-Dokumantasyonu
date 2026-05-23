# Gün 128 — Saga Pattern: Distributed Transaction

## Teorik

### Önce Problemi Anlayalım: Dağıtık İşlem Nedir?

E-ticaret sitende bir sipariş şu adımları takip ediyor:

```
1. Sipariş oluştur   (OrderService)
2. Ödemeyi al        (PaymentService)
3. Stoktan düş       (StockService)
4. Kargo hazırla     (ShipmentService)
5. Email gönder      (NotificationService)
```

Her adım **farklı bir servis, farklı bir veritabanı**. Soru şu:

> "3. adımda stok yoksa ne olur? Ödeme zaten alındı. Geri vermem gerekiyor. Ama 2. adımı nasıl 'geri alırım'?"

Tek bir veritabanında bu basit — `ROLLBACK` yeterli. Ama farklı servislerde `ROLLBACK` diyemezsin. Para zaten PaymentService'te işlendi.

---

### 2-Phase Commit — Neden Çalışmaz?

**Analoji: Toplantı odası rezervasyonu**

5 kişilik bir toplantı organize ediyorsun. Herkese "Salı saat 14:00 müsait misiniz?" diye soruyorsun. Herkes "evet" diyene kadar toplantıyı onaylamıyorsun. Birisi "hayır" derse herkese "iptal" diyorsun.

Bu mantığı mikroservislere uygularsın:

```
Koordinatör: "Hepiniz hazır mısınız?" (Phase 1 — Prepare)
  OrderService:   "Hazırım" ✅
  PaymentService: "Hazırım" ✅
  StockService:   "Hazırım" ✅

Koordinatör: "Hepiniz commit edin" (Phase 2 — Commit)
  OrderService:   commit ✅
  PaymentService: commit ✅
  StockService:   commit ✅
```

Kulağa güzel geliyor. Sorunları:

```
Sorun 1 — Kilit (Lock):
  PaymentService "hazırım" dedi ve kilitlendi.
  StockService cevap vermedi (ağ sorunu).
  PaymentService ne yapacağını bilmiyor, bekliyor...
  Bu süre boyunca PaymentService başka işlem yapamıyor.
  100 kullanıcı aynı anda sipariş verse → 100 servis birbirini bekliyor → sistem çöküyor

Sorun 2 — Koordinatör çöktü:
  Phase 2'yi gönderecekti, çöktü.
  Servisler "commit mi, rollback mi?" diye sonsuza kadar bekliyor.
```

**Sonuç:** 2PC mikroservislerde kullanılmaz. Servisleri birbirine kilitler, ölçeklenmez.

---

### Saga Pattern — Çözüm

**Analoji: Yurt dışı düğün planlaması**

İstanbul'da düğün yapıyorsun ama her şey ayrı firmadan:
- Mekan: A firması, İstanbul
- Catering: B firması, Ankara'dan geliyor
- Fotoğrafçı: C firması, freelance
- Müzik: D firması

Bunları aynı anda "lock"layamazsın. Hepsini ayrı ayrı rezerve edersin. Bir şey patlarsa diğerlerini tek tek iptal edersin.

```
Düğün planı:
  Mekan rezerve edildi ✅
  Catering rezerve edildi ✅
  Fotoğrafçı rezerve edildi ✅
  Müzik → Mevcut değil ❌

  Geri alma (compensating):
  Fotoğrafçı iptal edildi
  Catering iptal edildi
  Mekan iptal edildi
```

Saga da böyle çalışır:

```
Sipariş Saga:
  Adım 1: Sipariş oluştur ✅
  Adım 2: Ödeme al ✅
  Adım 3: Stoktan düş ❌ (stok yok)

  Compensating işlemler (geri sarma):
  Adım 2 geri al: Ödemeyi iade et
  Adım 1 geri al: Siparişi iptal et
```

Her adımın bir "geri alma" işlemi var. Buna **compensating transaction** denir.

---

### Choreography vs Orchestration

Saga'yı iki şekilde uygulayabilirsin:

---

**Choreography — Koreografi (Merkezi yönetici yok)**

**Analoji: Domino taşları**

Birinci taş düştü → ikincisini devirdi → üçüncüsünü devirdi. Kimse yönetmiyor, her taş bir sonrakini tetikliyor.

```
OrderCreated event'i yayınlandı
    ↓
PaymentService duydu → ödemeyi işledi → PaymentCompleted event'i yayınladı
    ↓
StockService duydu → stoktan düştü → StockReserved event'i yayınladı
    ↓
NotificationService duydu → email gönderdi
```

Her servis sadece kendi işini yapıyor ve bir event yayınlıyor. Kim dinliyor, ne yapıyor — bilmiyor.

**Avantajları:**
- Basit başlangıç
- Servisler birbirini tanımıyor (düşük bağımlılık)

**Dezavantajları:**
- "Siparişim nerede?" sorusuna cevap vermek zor — durum 5 servise dağılmış
- Hata ayıklama karmaşık: event zinciri nerede koptu?
- Yeni adım eklemek için birçok servise dokunman gerekebilir

---

**Orchestration — Orkestrasyon (Merkezi yönetici var)**

**Analoji: Düğün organizatörü**

Bir organizatör her şeyi yönetiyor. Mekana "hazır ol" diyor, cateringe "gel" diyor, fotoğrafçıya "başla" diyor. Bir şey ters giderse herkesi tek tek arayıp iptal ediyor.

```
OrderSaga (organizatör):
  1. "PaymentService, bu siparişin ödemesini al" → bekler
  2. Ödeme geldi → "StockService, bu ürünü düş" → bekler
  3. Stok düşüldü → "NotificationService, email gönder" → bekler
  4. Hepsi tamam → sipariş tamamlandı

  Hata senaryosu:
  2. Ödeme BAŞARISIZ → "OrderService, siparişi iptal et" → bitti
```

Tüm durum tek bir yerde (Saga) tutuluyor. "Sipariş nerede?" sorusuna hemen cevap verebilirsin.

**Avantajları:**
- Durum tek yerde görünür
- Hata yönetimi merkezi
- Debug kolay

**Dezavantajları:**
- Saga servisi kritik hale gelir (çökerse tüm akış durur)
- Biraz daha fazla kod

---

### Compensating Transaction — "Geri Al" Butonu

Normal `ROLLBACK` tek bir DB'de çalışır:

```sql
BEGIN TRANSACTION
  INSERT INTO orders ...
  UPDATE stock ...
ROLLBACK  -- her şey geri döner, sanki hiç olmadı
```

Farklı servislerde bu mümkün değil. Compensating transaction, "geri alma" işlemini **yeni bir işlem** olarak yapar:

```
Yapılan iş           Compensating transaction
──────────────────────────────────────────────
Ödeme alındı    →    Para iade et
Stok düşüldü    →    Stoku geri ekle
Sipariş oluştu  →    Siparişi iptal et
```

Önemli fark:
```
ROLLBACK:               Sanki hiç olmadı
Compensating işlem:     Oldu AMA telafi edildi (iz kalır)
```

Örneğin para iadesi şöyle görünür veritabanında:
```
payments tablosu:
  TXN-001: +150 TL (ödeme alındı)
  TXN-002: -150 TL (iade edildi)   ← compensating transaction
```

---

### MassTransit State Machine ile Orchestration Saga

MassTransit'te Saga = bir **State Machine** (durum makinesi).

Durum makinesi şunu sorar:
- "Şu an hangi durumdayım?" (state)
- "Hangi event geldi?" 
- "Bu event bu durumda ne anlama gelir?"
- "Bir sonraki durum ne olmalı?"

**Sipariş Saga Durum Diyagramı:**

```
[Başlangıç]
    ↓ OrderCreated event'i geldi
[ÖdemeBekliyor]
    ↓ PaymentCompleted geldi          ↓ PaymentFailed geldi
[Tamamlandı]                       [İptalEdildi]
```

Kod olarak:

```csharp
public class OrderSaga : MassTransitStateMachine<OrderSagaState>
{
    // Durumlar
    public State AwaitingPayment { get; private set; } = null!;
    public State Completed       { get; private set; } = null!;
    public State Cancelled       { get; private set; } = null!;

    // Eventler
    public Event<OrderCreatedEvent>    OrderCreated      { get; private set; } = null!;
    public Event<PaymentCompletedEvent> PaymentCompleted { get; private set; } = null!;
    public Event<PaymentFailedEvent>   PaymentFailed     { get; private set; } = null!;

    public OrderSaga()
    {
        InstanceState(x => x.CurrentState);
        // bunu yazmasaydık: Saga hangi alanda durumunu sakladığını bilmez

        // Hangi field Saga'yı tanımlıyor? OrderId
        Event(() => OrderCreated,      x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentCompleted,  x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentFailed,     x => x.CorrelateById(ctx => ctx.Message.OrderId));
        // bunu yazmasaydık: Saga farklı siparişlerin event'lerini birbirine karıştırır

        // İlk event geldiğinde (Başlangıç → ÖdemeBekliyor)
        Initially(
            When(OrderCreated)
                .Then(ctx => {
                    ctx.Saga.OrderId       = ctx.Message.OrderId;
                    ctx.Saga.CustomerEmail = ctx.Message.CustomerEmail;
                    ctx.Saga.TotalAmount   = ctx.Message.TotalAmount;
                })
                .Publish(ctx => new PaymentRequestedEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.CustomerEmail))
                // bunu yazmasaydık: PaymentService'e sinyal gitmez, ödeme hiç başlamaz
                .TransitionTo(AwaitingPayment)
        );

        // ÖdemeBekliyor durumundayken event gelince
        During(AwaitingPayment,
            When(PaymentCompleted)
                .Then(ctx => ctx.Saga.PaidAt = DateTime.UtcNow)
                .Publish(ctx => new OrderCompletedEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.TotalAmount))
                .TransitionTo(Completed)
                .Finalize(),
                // Finalize: Saga tamamlandı, state DB'den silinebilir

            When(PaymentFailed)
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Publish(ctx => new OrderCancelledEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason ?? "Ödeme başarısız"))
                // Compensating transaction: siparişi iptal et, müşteriyi bildir
                // bunu yazmasaydık: ödeme başarısız ama sipariş "Pending" kalır → tutarsızlık
                .TransitionTo(Cancelled)
                .Finalize()
        );

        SetCompletedWhenFinalized();
        // bunu yazmasaydık: tamamlanan Saga kayıtları DB'de sonsuza birikir
    }
}
```

---

### Saga Dayanıklılığı — State Persistence

Saga durumunu nereye kaydeder?

```
InMemory (geliştirme):
  Hızlı, ama servis restart'ta tüm Saga durumları kaybolur
  → "AwaitingPayment"taki siparişler askıda kalır

EntityFramework (production):
  Saga durumu DB'ye yazılır
  Servis restart'ta kaldığı yerden devam eder
  → Güvenli
```

```csharp
x.AddSagaStateMachine<OrderSaga, OrderSagaState>()
    .InMemoryRepository();
    // Geliştirme için — gerçek projede:
    // .EntityFrameworkRepository(r => r.ExistingDbContext<OrderDbContext>())
```

---

### Choreography vs Orchestration — Hangisini Seçmeli?

| | Choreography | Orchestration |
|--|--|--|
| Akış kontrolü | Servisler birbirini tetikler | Saga yönetir |
| Durum görünürlüğü | Dağılmış | Merkezi |
| Debug | Zor (event zinciri) | Kolay (tek Saga log) |
| Bağımlılık | Düşük | Orta |
| Yeni adım eklemek | Birçok servise dokunmak | Saga'ya eklemek |
| Kullanım | 2-3 servis, basit akış | Karmaşık iş akışı |

**Bizim projemiz için:** Orchestration — sipariş akışı karmaşıklaşacak (Gün 129'da genişleyecek).

---

### Faz3 ile Karşılaştırma

```csharp
// Faz3 — tek process, tek transaction, rollback mümkün
public async Task<Order> CreateOrderAsync(CreateOrderDto dto)
{
    var order   = new Order(...);
    var payment = await _paymentService.ChargeAsync(dto.Amount); // aynı process
    var stock   = await _stockService.ReserveAsync(dto.ProductId); // aynı process

    await _db.SaveChangesAsync(); // hepsi tek transaction
    // Hata olursa: rollback → sanki hiç olmadı
}

// Faz5 — farklı servisler, Saga gerekli
// Her adım farklı DB → rollback yok → compensating transaction
// Saga durumu takip eder: "ödeme alındı mı? stok düşüldü mü?"
```

| | Faz3 (tek process) | Faz5 Choreography | Faz5 Orchestration |
|---|---|---|---|
| Transaction | ✅ ACID | ❌ Yok | ❌ Yok ama Saga takip eder |
| Geri alma | ✅ ROLLBACK | Compensating event | Compensating + merkezi yönetim |
| Durum nerede | Transaction | Her serviste | Saga'da |
| Debug | Stack trace | Distributed trace | Saga state log |
| Ölçeklenme | Tek process | Yüksek | Yüksek |

---

### 500 vs 50K Kullanıcı

| Karar | 500 kullanıcı/ay | 50K kullanıcı/ay |
|-------|-----------------|-----------------|
| Saga kullan | ⚠️ Basit choreography yeterli olabilir | ✅ Orchestration, durum takibi zorunlu |
| State persistence | InMemory yeterli | ✅ EF Core ile DB'ye yaz |
| Compensating transaction | ✅ Her zaman plan yap | ✅ Zorunlu, otomatik tetikle |
| Saga timeout | ❌ Nadiren gerekir | ✅ "30 dk içinde ödeme gelmezse iptal et" |
| 2-Phase Commit | ❌ Hiç | ❌ Hiç |

---

## Örnek Kod

Saga akışı:

```
OrderService (Saga)  →  PaymentService  →  OrderService (Saga)
      ↓                                           ↓
  PaymentRequested                    PaymentCompleted / PaymentFailed
  event yayınlandı                    event geldi → durum güncellendi
```

ECommerceApp'e eklenenler:

```
ECommerceApp/
├── src/Shared/Contracts/Events/
│   ├── PaymentRequestedEvent.cs     → Saga → PaymentService
│   ├── PaymentCompletedEvent.cs     → PaymentService → Saga
│   ├── PaymentFailedEvent.cs        → PaymentService → Saga
│   ├── OrderCompletedEvent.cs       → Saga → NotificationService
│   └── OrderCancelledEvent.cs       → Saga → NotificationService
├── src/OrderService/
│   ├── Saga/
│   │   ├── OrderSaga.cs             → State Machine tanımı
│   │   └── OrderSagaState.cs        → Saga durumu (DB'ye yazılır)
│   └── Program.cs                   → Saga kaydı
└── src/PaymentService/              → YENİ — ödeme işleme servisi
    ├── PaymentService.csproj
    ├── Program.cs
    └── Consumers/
        └── PaymentRequestedConsumer.cs
```

### Yeni Eventler — Contracts

```csharp
// PaymentRequestedEvent.cs
namespace ECommerce.Contracts.Events;

// Saga → PaymentService: "Bu siparişin ödemesini al"
public record PaymentRequestedEvent(
    Guid    OrderId,
    decimal Amount,
    string  CustomerEmail
);

// PaymentCompletedEvent.cs
// PaymentService → Saga: "Ödeme alındı"
public record PaymentCompletedEvent(
    Guid   OrderId,
    string TransactionId   // ödeme referans numarası
);

// PaymentFailedEvent.cs
// PaymentService → Saga: "Ödeme başarısız"
public record PaymentFailedEvent(
    Guid   OrderId,
    string Reason   // "Yetersiz bakiye", "Kart reddedildi" vb.
);

// OrderCompletedEvent.cs
// Saga → NotificationService: "Sipariş tamamlandı, email gönder"
public record OrderCompletedEvent(
    Guid    OrderId,
    string  CustomerEmail,
    decimal TotalAmount
);

// OrderCancelledEvent.cs
// Saga → NotificationService: "Sipariş iptal edildi, bildir"
public record OrderCancelledEvent(
    Guid   OrderId,
    string CustomerEmail,
    string Reason
);
```

### OrderService/Saga/OrderSagaState.cs

```csharp
using MassTransit;

namespace OrderService.Saga;

// Saga'nın hafızası — her sipariş için bir satır DB'ye yazılır
public class OrderSagaState : SagaStateMachineInstance
{
    public Guid   CorrelationId { get; set; }
    // CorrelationId = OrderId → aynı siparişin tüm event'leri bu Saga'ya yönlenir
    // bunu yazmasaydık: MassTransit Saga'yı tanımlayamaz

    public string CurrentState { get; set; } = string.Empty;
    // "Initial", "AwaitingPayment", "Completed", "Cancelled"
    // bunu yazmasaydık: durum makinesi nerede olduğunu bilemez

    public Guid    OrderId       { get; set; }
    public string  CustomerEmail { get; set; } = string.Empty;
    public decimal TotalAmount   { get; set; }

    public DateTime? PaidAt        { get; set; }
    public string?   FailureReason { get; set; }
}
```

### OrderService/Saga/OrderSaga.cs

```csharp
using ECommerce.Contracts.Events;
using MassTransit;

namespace OrderService.Saga;

public class OrderSaga : MassTransitStateMachine<OrderSagaState>
{
    // Durumlar — string olarak DB'ye yazılır
    public State AwaitingPayment { get; private set; } = null!;
    public State Completed       { get; private set; } = null!;
    public State Cancelled       { get; private set; } = null!;

    // Dinlenecek event'ler
    public Event<OrderCreatedEvent>     OrderCreated     { get; private set; } = null!;
    public Event<PaymentCompletedEvent> PaymentCompleted { get; private set; } = null!;
    public Event<PaymentFailedEvent>    PaymentFailed    { get; private set; } = null!;

    public OrderSaga()
    {
        // Saga durumunu hangi property'de sakla?
        InstanceState(x => x.CurrentState);
        // bunu yazmasaydık: durum kaydedilemez, her event yeni Saga başlatır

        // Correlation: hangi OrderId bu Saga'ya ait?
        Event(() => OrderCreated,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentCompleted,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentFailed,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));
        // bunu yazmasaydık: farklı siparişlerin event'leri aynı Saga'ya karışır

        // ── Başlangıç ──────────────────────────────────────────────────────
        Initially(
            When(OrderCreated)
                .Then(ctx =>
                {
                    // Saga bilgilerini kaydet (bunlara sonra ihtiyacımız olacak)
                    ctx.Saga.OrderId       = ctx.Message.OrderId;
                    ctx.Saga.CustomerEmail = ctx.Message.CustomerEmail;
                    ctx.Saga.TotalAmount   = ctx.Message.TotalAmount;

                    ctx.Instance.GetPayload<ILogger<OrderSaga>>()
                        ?.LogInformation("🚀 Saga başladı | OrderId={OrderId}", ctx.Saga.OrderId);
                })
                .Publish(ctx => new PaymentRequestedEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.CustomerEmail))
                // bunu yazmasaydık: PaymentService'e sinyal gitmez, ödeme hiç başlamaz
                .TransitionTo(AwaitingPayment)
        );

        // ── Ödeme Bekleniyor ───────────────────────────────────────────────
        During(AwaitingPayment,

            // Ödeme başarılı → siparişi tamamla
            When(PaymentCompleted)
                .Then(ctx =>
                {
                    ctx.Saga.PaidAt = DateTime.UtcNow;
                    ctx.Instance.GetPayload<ILogger<OrderSaga>>()
                        ?.LogInformation("✅ Ödeme alındı | OrderId={OrderId}", ctx.Saga.OrderId);
                })
                .Publish(ctx => new OrderCompletedEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.TotalAmount))
                // NotificationService bunu dinliyor → "Siparişiniz tamamlandı" emaili
                .TransitionTo(Completed)
                .Finalize(),

            // Ödeme başarısız → compensating transaction
            When(PaymentFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailureReason = ctx.Message.Reason;
                    ctx.Instance.GetPayload<ILogger<OrderSaga>>()
                        ?.LogWarning("❌ Ödeme başarısız | OrderId={OrderId} | Sebep={Reason}",
                            ctx.Saga.OrderId, ctx.Message.Reason);
                })
                .Publish(ctx => new OrderCancelledEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason ?? "Ödeme başarısız"))
                // Compensating transaction: siparişi iptal et, müşteriyi bildir
                // bunu yazmasaydık: sipariş "AwaitingPayment"ta sonsuza kalır
                .TransitionTo(Cancelled)
                .Finalize()
        );

        // Finalize olan Saga kayıtlarını DB'den temizle
        SetCompletedWhenFinalized();
        // bunu yazmasaydık: tamamlanan siparişler DB'de sonsuza birikir
    }
}
```

### OrderService/Program.cs (Saga eklendi)

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Saga;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<OrderDbContext>(opt =>
    opt.UseInMemoryDatabase("OrdersDb"));

builder.Services.AddMassTransit(x =>
{
    // Gün 128 — Saga State Machine kaydı
    x.AddSagaStateMachine<OrderSaga, OrderSagaState>()
        .InMemoryRepository();
    // InMemory: geliştirme için — servis restart'ta Saga durumları sıfırlanır
    // Gerçek projede: .EntityFrameworkRepository(r => r.ExistingDbContext<OrderDbContext>())
    // bunu yazmasaydık: Saga DI container'a kaydolmaz, event'ler işlenmez

    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UseInMemoryOutbox();
        o.UseBusOutbox();
        o.QueryDelay = TimeSpan.FromSeconds(1);
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ__Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ__Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ__Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
        // bunu yazmasaydık: Saga'nın dinleyeceği queue otomatik oluşturulmaz
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "OrderService" }));
app.Run();
```

### PaymentService/Consumers/PaymentRequestedConsumer.cs

```csharp
using ECommerce.Contracts.Events;
using MassTransit;

namespace PaymentService.Consumers;

// PaymentService: Saga'dan gelen "ödeme al" komutunu işler
public class PaymentRequestedConsumer : IConsumer<PaymentRequestedEvent>
{
    private readonly ILogger<PaymentRequestedConsumer> _logger;

    public PaymentRequestedConsumer(ILogger<PaymentRequestedConsumer> logger)
        => _logger = logger;

    public async Task Consume(ConsumeContext<PaymentRequestedEvent> context)
    {
        var request = context.Message;

        _logger.LogInformation(
            "💳 Ödeme işleniyor | OrderId={OrderId} | Tutar={Amount:C}",
            request.OrderId, request.Amount);

        // Gerçek projede: Stripe, İyzico vb. entegrasyonu
        // Demo: %80 başarı, %20 başarısız
        await Task.Delay(500); // ödeme işleme simülasyonu

        var isSuccessful = Random.Shared.NextDouble() > 0.2;

        if (isSuccessful)
        {
            var transactionId = $"TXN-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            await context.Publish(new PaymentCompletedEvent(request.OrderId, transactionId));
            // bunu yazmasaydık: Saga PaymentCompleted event'ini alamaz, AwaitingPayment'ta kalır

            _logger.LogInformation(
                "✅ Ödeme başarılı | OrderId={OrderId} | TxnId={TxnId}",
                request.OrderId, transactionId);
        }
        else
        {
            await context.Publish(new PaymentFailedEvent(request.OrderId, "Yetersiz bakiye"));
            // Compensating transaction tetiklendi: Saga siparişi iptal edecek
            // bunu yazmasaydık: başarısız ödeme Saga'ya bildirilmez → askıda sipariş

            _logger.LogWarning(
                "❌ Ödeme başarısız | OrderId={OrderId}", request.OrderId);
        }
    }
}
```

**Çalıştırmak için:**

```bash
cd ECommerceApp/docker
docker-compose up -d

# Sipariş oluştur — Saga başlar
curl -X POST http://localhost:5002/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "ali@mail.com",
    "customerName": "Ali Yılmaz",
    "productId": "11111111-1111-1111-1111-111111111111",
    "productName": "Clean Code",
    "quantity": 1,
    "unitPrice": 149.90
  }'

# Saga akışını loglardan izle
docker logs order-service -f       # Saga durumları: başladı → ödeme bekleniyor → tamamlandı
docker logs payment-service -f     # Ödeme işleme: başarılı mı, başarısız mı
docker logs notification-service -f # Email: tamamlandı mı, iptal mi

# Birkaç kez dene — %20 ihtimalle ödeme başarısız olacak
# Başarısız olunca compensating transaction çalışır:
# → OrderCancelledEvent yayınlanır
# → NotificationService "siparişiniz iptal edildi" emaili gönderir
```

---

## Kontrol Soruları

1. 2-Phase Commit ile Saga'nın temel farkı nedir? 2PC neden "lock" yaratır, Saga yaratmaz?
2. Choreography'de "sipariş nerede şu an?" sorusunu nasıl cevaplarsın? Orchestration'da?
3. Ödeme alındı, stok düşüldü, kargo hazırlanırken stok servisi çöktü. Saga hangi compensating işlemleri hangi sırayla çalıştırmalı?
4. `CorrelateById(ctx => ctx.Message.OrderId)` satırı olmasaydı ne olurdu? 100 sipariş aynı anda gelirse?
5. Saga durumu InMemory'de tutulurken OrderService çöktü ve restart attı. "AwaitingPayment" durumundaki siparişlere ne olur? EntityFramework persistence bunu nasıl çözer?
