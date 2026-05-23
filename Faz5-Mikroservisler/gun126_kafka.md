# Gün 126 — Apache Kafka: Log-Based Messaging

## Teorik

### Kafka Nedir?

**Analoji: Gazete baskısı vs posta kutusu**

RabbitMQ bir posta kutusu gibi: mesaj gelir, alıcı okur, kutudan silinir. Kutuya bir kez konur.

Kafka ise gazete baskısı gibi: gazete bir kez basılır, arşive konur. İstediğin kişi arşivden istediği zamanda okur. Okunduktan sonra arşivden silinmez. Ocak ayı gazetesini Mart'ta da okuyabilirsin.

```
RabbitMQ — Queue (Kuyruk modeli):
  Producer ──► [Queue] ──► Consumer
               mesaj geldi     aldı, silindi

Kafka — Log (Günlük modeli):
  Producer ──► [Topic/Log] ──► Consumer A (offset 7'den okuyor)
                          ──► Consumer B (offset 3'ten okuyor)
                          ──► Consumer C (offset 0'dan tekrar okuyor)
               mesaj arşivde kalır, birden fazla consumer bağımsız okur
```

---

### Kafka vs RabbitMQ — Temel Fark

| Özellik | RabbitMQ | Kafka |
|---------|----------|-------|
| Model | Queue (kuyruk) | Log (günlük) |
| Mesaj okunduktan sonra | Silinir | Arşivde kalır (retention) |
| Birden fazla consumer | Yük paylaşımı yapar | Her biri bağımsız okur |
| Sıra garantisi | Kuyruk seviyesinde | Partition seviyesinde |
| Hız (throughput) | Yüksek | Çok yüksek (milyon/sn) |
| Kullanım amacı | Komut, görev kuyruğu | Event stream, log analiz |
| Gecikme | Düşük (ms) | Biraz daha yüksek |
| Replay (geçmişi tekrar okuma) | ❌ Yok | ✅ Var |

**Özet kural:**
- "Şu işi yap" → RabbitMQ (`SendEmailCommand`)
- "Şu olay gerçekleşti, kim isterse okusun, geçmişe de bakılabilsin" → Kafka (`OrderCreatedEvent`)

---

### Topic, Partition, Consumer Group

**Topic — Konu başlığı**

Mesajlar topic'lere yazılır. Her topic bir log dosyası gibidir.

```
Topic: order-events
  Mesaj 0: OrderCreated  { orderId: 1 }
  Mesaj 1: OrderShipped  { orderId: 1 }
  Mesaj 2: OrderCreated  { orderId: 2 }
  Mesaj 3: OrderCancelled { orderId: 1 }
  ... (silinmez, retention süresine kadar saklanır)
```

**Partition — Bölümleme**

Topic'ler partition'lara bölünür. Paralel yazma ve okuma için.

```
Topic: order-events (3 partition)
  Partition 0: [mesaj 0] [mesaj 3] [mesaj 6]
  Partition 1: [mesaj 1] [mesaj 4] [mesaj 7]
  Partition 2: [mesaj 2] [mesaj 5] [mesaj 8]
```

Mesaj hangi partition'a gider? **Partition key** ile belirlenir.
```csharp
// orderId'yi key olarak kullan
// Aynı orderId hep aynı partition'a gider
// → aynı siparişin eventleri sıralı işlenir
await producer.ProduceAsync("order-events", new Message<string, string>
{
    Key = order.Id.ToString(),  // bunu yazmasaydık: farklı partition'lara dağılır, sıra garantisi kaybolur
    Value = JsonSerializer.Serialize(orderEvent)
});
```

**Consumer Group — Okuyucu grubu**

Aynı consumer group içindeki consumer'lar yükü paylaşır.
Farklı consumer group'lar ise aynı mesajı bağımsız okur.

```
Topic: order-events (3 partition)

Consumer Group: notification-service
  Consumer A ──► Partition 0
  Consumer B ──► Partition 1
  Consumer C ──► Partition 2
  (3 partition, 3 consumer → her biri 1 partition okur)

Consumer Group: analytics-service
  Consumer X ──► Partition 0, 1, 2
  (aynı mesajları bağımsız, baştan okur)
```

**Kural:** Bir partition'ı aynı group içinde sadece 1 consumer okuyabilir.
Consumer sayısı > Partition sayısı olursa fazla consumer'lar bekler (boşta kalır).

---

### Offset Management — Consumer Nerede Kaldı?

