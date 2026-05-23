# Gün 125 — RabbitMQ: Kavramsal Derinlik

## Teorik

### RabbitMQ Nedir?

**Analoji: Posta ofisi**

Mektup göndermek istiyorsun. Doğrudan alıcının kapısına gitmen gerekmez. Postaneye bırakırsın. Postane mektubu saklıyor, alıcı hazır olduğunda teslim ediyor. Alıcı evde değilse posta kutusu dolduğunda okur.

RabbitMQ da aynı: OrderService mesajı RabbitMQ'ya bırakır. NotificationService hazır olduğunda alır. OrderService, NotificationService'in ayakta olup olmadığını bilmek zorunda değil.

```
OrderService ──► [RabbitMQ] ──► NotificationService
   mesajı         saklar,          mesajı hazır
   bırakır        iletir           olunca alır
```

---

### AMQP Protokolü

RabbitMQ, **AMQP** (Advanced Message Queuing Protocol) protokolünü kullanır. HTTP gibi bir iletişim protokolü, ama mesajlaşma için özel tasarlanmış.

```
HTTP:   istek-cevap    → senkron, bağlantı kapanır
AMQP:   mesaj akışı    → async, bağlantı açık kalır, mesajlar birikir
```

Port 5672 = AMQP (servisler bağlanır)
Port 15672 = Management UI (tarayıcıdan izlenir)

---

### RabbitMQ'nun 3 Temel Kavramı

**1. Exchange — Posta Dağıtım Merkezi**

Mesaj doğrudan kuyruğa gitmez. Önce Exchange'e gider. Exchange mesajı hangi kuyruğa göndereceğine karar verir.

**2. Queue — Posta Kutusu**

Mesajların beklendiği yer. Consumer mesajı buradan alır.

**3. Binding — Yönlendirme Kuralı**

Exchange ile Queue arasındaki bağlantı kuralı. "Bu mesajı şu kuyruğa gönder" talimatı.

```
Producer ──► [Exchange] ──binding──► [Queue] ──► Consumer
             (dağıtım               (posta       (alan)
              merkezi)               kutusu)
```

---

### Exchange Türleri

**Direct Exchange — Adrese doğrudan teslim**

Routing key tam eşleşmesi. "Bu mesajı sadece `order.created` kuyruğuna gönder."

```
Exchange ──── routing key: "order.created" ────► Queue: order.created
         ──── routing key: "order.cancelled" ──► Queue: order.cancelled
```

Kullanım: Belirli bir servise komut göndermek. `SendEmailCommand` gibi.

**Topic Exchange — Konu filtresi**

Wildcard ile eşleşme. `*` = tek kelime, `#` = birden fazla kelime.

```
Exchange ──── "order.*"     eşleşir: "order.created", "order.cancelled"
         ──── "order.#"     eşleşir: "order.created.v2", "order.x.y.z"
         ──── "*.created"   eşleşir: "order.created", "payment.created"
```

Kullanım: Birden fazla servise filtreli dağıtım.

**Fanout Exchange — Herkese dağıt**

Routing key'e bakmaz. Bağlı tüm kuyruklara kopyasını gönderir.

```
Exchange ──► Queue: notification-service
        ──► Queue: analytics-service
        ──► Queue: audit-service
(aynı mesajın 3 kopyası)
```

Kullanım: `OrderCreatedEvent` — kimin dinlediği önemli değil, herkese.

**Headers Exchange**

Routing key yerine mesaj header'larına göre yönlendirir. Nadiren kullanılır.

---

### Message Durability — Mesaj Dayanıklılığı

**Analoji: Kağıt not vs SMS**

Kağıda yazdığın notu masa üzerine bıraktın. Işık gidince gece kayboldu. SMS ise telefon şarjı bitse bile sunucuda saklanır.

RabbitMQ'da da iki mod var:

```
Transient (geçici):  RabbitMQ yeniden başlayınca mesaj KAYBOLUR
Durable  (kalıcı):   RabbitMQ yeniden başlasa da mesaj SAKLANIR
```

İki şeyi birden durable yapman gerekiyor:

```csharp
// 1. Queue durable olmalı
e.Durable = true;  // bunu yazmasaydık: restart'ta kuyruk silinir

// 2. Mesaj persistent olmalı — MassTransit bunu otomatik yapar
// Manuel RabbitMQ'da: properties.DeliveryMode = 2 (persistent)
```

