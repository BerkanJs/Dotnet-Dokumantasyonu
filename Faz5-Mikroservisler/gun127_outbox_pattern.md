# Gün 127 — Outbox Pattern: At-Least-Once Delivery

## Teorik

### Önce Problemi Hissedelim

Bir online kitap mağazasısın. Müşteri sipariş verdi. İki şey yapman gerekiyor:

1. Siparişi veritabanına kaydet
2. "Siparişiniz alındı" emaili gönder

Şu an kodun şöyle:

```csharp
await _db.Orders.AddAsync(order);
await _db.SaveChangesAsync();          // 1. DB'ye kaydet

await _publishEndpoint.Publish(...);   // 2. Email için event gönder
```

Basit görünüyor. Ama şu soruyu sor kendine:

**"1. adım başarılı oldu, 2. adım çalışmadan önce sunucu elektrik kesilirse ne olur?"**

```
Gerçek hayatta bu olur:
  → Müşteri para ödedi ✅
  → Sipariş DB'ye kaydedildi ✅
  → Email servisi hiç haberdar olmadı ❌
  → Müşteri telefon açıyor: "Sipariş verdim ama onay gelmedi?"
```

Ya tam tersi:

```
  → Email servisi "sipariş geldi" diye çalıştı ✅
  → SaveChanges'ten önce DB bağlantısı koptu ❌
  → DB'de sipariş yok ama email gönderildi
  → Müşteri emaili aldı ama sipariş sisteme kaydolmadı
```

**Bu ikisi neden olur?**

DB'ye yazmak ve event göndermek iki **ayrı sistem**. Bunları aynı anda, atomik olarak yapamazsın. Biri başarılı olup diğeri başarısız olabilir.

---

### Outbox Pattern — Çözüm

**Analoji: Taşınma günü kutular**

Evi taşıyorsun. Her eşyayı doğrudan kamyona atmak yerine önce "Taşınacaklar" kutusuna koyuyorsun. Taşıma şirketi gelince kutuya bakıyor, her şeyi alıp kamyona yüklüyor, kutuyu "teslim edildi" diye işaretliyor.

Elektrik gitse bile eşyalar kutu içinde güvende. Taşıma şirketi gecikmeli gelse de eşyalar kaybolmaz.

```
Eski yöntem (sorunlu):
  Sipariş geldi → DB'ye yaz → Email servisine direkt gönder
                                    ↑
                              burada crash olursa kayıp

Outbox yöntemi (güvenli):
  Sipariş geldi → [DB'ye yaz + "Taşınacaklar kutusuna" yaz] → tek seferde ✅
                                   ↓
                  Arka plan işçisi kutuya bakıyor
                                   ↓
                  Email servisine gönderdi → kutuyu işaretledi ✅
```

"Taşınacaklar kutusu" = **Outbox tablosu** (aynı veritabanında bir tablo)

Kritik nokta: **DB kaydı ve outbox kaydı aynı transaction içinde yazılıyor.** Ya ikisi birden başarılı, ya ikisi birden başarısız. Arası yok.

---

### Outbox Tablosu Neye Benziyor?

```sql
-- Bu tablo senin normal "orders" tabloyla aynı veritabanında
OutboxMessages:
  ┌──────────┬──────────────────────────┬───────────────────┬──────────┬────────┐
  │ Id       │ Type                     │ Body              │CreatedAt │ SentAt │
  ├──────────┼──────────────────────────┼───────────────────┼──────────┼────────┤
  │ a1b2...  │ OrderCreatedEvent        │ {"orderId":"..."}  │ 14:30:01 │ NULL   │ ← bekliyor
  │ c3d4...  │ OrderCreatedEvent        │ {"orderId":"..."}  │ 14:29:55 │ 14:29:56│ ← gönderildi
  └──────────┴──────────────────────────┴───────────────────┴──────────┴────────┘
```

Arka plan işçisi (worker) her 1 saniyede bir şunu yapar:

```
"SentAt = NULL olan var mı?" → Varsa al → RabbitMQ'ya gönder → SentAt'ı doldur
```

---

### Polling Publisher vs Transaction Log Tailing

