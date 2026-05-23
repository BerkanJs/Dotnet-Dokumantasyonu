# Gün 129 — Hafta 18 Özet

## Haftanın Büyük Resmi

Bu hafta öğrendiklerimizi bir arada görelim. Hepsi aynı problemi farklı açılardan çözüyor:

```
Problem: Birden fazla servis arasında güvenilir iletişim nasıl sağlanır?

Gün 125 — RabbitMQ:    Servisler arası mesaj iletimi (kuyruk modeli)
Gün 126 — Kafka:       Servisler arası mesaj iletimi (log modeli, replay var)
Gün 127 — Outbox:      "Mesajı gönderdim ama DB'ye kaydedemedim" sorununu çözdü
Gün 128 — Saga:        "5 adımlı işlemde 3. adım patlarsa geri al" sorununu çözdü
```

---

## Bu Günün Senaryosu

Müşteri kitap siparişi verdi. Tam akış şöyle olmalı:

```
1. Sipariş oluştur   → OrderService
2. Ödeme al          → PaymentService
3. Kargo hazırla     → ShipmentService
4. Bildirim gönder   → NotificationService
```

Her adımda şunlar olabilir:

```
Adım 2 başarısız (ödeme reddedildi):
  → Siparişi iptal et
  → Müşteriyi bildir: "Ödemeniz alınamadı"

Adım 3 başarısız (depo boş):
  → Ödemeyi iade et  ← compensating transaction
  → Siparişi iptal et
  → Müşteriyi bildir: "Üzgünüz, stokta kalmadı"
```

---

## Güncellenmiş Saga Akış Diyagramı

```
POST /api/orders
      ↓
[OrderCreated]
      ↓
Saga başladı ──► PaymentRequested ──► PaymentService
                                            ↓
                              ┌─────────────┴─────────────┐
                         PaymentCompleted           PaymentFailed
                              ↓                          ↓
                    ShipmentRequested          [CANCELLED]
                              ↓               OrderCancelled event
                    ShipmentService            NotificationService
                              ↓               "Ödemeniz alınamadı"
               ┌──────────────┴──────────────┐
          ShipmentPrepared            ShipmentFailed
               ↓                          ↓
         [COMPLETED]              [CANCELLED]
         OrderCompleted           PaymentRefunded event  ← compensating
         NotificationService      OrderCancelled event
         "Siparişiniz yolda"      NotificationService
                                  "Stokta kalmadı, iade edildi"
```

---

## Haftanın Kavramları — Tek Tabloda

| Kavram | Hangi sorunu çözer | Ne zaman devreye girer |
|--------|-------------------|----------------------|
| **RabbitMQ** | Servisler direkt bağlı, biri çökünce diğeri de durur | Her zaman — servisler arası iletişim |
| **Kafka** | Mesaj okundu mu? Silinmeden önce tekrar okuyabilir miyim? | Event stream, replay, analitik |
| **Outbox** | DB kaydedildi mesaj gönderilemedi (ya da tam tersi) | Her Publish çağrısından önce |
| **Saga** | 3 servis tamamlandı 4. patladı, ilk 3'ü nasıl geri alırım? | Birden fazla servisi kapsayan iş akışı |
| **Compensating TX** | Distributed rollback yok, geri almayı nasıl yaparım? | Saga bir adım başarısız olduğunda |
| **Idempotency** | Aynı mesaj 2 kez geldi, 2 kez işleme nasıl önlerim? | Consumer tarafında her zaman |

---

## Hafta 18 Güncellenmiş Saga — Tam Akış

Gün 128'de Saga şuydu:
```
OrderCreated → AwaitingPayment → Completed / Cancelled
```

Bugün bunu genişletiyoruz:
```
OrderCreated → AwaitingPayment → AwaitingShipment → Completed / Cancelled
```

**Compensating transaction tablosu:**

| Adım | Başarılı → | Başarısız → | Compensating |
|------|-----------|------------|--------------|
| Sipariş oluştur | AwaitingPayment | — | — |
| Ödeme al | AwaitingShipment | Cancelled | Sipariş iptal et |
| Kargo hazırla | Completed | Cancelled | **Ödemeyi iade et** + Sipariş iptal et |

Dikkat: Kargo başarısız olursa **ödeme** zaten alınmıştı → onu da geri almak gerekiyor.

---

## Haftanın Mimarisi — ECommerceApp Son Hali