**Ne zaman durable gerekmez?** Gerçek zamanlı anlık veri: anlık fiyat akışı, canlı konum verisi. Kaybolsa da olur, yeni veri zaten geliyor.

---

### Dead Letter Queue (DLQ) — Başarısız Mesajlar Nereye?

**Analoji: İade kutusu**

Kargo teslim edilemedi (adres yanlış, kapı kapalı). Geri iade kutusuna atılıyor. Oradan tekrar deneyebilir veya inceleyebilirsin.

Consumer mesajı işleyemezse (exception fırlattıysa):

```
Normal akış:
[Queue] ──► Consumer ──► Consume() başarılı ──► Ack ──► mesaj silindi ✅

Hatalı akış (DLQ olmadan):
[Queue] ──► Consumer ──► Exception! ──► Nack ──► mesaj tekrar kuyruğa ──► sonsuz döngü ❌

Hatalı akış (DLQ ile):
[Queue] ──► Consumer ──► Exception! ──► retry 1,2,3 ──► hâlâ hata ──► [DLQ] ──► incelenir ✅
```

```csharp
// NotificationService/Program.cs — retry + DLQ yapılandırması
cfg.UseMessageRetry(r =>
{
    r.Intervals(
        TimeSpan.FromSeconds(5),   // 1. deneme başarısız → 5 sn bekle
        TimeSpan.FromSeconds(15),  // 2. deneme başarısız → 15 sn bekle
        TimeSpan.FromSeconds(30)   // 3. deneme başarısız → 30 sn bekle
    );                             // 3. de başarısız → DLQ'ya at
});

e.ConfigureDeadLetterQueueDeadLetterTransport();
// bunu yazmasaydık: 3 retry sonrası mesaj kaybolur, neden hata verdiği bilinemez
```

---

### Consumer Acknowledgment — Manuel Onay

**Analoji: Kargo teslim imzası**

Kargo sana teslim edildi. İmzaladın → teslim tamamlandı, sistemden silindi. İmzalamadan önce kutu düşüp kırıldı → imzalamadın → kargocuya iade, tekrar gönderildi.

RabbitMQ'da da aynı mantık:

```
Otomatik Ack (Auto-Ack):
  Mesaj consumer'a ulaştı mı? → hemen sil.
  Consumer process'i çöktü, mesaj işlenmedi → mesaj kayboldu ❌

Manuel Ack:
  Mesaj consumer'a ulaştı → işlendi mi bekle
  Consume() başarıyla bitti → Ack gönder → mesaj silindi ✅
  Consume() exception fırlattı → Nack gönder → mesaj tekrar kuyruğa ✅
```

MassTransit varsayılan olarak manuel Ack kullanır:
- `Consume()` metodu exception fırlatmadan biterse → otomatik **Ack**
- `Consume()` exception fırlatırsa → **Nack** + retry

```csharp
// MassTransit'te manuel Ack — framework halleder
public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
{
    await SendEmailAsync(context.Message); // başarılı → Ack
    // Exception fırlatılırsa → Nack → retry → DLQ
}
// bunu yazmasaydık: mesaj işlenmeden önce silinir, email gönderilmeden kayıp olur
```

---

### Prefetch Count — Kaç Mesaj Aynı Anda Alınsın?

Consumer kuyruğa "bana şu kadar mesaj ver" diyebilir.

```
PrefetchCount = 1:
  Consumer mesaj 1'i alır → işler → Ack → mesaj 2'yi alır
  Sıralı işleme, düşük throughput ama sıra garantisi

PrefetchCount = 10:
  Consumer aynı anda 10 mesaj alır → paralel işler
  Yüksek throughput ama sıra garantisi yok

PrefetchCount = 0:
  Sınırsız — consumer tüm kuyruğu çeker → bellek patlar ❌
```

```csharp
e.PrefetchCount = 5; // aynı anda 5 mesaj al, işle, sıradakine geç
// bunu yazmasaydık: varsayılan 16 — ani yük altında servis belleği tükenir
```

**Ne seçmeli?** Email gönderimi gibi I/O ağırlıklı → 5-10 arası. CPU ağırlıklı → 1-2.

---

### MassTransit ile RabbitMQ Entegrasyonu

MassTransit, RabbitMQ'nun üzerinde oturan bir soyutlama katmanı. Exchange türleri, binding, serialization gibi low-level detayları gizler.