**Polling Publisher — "Posta kutusunu düzenli kontrol et"**

```
Analoji: Her gün saat 09:00'da postanı kontrol edersin.
         Mektup gece yarısı geldiyse sabaha kadar bekledi.
         Ama kaybolmadı — posta kutusunda güvende.

Kod olarak:
  Worker: her 1 saniyede bir → "OutboxMessages'ta SentAt IS NULL var mı?" → gönder
```

Avantajı: Kurulumu basit, her veritabanıyla çalışır.
Dezavantajı: Mesaj 1 saniye bekler (genellikle sorun değil).

---

**Transaction Log Tailing (Debezium) — "Postane kapısını izle"**

```
Analoji: Postacı posta kutusunu kontrol etmek yerine
         kapının önünde bekliyor. Mektup atılır atılmaz görüyor.
         Anında hareket ediyor.

Teknik olarak:
  PostgreSQL her yazma işlemini WAL (Write-Ahead Log) dosyasına kaydeder.
  Debezium bu dosyayı gerçek zamanlı okur → değişikliği Kafka'ya iletir.
```

Avantajı: Gerçek zamanlı, sıfır gecikme.
Dezavantajı: Debezium ayrı bir servis, kurulumu karmaşık.

**Ne zaman hangisi?**

```
Polling Publisher → Çoğu projede bu yeterli (bizim projemiz dahil)
Debezium         → Saniyede 100.000+ işlem, gecikme kabul edilemez durumlarda
```

---

### MassTransit Bunu Nasıl Hallediyor?

Kendin Outbox tablosu oluşturmak, worker yazmak zorunda değilsin. MassTransit bunu EF Core ile entegre ederek otomatik yapıyor.

**Senin yapman gereken 3 şey:**

**1. Program.cs'e Outbox'ı ekle:**

```csharp
x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
{
    o.UseInMemoryOutbox(); // veya UsePostgres(), UseSqlServer()
    o.UseBusOutbox();
    // bunu yazmasaydık: Outbox devreye girmez, Publish direkt RabbitMQ'ya gider
    // crash'te mesaj kaybolur
    
    o.QueryDelay = TimeSpan.FromSeconds(1);
    // bunu yazmasaydık: varsayılan 10 sn — email 10 sn gecikmeli gönderilir
});
```

**2. DbContext'e Outbox tablolarını ekle:**

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Senin tablolarının yanına MassTransit'in tablolarını da ekle
    modelBuilder.AddOutboxMessageEntity();   // gönderilecek mesajlar
    modelBuilder.AddOutboxStateEntity();     // worker durumu
    modelBuilder.AddInboxStateEntity();      // consumer tarafı takip
    // bunu yazmasaydık: MassTransit tablolarını bulamaz → uygulama başlarken hata
}
```

**3. Controller'da hiçbir şeyi değiştirmene gerek yok:**

```csharp
// Bu satır AYNI kalıyor — ama artık farklı davranıyor
await _publishEndpoint.Publish(new OrderCreatedEvent(...));

// Outbox OLMADAN: direkt RabbitMQ'ya gider
// Outbox İLE:     OutboxMessages tablosuna yazar, worker gönderir

// Fark şeffaf — kod aynı, davranış güvenli
```

---

### Idempotency — "Aynı Şeyi İki Kez Yapma"

Outbox **at-least-once** (en az bir kez) garantisi verir. Yani:

```
Normal senaryo: Mesaj 1 kez gönderilir ✅
Nadir senaryo:  Ağ sorunu → worker tekrar dener → mesaj 2 kez gönderilir ⚠️
```

**Analoji: Kapıyı iki kez çalan kargo**

Kargocunun kapıyı çaldığını duymadın. Tekrar çaldı. İkinci sefer açtın. Kargo teslim edildi, sorun yok.

Ama sen bir banka olsaydın:

```
"Ali'ye 500 TL gönder" işlemi
  → İlk deneme: ağ sorunu, cevap gelmedi
  → İkinci deneme: tekrar gönderildi
  → Ali 1000 TL aldı ❌