```
ECommerceApp/
├── src/
│   ├── Shared/Contracts/Events/
│   │   ├── OrderCreatedEvent.cs
│   │   ├── PaymentRequestedEvent.cs      ← Gün 128
│   │   ├── PaymentCompletedEvent.cs      ← Gün 128
│   │   ├── PaymentFailedEvent.cs         ← Gün 128
│   │   ├── ShipmentRequestedEvent.cs     ← Gün 129 (YENİ)
│   │   ├── ShipmentPreparedEvent.cs      ← Gün 129 (YENİ)
│   │   ├── ShipmentFailedEvent.cs        ← Gün 129 (YENİ)
│   │   ├── PaymentRefundedEvent.cs       ← Gün 129 (YENİ, compensating)
│   │   ├── OrderCompletedEvent.cs        ← Gün 128
│   │   └── OrderCancelledEvent.cs        ← Gün 128
│   │
│   ├── OrderService/
│   │   ├── Saga/
│   │   │   ├── OrderSaga.cs             ← Gün 129: AwaitingShipment durumu eklendi
│   │   │   └── OrderSagaState.cs        ← Gün 129: TransactionId, ShippedAt eklendi
│   │   └── ...
│   │
│   ├── PaymentService/                   ← Gün 128
│   ├── ShipmentService/                  ← Gün 129 (YENİ)
│   ├── NotificationService/              ← Gün 125: OrderCompleted/Cancelled da dinliyor
│   └── AnalyticsService/                 ← Gün 126
│
└── docker/docker-compose.yml             ← shipment-service eklendi
```

---

## Örnek Kod

### Yeni Eventler

```csharp
// ShipmentRequestedEvent.cs — Saga → ShipmentService
public record ShipmentRequestedEvent(
    Guid   OrderId,
    string CustomerName,
    string ProductName,
    int    Quantity
);

// ShipmentPreparedEvent.cs — ShipmentService → Saga
public record ShipmentPreparedEvent(
    Guid   OrderId,
    string TrackingNumber   // "TR-123456789"
);

// ShipmentFailedEvent.cs — ShipmentService → Saga
public record ShipmentFailedEvent(
    Guid   OrderId,
    string Reason   // "Stokta yok", "Depo kapalı"
);

// PaymentRefundedEvent.cs — Saga → NotificationService (compensating)
// Kargo başarısız olunca ödeme iade edildiğini bildirmek için
public record PaymentRefundedEvent(
    Guid    OrderId,
    string  CustomerEmail,
    decimal Amount,
    string  Reason
);
```

### Güncellenmiş OrderSagaState.cs

```csharp
using MassTransit;

namespace OrderService.Saga;

public class OrderSagaState : SagaStateMachineInstance
{
    public Guid   CorrelationId { get; set; }
    public string CurrentState  { get; set; } = string.Empty;

    // Sipariş bilgileri
    public Guid    OrderId       { get; set; }
    public string  CustomerEmail { get; set; } = string.Empty;
    public string  CustomerName  { get; set; } = string.Empty;
    public string  ProductName   { get; set; } = string.Empty;
    public int     Quantity      { get; set; }
    public decimal TotalAmount   { get; set; }

    // Ödeme bilgileri
    public DateTime? PaidAt         { get; set; }
    public string?   TransactionId  { get; set; }
    // bunu yazmasaydık: kargo başarısız olunca hangi işlemi iade edeceğimizi bilemeyiz

    // Kargo bilgileri
    public DateTime? ShippedAt      { get; set; }
    public string?   TrackingNumber { get; set; }

    // İptal bilgileri
    public string? FailureReason { get; set; }
}
```

### Güncellenmiş OrderSaga.cs