```
Sen:        await _publishEndpoint.Publish(new OrderCreatedEvent(...));
MassTransit: exchange oluşturur, routing key belirler, mesajı serialize eder, RabbitMQ'ya gönderir
RabbitMQ:   exchange'den binding'e göre queue'ya iletir
MassTransit: queue'dan alır, deserialize eder, doğru Consumer'ı çağırır
```

**MassTransit'in otomatik yaptıkları:**
- Her `IConsumer<T>` için otomatik kuyruk oluşturur
- Exchange-queue binding otomatik kurulur
- Serialization: mesajı JSON'a çevirir
- Manuel Ack: Consume() başarılıysa Ack gönderir
- Retry pipeline: exception'da tekrar dener

---

### Faz3 ile Karşılaştırma

Faz3'te domain event dispatch aynı process içindeydi:

```csharp
// Faz3 — aynı process, MediatR ile in-process
await _mediator.Publish(new OrderCreatedDomainEvent(order));
// Aynı thread, aynı transaction, hata varsa rollback

// Faz5 — farklı servis, RabbitMQ üzerinden
await _publishEndpoint.Publish(new OrderCreatedEvent(...));
// Farklı process, farklı DB, hata varsa retry → DLQ
```

| | Faz3 (in-process) | Faz5 (RabbitMQ) |
|---|---|---|
| Hız | Microsecond | Millisecond |
| Güvenilirlik | Transaction ile | Retry + DLQ ile |
| Ölçeklenebilirlik | Tek process | Bağımsız servisler |
| Debug | Stack trace | Distributed trace |
| Hata | Rollback | Compensating transaction |

---

### 500 vs 50K Kullanıcı

| Karar | 500 kullanıcı/ay | 50K kullanıcı/ay |
|-------|-----------------|-----------------|
| RabbitMQ | ❌ In-process event yeter | ✅ Async servisler arası iletişim için |
| Durable queue | ✅ Her zaman açık bırak | ✅ Her zaman zorunlu |
| DLQ | ✅ Küçük projede de şart | ✅ Zorunlu |
| Prefetch 1 | ✅ Sıralı işleme yeterli | ⚠️ Throughput için artır |
| Fanout exchange | ❌ Overkill | ✅ Çok consumer varsa |
| Topic exchange | ❌ Overkill | ✅ Mesaj filtreleme gerekince |
| Manuel Ack | ✅ Her zaman | ✅ Her zaman |

---

## Örnek Kod

```
ECommerceApp/
├── docker/docker-compose.yml                         → RabbitMQ + servisler
├── src/Shared/Contracts/
│   └── Events/OrderCreatedEvent.cs                   → paylaşılan event sözleşmesi
├── src/OrderService/
│   ├── Program.cs                                    → MassTransit publisher config
│   └── Controllers/OrderController.cs               → event yayınlar
└── src/NotificationService/
    ├── Program.cs                                    → consumer config, prefetch, DLQ
    └── Consumers/OrderCreatedConsumer.cs             → mesajı işler, email gönderir
```

**Çalıştırmak için:**
```bash
cd ECommerceApp/docker
docker-compose up -d

# RabbitMQ Management UI
# http://localhost:15672 → guest/guest
# Exchange ve Queue'ları buradan izleyebilirsin

# Sipariş oluştur
curl -X POST http://localhost:5002/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "ali@mail.com",
    "customerName": "Ali Yılmaz",
    "bookId": "11111111-1111-1111-1111-111111111111",
    "bookTitle": "Clean Code",
    "quantity": 2,
    "unitPrice": 149.90
  }'

# NotificationService loglarında email gönderildiğini görürsün
docker logs notification-service -f
```

---

## Kontrol Soruları

1. Fanout ve Topic exchange arasındaki fark ne? `OrderCreatedEvent` için hangisi daha uygun, neden?
2. Consumer process'i çöktü, mesaj henüz işlenmemişti. Durable queue + Manuel Ack olmadan ne olurdu?
3. PrefetchCount = 1 ile = 10 arasında ne zaman hangisini seçersin? Email gönderimi için ne seçerdin?
4. DLQ'daki mesajı nasıl işlersin? Oraya düşen mesajı nasıl tekrar denersin?
5. MassTransit olmasaydı RabbitMQ'yu direkt kullanmak ne kadar daha karmaşık olurdu? Neyi elle yapman gerekirdi?