**Analoji: Kitap imi**

Kitabı okuyorsun, 142. sayfaya imi koydun. Yarın kaldığın yerden devam edersin. Başkası aynı kitabı okurken kendi imini kullanır.

Kafka'da her consumer, her partition için **offset** (kaçıncı mesajı okuduğu) kaydeder.

```
Partition 0: [msg 0] [msg 1] [msg 2] [msg 3] [msg 4]
                                       ↑
                              Consumer A'nın offset'i = 3
                              (0,1,2'yi okudu, 3'ü okuyacak)
```

```csharp
// Confluent .NET — offset commit
consumer.Commit(consumeResult);
// bunu yazmasaydık: servis yeniden başlayınca son commit'ten değil baştan okur → duplicate işleme

// Auto-commit (varsayılan, dikkatli kullan)
var config = new ConsumerConfig
{
    EnableAutoCommit = true,          // her 5 sn'de bir otomatik commit
    AutoCommitIntervalMs = 5000,      // bunu yazmasaydık: process çökerse son 5 sn'deki mesajlar kaybolur
    AutoOffsetReset = AutoOffsetReset.Earliest  // ilk kez bağlanınca baştan oku
    // AutoOffsetReset.Latest: sadece yeni mesajları oku
};
```

**Offset sıfırlama:** Consumer group silinirse veya yeni bir group başlarsa offset'i sıfırlayabilirsin. Bu sayede geçmiş mesajları **replay** edebilirsin — RabbitMQ'da bu mümkün değil.

---

### Retention Policy — Mesajlar Silinmez, Ama...

Kafka mesajları silmez ama sonsuza kadar saklamaz. İki retention modu:

```
Time-based retention:
  log.retention.hours = 168  # 7 gün — bunu yazmasaydık: disk dolar

Size-based retention:
  log.retention.bytes = 1073741824  # 1 GB per partition

Log compaction (özel mod):
  Her key için en son değeri tutar, eskisini siler
  Kullanım: kullanıcı profili, ürün fiyatı gibi "en güncel değer ne?" soruları
```

```yaml
# docker-compose.yml
KAFKA_LOG_RETENTION_HOURS: 168       # 7 gün tut, sonra sil
KAFKA_LOG_RETENTION_BYTES: -1        # boyut sınırı yok (sadece zaman bazlı)
KAFKA_LOG_SEGMENT_BYTES: 1073741824  # 1 GB dolunca yeni segment aç
```

---

### Event Sourcing için Kafka Neden Uygun?

**Event sourcing:** Uygulamanın durumunu (state) kaydetmek yerine, durumu değiştiren event'leri kaydedersin.

```
Normal DB:
  orders tablosu: { id: 1, status: "Shipped", updatedAt: ... }
  (son durum var, geçmiş yok)

Event Sourcing + Kafka:
  order-events topic:
    msg 0: OrderCreated  { orderId: 1, amount: 299 }
    msg 1: OrderPaid     { orderId: 1 }
    msg 2: OrderShipped  { orderId: 1, trackingNo: "TR123" }
    msg 3: OrderDelivered { orderId: 1 }
  (tüm geçmiş var, herhangi bir ana dönebilirsin)
```

**Kafka'nın event sourcing için avantajları:**
- Retention: geçmiş silinmez
- Replay: baştan okuyup farklı bir view oluşturabilirsin
- Audit log: kim ne zaman ne yaptı → her zaman mevcut
- Time travel: "Ocak ayı sonu itibarıyla siparişlerin durumu neydi?" sorusuna cevap verebilirsin

---

### Exactly-Once Semantics

Mesaj iletiminde 3 seviye garanti:

```
At-most-once:  Mesaj kaybolabilir ama tekrar gelmez
               (fire-and-forget, en hızlı)

At-least-once: Mesaj kesinlikle gelir ama tekrar da gelebilir
               (varsayılan, çoğu sistemde kullanılır)

Exactly-once:  Mesaj tam olarak 1 kez gelir
               (en güvenli ama en yavaş)
```

**Exactly-once için iki şart:**