```csharp
using ECommerce.Contracts.Events;
using MassTransit;

namespace OrderService.Saga;

public class OrderSaga : MassTransitStateMachine<OrderSagaState>
{
    // ── Durumlar ───────────────────────────────────────────────────────────
    public State AwaitingPayment  { get; private set; } = null!;
    public State AwaitingShipment { get; private set; } = null!;  // YENİ
    public State Completed        { get; private set; } = null!;
    public State Cancelled        { get; private set; } = null!;

    // ── Eventler ──────────────────────────────────────────────────────────
    public Event<OrderCreatedEvent>     OrderCreated     { get; private set; } = null!;
    public Event<PaymentCompletedEvent> PaymentCompleted { get; private set; } = null!;
    public Event<PaymentFailedEvent>    PaymentFailed    { get; private set; } = null!;
    public Event<ShipmentPreparedEvent> ShipmentPrepared { get; private set; } = null!;  // YENİ
    public Event<ShipmentFailedEvent>   ShipmentFailed   { get; private set; } = null!;  // YENİ

    public OrderSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreated,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentCompleted,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentFailed,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => ShipmentPrepared,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => ShipmentFailed,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));

        // ── 1. Sipariş oluştu ─────────────────────────────────────────────
        Initially(
            When(OrderCreated)
                .Then(ctx =>
                {
                    ctx.Saga.OrderId       = ctx.Message.OrderId;
                    ctx.Saga.CustomerEmail = ctx.Message.CustomerEmail;
                    ctx.Saga.CustomerName  = ctx.Message.CustomerName;
                    ctx.Saga.ProductName   = ctx.Message.ProductName;
                    ctx.Saga.Quantity      = ctx.Message.Quantity;
                    ctx.Saga.TotalAmount   = ctx.Message.TotalAmount;
                })
                .Publish(ctx => new PaymentRequestedEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.CustomerEmail))
                .TransitionTo(AwaitingPayment)
        );

        // ── 2. Ödeme bekleniyor ───────────────────────────────────────────
        During(AwaitingPayment,

            When(PaymentCompleted)
                .Then(ctx =>
                {
                    ctx.Saga.PaidAt        = DateTime.UtcNow;
                    ctx.Saga.TransactionId = ctx.Message.TransactionId;
                })
                .Publish(ctx => new ShipmentRequestedEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerName,
                    ctx.Saga.ProductName,
                    ctx.Saga.Quantity))
                // Ödeme tamam → şimdi kargo hazırlansın
                // bunu yazmasaydık: ödeme alındı ama kargo başlamaz
                .TransitionTo(AwaitingShipment),

            When(PaymentFailed)
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Publish(ctx => new OrderCancelledEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason ?? "Ödeme başarısız"))
                .TransitionTo(Cancelled)
                .Finalize()
        );

        // ── 3. Kargo bekleniyor ───────────────────────────────────────────
        During(AwaitingShipment,

            When(ShipmentPrepared)
                .Then(ctx =>
                {
                    ctx.Saga.ShippedAt      = DateTime.UtcNow;
                    ctx.Saga.TrackingNumber = ctx.Message.TrackingNumber;
                })
                .Publish(ctx => new OrderCompletedEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.TotalAmount))
                // Her şey tamam → müşteriyi bildir
                .TransitionTo(Completed)
                .Finalize(),

            When(ShipmentFailed)
                // Compensating transaction: ödeme iade edilmeli!
                // Çünkü ödeme zaten alınmıştı (AwaitingShipment'a geldiysek)
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Publish(ctx => new PaymentRefundedEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.FailureReason ?? "Stokta yok"))
                // bunu yazmasaydık: kargo başarısız ama ödeme iade edilmez → müşteri zarara girer
                .Publish(ctx => new OrderCancelledEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail,
                    $"Kargo hazırlanamadı: {ctx.Saga.FailureReason}"))
                .TransitionTo(Cancelled)
                .Finalize()
        );

        SetCompletedWhenFinalized();
    }
}
```

### ShipmentService/Consumers/ShipmentRequestedConsumer.cs

```csharp
using ECommerce.Contracts.Events;
using MassTransit;

namespace ShipmentService.Consumers;

public class ShipmentRequestedConsumer : IConsumer<ShipmentRequestedEvent>
{
    private readonly ILogger<ShipmentRequestedConsumer> _logger;

    public ShipmentRequestedConsumer(ILogger<ShipmentRequestedConsumer> logger)
        => _logger = logger;

    public async Task Consume(ConsumeContext<ShipmentRequestedEvent> context)
    {
        var req = context.Message;

        _logger.LogInformation(
            "📦 Kargo hazırlanıyor | OrderId={OrderId} | Ürün={Product} x{Qty}",
            req.OrderId, req.ProductName, req.Quantity);

        // Gerçek projede: stok kontrolü, depo sistemi entegrasyonu
        await Task.Delay(800); // kargo hazırlama simülasyonu

        // %90 başarı, %10 başarısız — "stok tükendi" senaryosu
        var isSuccessful = Random.Shared.NextDouble() > 0.1;

        if (isSuccessful)
        {
            var trackingNumber = $"TR-{Random.Shared.Next(100000000, 999999999)}";

            await context.Publish(new ShipmentPreparedEvent(req.OrderId, trackingNumber));
            // bunu yazmasaydık: Saga ShipmentPrepared event'ini alamaz → AwaitingShipment'ta kalır

            _logger.LogInformation(
                "✅ Kargo hazır | OrderId={OrderId} | Takip={Tracking}",
                req.OrderId, trackingNumber);
        }
        else
        {
            await context.Publish(new ShipmentFailedEvent(req.OrderId, "Stokta kalmadı"));
            // Compensating transaction tetiklenir: Saga ödemeyi iade edecek
            // bunu yazmasaydık: kargo başarısız ama Saga bunu bilmez → askıda sipariş

            _logger.LogWarning(
                "❌ Kargo başarısız | OrderId={OrderId} | Sebep=Stokta kalmadı",
                req.OrderId);
        }
    }
}
```