```

Bunun önüne geçmek için her işleme benzersiz bir kimlik verirsin:

```
"Ali'ye 500 TL gönder, işlem ID: TXN-1234"
  → İlk deneme: işlendi, TXN-1234 kaydedildi
  → İkinci deneme geldi: "TXN-1234'ü daha önce işledim" → atla ✅
```

---

### Idempotency Key Pattern

Kodda bunu şöyle uygularsın:

```csharp
// "Bu isteği daha önce gördüm mü?" sorusunu cevapla
// Aynı müşteri + aynı ürün + aynı dakika = aynı istek sayılır
var idempotencyKey = $"{request.CustomerEmail}:{request.ProductId}:{DateTime.UtcNow:yyyyMMddHHmm}";
//                    ─────────────────────────────────────────────────────────────────────────
//                    "ali@mail.com:KITAP-111:202605231430"

// Bu key ile daha önce sipariş oluşturduk mu?
if (await _db.Orders.AnyAsync(o => o.IdempotencyKey == idempotencyKey))
    return Conflict(new { Message = "Bu sipariş zaten işleniyor." });
// bunu yazmasaydık: kullanıcı "Sipariş Ver" butonuna iki kez bastı → iki sipariş → iki ödeme
```

**Key ne kadar özgün olmalı?**

```
Çok geniş key: "ali@mail.com" → Ali hiç sipariş veremez, hep "zaten var" der
Çok dar key:   sadece timestamp → her saniyede bir tane geçer, koruma yok
Doğru key:     müşteri + ürün + zaman penceresi (1 dakika) → mantıklı
```

---

### Faz3 ile Karşılaştırma

Faz3'te her şey aynı process içindeydi, bu sorun yoktu:

```csharp
// Faz3 — MediatR, aynı thread, aynı transaction
await _mediator.Publish(new OrderCreatedDomainEvent(order));
// "Event yayınla" ile "DB'ye kaydet" aynı Unit of Work içinde
// Ya ikisi birden olur ya ikisi birden olmaz — crash problemi yok
```

Faz5'te servisler farklı makinelerde, farklı veritabanlarında:

```csharp
// Faz5 — farklı servisler, farklı DB'ler
await _db.SaveChangesAsync();        // OrderService DB'si
await _publishEndpoint.Publish();    // NotificationService'e mesaj
// Bunlar farklı sistemler — birini garanti edersen diğerini edemezsin
```

Outbox bu farkı kapatıyor:

```
Faz3'ün güvenliği + Faz5'in dağıtık mimarisi = Outbox Pattern
```

| | Faz3 (MediatR) | Faz5 Outbox yok | Faz5 Outbox var |
|---|---|---|---|
| Atomiklik | ✅ Aynı transaction | ❌ 2 ayrı sistem | ✅ Tek transaction |
| Crash'te mesaj kaybı | ❌ Olmaz | ✅ Olur | ❌ Olmaz |
| Email gecikmesi | Sıfır | Sıfır | ~1 sn |
| Karmaşıklık | Düşük | Düşük | Orta |
| Dağıtık mimari | ❌ Tek process | ✅ | ✅ |

---

### 500 vs 50K Kullanıcı

| Karar | 500 kullanıcı/ay | 50K kullanıcı/ay |
|-------|-----------------|-----------------|
| Outbox kullan | ⚠️ Crash nadir, ama yine de ekle | ✅ Zorunlu, veri tutarsızlığı kabul edilemez |
| Polling aralığı | 5 sn bile yeterli | 1 sn veya altı |
| Debezium | ❌ Overkill | ⚠️ Yalnızca 100K+ işlem/sn'de |
| IdempotencyKey | ✅ Her zaman ekle | ✅ Zorunlu |
| InboxState (consumer) | ❌ Manuel kontrol yeterli | ✅ MassTransit otomatik yönetsin |
| Outbox kayıt temizliği | Haftalık yeterli | Günlük veya otomatik TTL ile |

---

## Örnek Kod

Outbox, **OrderService**'e eklendi. Artık OrderService sipariş oluşturunca hem DB'ye hem Outbox tablosuna yazıyor:

```
ECommerceApp/
└── src/OrderService/
    ├── OrderService.csproj          → EF Core + MassTransit.EntityFrameworkCore
    ├── Program.cs                   → Outbox yapılandırması
    ├── Data/
    │   └── OrderDbContext.cs        → Orders tablosu + MassTransit Outbox tabloları
    ├── Entities/
    │   └── Order.cs                 → sipariş entity'si, IdempotencyKey dahil
    └── Controllers/
        └── OrderController.cs      → DB kaydet + Publish (Outbox otomatik devreye girer)