```csharp
// 1. Transactional Producer — "ya hepsi ya hiç" yazar
var producerConfig = new ProducerConfig
{
    TransactionalId = "order-service-producer-1",  // bunu yazmasaydık: transaction kullanamayız
    EnableIdempotence = true,  // aynı mesaj iki kez gönderilse bile Kafka bir kez yazar
};

using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
producer.InitTransactions(TimeSpan.FromSeconds(30));
producer.BeginTransaction();

await producer.ProduceAsync("order-events", message);

producer.CommitTransaction();  // bunu yazmasaydık: crash'te mesaj yarım gidebilir

// 2. Idempotent Consumer — aynı mesaj 2 kez gelse de sadece 1 kez işle
public async Task ConsumeAsync(OrderCreatedEvent @event)
{
    // İdempotency key kontrolü: bu mesajı daha önce işledik mi?
    if (await _processedEvents.ContainsAsync(@event.EventId))
        return;  // bunu yazmasaydık: duplicate işleme → mükerrer email, çift sipariş

    await _orderRepository.CreateAsync(...);
    await _processedEvents.AddAsync(@event.EventId);
}
```

---

### Confluent .NET Client vs MassTransit Kafka Transport

**Confluent .NET Client — Low-level, tam kontrol:**

```csharp
// Doğrudan Kafka ile konuşur
// Producer
using var producer = new ProducerBuilder<string, string>(config).Build();
await producer.ProduceAsync("order-events", new Message<string, string>
{
    Key = orderId,
    Value = JsonSerializer.Serialize(orderEvent)
});

// Consumer
using var consumer = new ConsumerBuilder<string, string>(config).Build();
consumer.Subscribe("order-events");
while (true)
{
    var result = consumer.Consume(cancellationToken);
    await ProcessAsync(result.Message.Value);
    consumer.Commit(result);  // bunu yazmasaydık: her restart'ta baştan okur
}
```

**MassTransit Kafka Transport — High-level, soyutlama:**

```csharp
// Program.cs — OrderService
services.AddMassTransit(x =>
{
    x.UsingRabbitMq(...);  // mevcut RabbitMQ config

    x.AddRider(rider =>
    {
        rider.AddProducer<OrderCreatedEvent>("order-events");
        // bunu yazmasaydık: manuel ProduceAsync çağırman gerekir

        rider.UsingKafka((ctx, k) =>
        {
            k.Host("localhost:9092");  // bunu yazmasaydık: Kafka'ya bağlanamaz
        });
    });
});

// Program.cs — AnalyticsService (consumer)
services.AddMassTransit(x =>
{
    x.AddRider(rider =>
    {
        rider.AddConsumer<OrderAnalyticsConsumer>();

        rider.UsingKafka((ctx, k) =>
        {
            k.Host("localhost:9092");
            k.TopicEndpoint<OrderCreatedEvent>("order-events", "analytics-group", e =>
            {
                e.ConfigureConsumer<OrderAnalyticsConsumer>(ctx);
                // bunu yazmasaydık: consumer Kafka'ya register edilmez, mesaj alınmaz
            });
        });
    });
});
```

**Hangisini seç?**

| | Confluent Client | MassTransit Kafka |
|--|--|--|
| Kontrol | Tam kontrol | Soyutlama |
| Complexity | Yüksek | Düşük |
| Offset yönetimi | Manuel | Otomatik |
| MassTransit ile entegrasyon | Manuel bağlantı | Doğal entegrasyon |
| Kullanım | Özel gereksinimler | Standart mikroservis |

---

### Faz3 ile Karşılaştırma

Faz3'te domain event in-process, Faz5'te RabbitMQ ile servisler arası, Kafka ise "kalıcı log" ekliyor:

```csharp
// Faz3 — MediatR, aynı process
await _mediator.Publish(new OrderCreatedDomainEvent(order));
// Hızlı, geçmiş yok, replay yok

// Faz5 RabbitMQ — farklı servis, mesaj silinir
await _publishEndpoint.Publish(new OrderCreatedEvent(...));
// Async, mesaj okundu mu? Okunduktan sonra gitti

// Faz5 Kafka — farklı servis, mesaj arşivde
await _kafkaProducer.Produce("order-events", new OrderCreatedEvent(...));
// Async, 7 gün sonra da tekrar okuyabilirsin, yeni servis eski eventleri görebilir
```

| | Faz3 (MediatR) | Faz5 RabbitMQ | Faz5 Kafka |
|---|---|---|---|
| Kapsam | Aynı process | Servisler arası | Servisler arası |
| Mesaj ömrü | Transaction süresi | Okunana kadar | Retention süresi |
| Replay | ❌ | ❌ | ✅ |
| Throughput | Çok yüksek | Yüksek | Çok yüksek |
| Kullanım | Domain logic | Komut/görev | Event stream |
| Karmaşıklık | Düşük | Orta | Yüksek |