### Güncellenmiş NotificationService — Yeni Event'leri Dinliyor

```csharp
// OrderCreatedConsumer var (Gün 125)
// OrderCompletedConsumer — YENİ
public class OrderCompletedConsumer : IConsumer<OrderCompletedEvent>
{
    public Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        var e = context.Message;
        // Gerçek projede: SMTP / SendGrid ile email gönder
        Console.WriteLine($"📧 Email: Siparişiniz yolda! | {e.CustomerEmail} | {e.TotalAmount:C}");
        return Task.CompletedTask;
    }
}

// OrderCancelledConsumer — YENİ
public class OrderCancelledConsumer : IConsumer<OrderCancelledEvent>
{
    public Task Consume(ConsumeContext<OrderCancelledEvent> context)
    {
        var e = context.Message;
        Console.WriteLine($"📧 Email: Siparişiniz iptal edildi. Sebep: {e.Reason} | {e.CustomerEmail}");
        return Task.CompletedTask;
    }
}

// PaymentRefundedConsumer — YENİ (compensating transaction bildirimi)
public class PaymentRefundedConsumer : IConsumer<PaymentRefundedEvent>
{
    public Task Consume(ConsumeContext<PaymentRefundedEvent> context)
    {
        var e = context.Message;
        Console.WriteLine($"📧 Email: {e.Amount:C} iade edildi. Sebep: {e.Reason} | {e.CustomerEmail}");
        return Task.CompletedTask;
        // bunu yazmasaydık: müşteri ödeme iade edildiğinden haberdar olmaz
    }
}
```

**Çalıştırmak için:**

```bash
cd ECommerceApp/docker
docker-compose up -d

# Birkaç sipariş oluştur ve logları izle
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

# Tüm servislerin loglarını aynı anda izle
docker logs order-service    -f &
docker logs payment-service  -f &
docker logs shipment-service -f &
docker logs notification-service -f

# Birkaç kez dene — 3 farklı senaryo göreceksin:
# 1. Başarılı: Ödeme ✅ → Kargo ✅ → "Siparişiniz yolda" emaili
# 2. Ödeme başarısız (%20): Ödeme ❌ → "Ödemeniz alınamadı" emaili
# 3. Kargo başarısız (%10): Ödeme ✅ → Kargo ❌ → "İade edildi" + "İptal" emaili
```

---

## Hafta 18 — Kavramların Birbirine Bağlantısı

```
Sipariş geldi
    ↓
[Outbox] — "DB + event gönderme atomik olsun"
    ↓
[RabbitMQ] — "OrderCreated event'ini Saga'ya ilet"
    ↓
[Saga] — "Ödeme, kargo, bildirim akışını yönet"
    ↓
[RabbitMQ] — "PaymentRequested event'ini PaymentService'e ilet"
    ↓
[Kafka] — "OrderCreated event'ini AnalyticsService'e ilet (farklı group)"
    ↓
[Idempotency] — "Aynı mesaj 2 kez gelse de 1 kez işle"
```

Bunların hepsi birbirini tamamlıyor. Birini çıkardığında:
- Outbox yok → crash'te sipariş-event tutarsızlığı
- Saga yok → kargo patladığında ödeme iade edilmez
- Idempotency yok → at-least-once garantisi mükerrer işlemlere yol açar

---

## Hafta 18 Kontrol Soruları

1. Outbox olmadan Saga kullanabilir misin? Outbox + Saga birlikte ne sağlar?
2. Choreography Saga ile bu senaryoyu (sipariş → ödeme → kargo → bildirim) uygulasan nasıl olurdu? Kaç event'e ihtiyaç duyardın?
3. ShipmentFailed geldi, Saga PaymentRefunded yayınladı. PaymentService bu event'i dinleyip parayı iade etmeli. Bu hangi gün öğrendiğimiz hangi pattern ile güvenli yapılır?
4. Saga InMemory'de tutulurken 50 sipariş "AwaitingShipment" durumundayken servis çöktü. Restart sonrası ne olur? Nasıl kurtarırsın?
5. Analytics için OrderCreated event'ini hem RabbitMQ (Saga için) hem Kafka (AnalyticsService için) üzerinden göndermek istiyorsun. Bunu nasıl yaparsın?