```

### OrderService.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MassTransit.RabbitMQ" Version="8.3.6" />
    <!-- Gün 127 — Outbox için EF Core entegrasyonu -->
    <PackageReference Include="MassTransit.EntityFrameworkCore" Version="8.3.6" />
    <!-- Demo için InMemory DB — gerçek projede Postgres/SqlServer kullan -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Shared\Contracts\Contracts.csproj" />
  </ItemGroup>
</Project>
```

### Entities/Order.cs

```csharp
namespace OrderService.Entities;

public class Order
{
    public Guid Id { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;
    // bunu yazmasaydık: kullanıcı butona çift tıklar → iki sipariş → iki ödeme

    public string Status { get; set; } = "Pending";
}
```

### Data/OrderDbContext.cs

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Entities;

namespace OrderService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);

            e.HasIndex(o => o.IdempotencyKey).IsUnique();
            // bunu yazmasaydık: DB katmanında son savunma çalışmaz,
            // aynı key iki kez girebilir → duplicate sipariş
        });

        // MassTransit'in ihtiyaç duyduğu Outbox tablolarını şemaya ekle
        // Bunlar senin Orders tablon ile aynı DB'de oluşur
        modelBuilder.AddInboxStateEntity();
        // bunu yazmasaydık: consumer tarafı idempotency takibi yapılamaz

        modelBuilder.AddOutboxMessageEntity();
        // bunu yazmasaydık: OutboxMessages tablosu yok → Publish çağrısı exception fırlatır

        modelBuilder.AddOutboxStateEntity();
        // bunu yazmasaydık: worker hangi mesajı gönderdiğini takip edemez
    }
}
```

### Program.cs

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Gün 127 — OrderService artık DB kullanıyor
builder.Services.AddDbContext<OrderDbContext>(opt =>
    opt.UseInMemoryDatabase("OrdersDb"));
// InMemory: uygulama kapanınca veriler silinir, geliştirme için ideal
// Gerçek projede: opt.UseNpgsql("...connection string...")

// Gün 125 + 127 — MassTransit + RabbitMQ + Outbox Pattern
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UseInMemoryOutbox();
        // InMemory DB ile uyumlu — gerçek projede o.UsePostgres() veya o.UseSqlServer()
        // bunu yazmasaydık: Outbox veritabanı sağlayıcısını bilemez, hata verir

        o.UseBusOutbox();
        // bunu yazmasaydık: Outbox kayıtlı ama devreye girmez
        // Publish çağrıları eskisi gibi direkt RabbitMQ'ya gider → crash riski devam eder

        o.QueryDelay = TimeSpan.FromSeconds(1);
        // bunu yazmasaydık: varsayılan 10 sn — email 10 sn gecikmeli gelir
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ__Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ__Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ__Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// Uygulama başlarken DB tablolarını oluştur
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
    // bunu yazmasaydık: tablolar yok → ilk POST isteğinde "table not found" hatası
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "OrderService" }));
app.Run();
```

### Controllers/OrderController.cs