---

### 500 vs 50K Kullanıcı

| Karar | 500 kullanıcı/ay | 50K kullanıcı/ay |
|-------|-----------------|-----------------|
| Kafka kullan | ❌ RabbitMQ yeter, Kafka overkill | ✅ Yüksek throughput, log analizi, replay gerekince |
| Partition sayısı | 1-3 yeterli | 6-12 arası, servis sayısına göre |
| Retention | 3 gün yeterli | 7-30 gün, compliance gereksinimine göre |
| Consumer group | 1-2 group | Her servis için ayrı group |
| Exactly-once | ❌ At-least-once + idempotency yeterli | ✅ Finansal işlemlerde zorunlu |
| Log compaction | ❌ Basit retention yeterli | ✅ Ürün katalog, kullanıcı profil için |
| Event sourcing | ❌ Normal DB yeterli | ✅ Audit, time-travel, CQRS için |

---

## Örnek Kod

Kod ECommerceApp projesine entegre edilmiştir:

```
ECommerceApp/
├── docker/docker-compose.yml                         → RabbitMQ + Kafka + tüm servisler
├── src/Shared/Contracts/Events/
│   └── OrderCreatedEvent.cs                          → EventId eklendi (idempotency)
├── src/OrderService/                                 → değişmedi (RabbitMQ ile yayınlıyor)
└── src/AnalyticsService/                             → YENİ (Gün 126)
    ├── AnalyticsService.csproj                       → MassTransit.Kafka paketi
    ├── Program.cs                                    → Kafka consumer, analytics-group
    └── Consumers/OrderAnalyticsConsumer.cs           → mesajı işler, istatistik günceller
```

### docker-compose.yml

```yaml
version: '3.8'
services:
  zookeeper:
    image: confluentinc/cp-zookeeper:7.4.0
    # Kafka'nın koordinasyon servisi — bunu yazmasaydık: Kafka başlamaz
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000

  kafka:
    image: confluentinc/cp-kafka:7.4.0
    depends_on: [zookeeper]
    ports:
      - "9092:9092"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://localhost:9092
      # bunu yazmasaydık: dışarıdan bağlanamazsın
      KAFKA_LOG_RETENTION_HOURS: 168      # 7 gün sakla
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: true  # topic otomatik oluşsun
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1  # tek broker olduğu için 1

  order-service:
    build: ./OrderService
    ports:
      - "5002:8080"
    environment:
      Kafka__BootstrapServers: kafka:9092

  analytics-service:
    build: ./AnalyticsService
    environment:
      Kafka__BootstrapServers: kafka:9092
```

### Contracts/OrderCreatedEvent.cs

```csharp
namespace Contracts;

public record OrderCreatedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    // bunu yazmasaydık: idempotency kontrolü yapamayız, duplicate işleme riski

    public Guid OrderId { get; init; }
    public string CustomerEmail { get; init; } = string.Empty;
    public string BookTitle { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    // bunu yazmasaydık: mesajın ne zaman oluştuğunu bilemeyiz, geç işleme tespiti yapamayız
}
```

### OrderService/Program.cs

```csharp
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddRider(rider =>
    {
        rider.AddProducer<OrderCreatedEvent>("order-events");
        // bunu yazmasaydık: ITopicProducer inject edilemez, controller patlar

        rider.UsingKafka((ctx, k) =>
        {
            k.Host(builder.Configuration["Kafka:BootstrapServers"]!);
            // bunu yazmasaydık: localhost:9092'ye bağlanır, docker'da çalışmaz
        });
    });
});

builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.Run();
```

### OrderService/Controllers/OrderController.cs

```csharp
using Contracts;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly ITopicProducer<OrderCreatedEvent> _producer;
    // ITopicProducer: Kafka'ya özel producer, IPublishEndpoint RabbitMQ içindir

    public OrderController(ITopicProducer<OrderCreatedEvent> producer)
        => _producer = producer;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var orderEvent = new OrderCreatedEvent
        {
            OrderId = Guid.NewGuid(),
            CustomerEmail = request.CustomerEmail,
            BookTitle = request.BookTitle,
            TotalAmount = request.Quantity * request.UnitPrice
        };

        await _producer.Produce(orderEvent);
        // bunu yazmasaydık: event Kafka'ya gitmez, AnalyticsService hiç okuyamaz

        return Ok(new { orderEvent.OrderId, Message = "Sipariş alındı, event Kafka'ya yazıldı" });
    }
}

public record CreateOrderRequest(
    string CustomerEmail,
    string BookTitle,
    int Quantity,
    decimal UnitPrice
);
```