```csharp
using ECommerce.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Entities;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly OrderDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    // IPublishEndpoint: Outbox aktifken bu artık OutboxMessages tablosuna yazar

    public OrderController(OrderDbContext db, IPublishEndpoint publishEndpoint)
    {
        _db              = db;
        _publishEndpoint = publishEndpoint;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        // Adım 1: Bu isteği daha önce gördük mü?
        var idempotencyKey = $"{request.CustomerEmail}:{request.ProductId}:{DateTime.UtcNow:yyyyMMddHHmm}";
        // "ali@mail.com:11111111-...:202605231430" → her dakika bir pencere

        if (await _db.Orders.AnyAsync(o => o.IdempotencyKey == idempotencyKey))
            return Conflict(new { Message = "Bu sipariş zaten işleniyor, lütfen bekleyin." });
        // bunu yazmasaydık: çift tıklama → çift sipariş → çift ödeme alınır

        // Adım 2: Sipariş nesnesini oluştur
        var order = new Order
        {
            Id             = Guid.NewGuid(),
            CustomerEmail  = request.CustomerEmail,
            CustomerName   = request.CustomerName,
            ProductId      = request.ProductId,
            ProductName    = request.ProductName,
            Quantity       = request.Quantity,
            TotalAmount    = request.Quantity * request.UnitPrice,
            CreatedAt      = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey,
            Status         = "Pending"
        };

        _db.Orders.Add(order);
        // Henüz DB'ye yazmadı — sadece "değişiklik listesine" ekledi

        // Adım 3: Event yayınla
        // Outbox aktif: Bu çağrı RabbitMQ'ya GITMEZ
        // OutboxMessages tablosuna yazar — asıl gönderimi worker üstlenir
        // bunu yazmasaydık: DB + RabbitMQ ayrı transaction → crash'te tutarsızlık
        await _publishEndpoint.Publish(new OrderCreatedEvent(
            OrderId:       order.Id,
            CustomerEmail: order.CustomerEmail,
            CustomerName:  order.CustomerName,
            ProductId:     order.ProductId,
            ProductName:   order.ProductName,
            Quantity:      order.Quantity,
            TotalAmount:   order.TotalAmount,
            CreatedAt:     order.CreatedAt
        ));

        // Adım 4: TEK SaveChanges ile her şey aynı anda commit olur:
        //   ✅ orders tablosuna sipariş yazıldı
        //   ✅ outbox_messages tablosuna event yazıldı
        //   Worker ~1 sn sonra okuyup RabbitMQ'ya gönderir
        // bunu yazmasaydık: DB ve mesaj ayrı transaction → crash'te biri kaybolabilir
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(CreateOrder), new { id = order.Id }, new { order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        var orders = await _db.Orders
            .Select(o => new { o.Id, o.CustomerName, o.ProductName, o.TotalAmount, o.Status })
            .ToListAsync();

        return Ok(orders);
    }
}

public record CreateOrderRequest(
    string  CustomerEmail,
    string  CustomerName,
    Guid    ProductId,
    string  ProductName,
    int     Quantity,
    decimal UnitPrice
);
```

**Çalıştırmak için:**

```bash
cd ECommerceApp/docker
docker-compose up -d

# Sipariş oluştur
curl -X POST http://localhost:5002/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "ali@mail.com",
    "customerName": "Ali Yılmaz",
    "productId": "11111111-1111-1111-1111-111111111111",
    "productName": "Clean Code",
    "quantity": 2,
    "unitPrice": 149.90
  }'

# Aynı isteği 1 dakika içinde tekrar gönder → 409 Conflict döner
# (idempotency key çalıştı)

# Siparişleri listele
curl http://localhost:5002/api/orders

# NotificationService ~1 sn sonra email logunu bastı mı?
docker logs notification-service -f
```

---

## Kontrol Soruları

1. Sipariş oluşturuldu, `SaveChangesAsync()` henüz çağrılmadı, sunucu çöktü. Outbox olmadan ne olurdu? Outbox ile ne olur?
2. Outbox pattern "at-least-once" garanti veriyor. "Exactly-once" garantisi neden çok daha zor? Hangi ek mekanizma gerekiyor?
3. IdempotencyKey'i sadece `CustomerEmail` yapsaydın ne olurdu? Her müşteri ömrü boyunca bir tane sipariş verebilirdi. Peki sadece `DateTime.UtcNow` yapsaydın?
4. Polling aralığı 1 sn olan bir sistemde, sipariş emaili en geç kaç saniye gecikmeli gelebilir? Bu gecikme hangi e-ticaret senaryolarında sorun olmaz, hangilerinde olur?
5. `modelBuilder.AddOutboxMessageEntity()` satırını silip uygulamayı çalıştırsaydın, hata ilk POST isteğinde mi, uygulama başlarken mi gelirdi? Neden?