### AnalyticsService/Program.cs

```csharp
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddRider(rider =>
    {
        rider.AddConsumer<OrderAnalyticsConsumer>();
        // bunu yazmasaydık: consumer DI container'a kaydolmaz

        rider.UsingKafka((ctx, k) =>
        {
            k.Host(builder.Configuration["Kafka:BootstrapServers"]!);

            k.TopicEndpoint<OrderCreatedEvent>("order-events", "analytics-group", e =>
            {
                // "analytics-group" → NotificationService'den farklı group
                // bunu yazmasaydık: aynı group olursa mesajlar paylaşılır, her iki servis tümünü göremez

                e.ConfigureConsumer<OrderAnalyticsConsumer>(ctx);

                e.AutoOffsetReset = AutoOffsetReset.Earliest;
                // bunu yazmasaydık: servis ilk kez başlarken eski mesajları görmez
                // Latest: sadece yeni mesajları al (canlı sistemde bu tercih edilir)
            });
        });
    });
});

var app = builder.Build();
app.Run();
```

### AnalyticsService/Consumers/OrderAnalyticsConsumer.cs

```csharp
using Contracts;
using MassTransit;

public class OrderAnalyticsConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly ILogger<OrderAnalyticsConsumer> _logger;
    private static readonly Dictionary<string, int> _bookSales = new();
    // Gerçek uygulamada DB veya Redis — burada demo için in-memory

    public OrderAnalyticsConsumer(ILogger<OrderAnalyticsConsumer> logger)
        => _logger = logger;

    public Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var @event = context.Message;

        // İdempotency: aynı event iki kez gelirse sadece bir kez say
        // (Gerçek uygulamada EventId'yi DB'de kontrol et)
        _bookSales.TryGetValue(@event.BookTitle, out var current);
        _bookSales[@event.BookTitle] = current + 1;
        // bunu yazmasaydık: duplicate mesajda kitap satış sayısı şişer

        _logger.LogInformation(
            "📊 Analytics güncellendi | Kitap: {Book} | Toplam satış: {Count} | Tutar: {Amount:C}",
            @event.BookTitle,
            _bookSales[@event.BookTitle],
            @event.TotalAmount
        );

        return Task.CompletedTask;
        // Exception fırlatılmazsa MassTransit otomatik Ack gönderir
        // bunu yazmasaydık: mesaj işlenmeden sonraki mesaja geçilirdi
    }
}
```

**Çalıştırmak için:**

```bash
cd ECommerceApp/docker
docker-compose up -d

# Kafka hazır oluncaya kadar ~30 sn bekle, sonra:

# Sipariş oluştur — OrderService RabbitMQ'ya event yayınlar
# AnalyticsService Kafka'dan okur (OrderService aynı zamanda Kafka'ya da yazmalı — Gün 127'de OrderService güncellenir)
curl -X POST http://localhost:5002/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "ayse@mail.com",
    "customerName": "Ayşe Kaya",
    "productId": "11111111-1111-1111-1111-111111111111",
    "productName": "Clean Architecture",
    "quantity": 1,
    "unitPrice": 199.90
  }'

# AnalyticsService loglarını izle
docker logs analytics-service -f

# Kafka topic içeriğini izle (RabbitMQ'nun Management UI'ı gibi)
docker exec -it kafka \
  kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic order-events --from-beginning
# --from-beginning: baştan tüm mesajları oku — RabbitMQ'da bu mümkün değil
```

---

## Kontrol Soruları

1. RabbitMQ'da bir mesajı 3 farklı servis okuması için ne yapman gerekir? Kafka'da ne yapman gerekir? Hangisi daha kolay, neden?
2. AnalyticsService ilk kez ayağa kalktı. `AutoOffsetReset.Earliest` ile `Latest` arasındaki fark nedir? Hangi senaryoda hangisini seçerdin?
3. Aynı consumer group'tan 2 consumer varsa ve 3 partition varsa dağılım nasıl olur? 4 consumer olsaydı ne olurdu?
4. Kafka'da sıra garantisi tam olarak nerede geçerli, nerede değil? `orderId`'yi partition key olarak kullanmak bu garantiyi nasıl sağlıyor?
5. Exactly-once için hem transactional producer hem idempotent consumer neden gerekiyor? Sadece biri olsa ne eksik kalır?
