# Gün 66 — Event-Driven Architecture Temelleri

Bu bölümde CQRS ve Onion üstüne bir katman daha koyuyoruz: olay tabanlı düşünme.  
Amaç, modüller arası bağımlılığı azaltmak ve akışı daha esnek hale getirmek.

---

## Event-Driven Ne Demek?

Klasik yaklaşım:
- Servis A, Servis B'yi direkt çağırır
- A, B'nin ayakta olmasına ve API detayına bağlıdır

Event-driven yaklaşım:
- Servis A bir **olay yayınlar**
- İlgili servisler bu olayı **dinler** ve kendi işini yapar

Bu sayede üretici taraf tüketicileri bilmek zorunda kalmaz.

---

## Domain Event vs Integration Event

Müfredattaki en kritik ayrım:

- **Domain Event**
  - Aynı bounded context içinde
  - Domain'de gerçekleşen bir olayı temsil eder
  - Örn: `SiparisOlusturulduDomainEvent`

- **Integration Event**
  - Farklı bounded context / farklı servisler arası
  - Dış dünyaya yayılan kontrat
  - Örn: `OrderCreatedIntegrationEvent`

Kural: Domain event doğrudan dış broker mesajı olmak zorunda değil.  
Çoğu projede domain event -> mapping -> integration event akışı tercih edilir.

Kısa mapping örneği:

```csharp
public sealed record OrderCreatedIntegrationEvent(
    Guid OrderId,
    Guid CustomerId,
    DateTime CreatedAtUtc);

public static class EventMapper
{
    public static OrderCreatedIntegrationEvent ToIntegration(
        SiparisOlusturulduDomainEvent domainEvent)
        => new(domainEvent.SiparisId, domainEvent.KullaniciId, domainEvent.OccurredOn);
}
```

---

## Kitabevi Örneği

Sipariş oluşturulduğunda senaryo:

1. `Order` aggregate oluşturulur
2. `SiparisOlusturulduDomainEvent` eklenir
3. Transaction başarılı olursa event dispatch edilir
4. Gerekirse `OrderCreatedIntegrationEvent` olarak dışarı yayınlanır

Basit command handler akışı:

```csharp
public async Task<Guid> Handle(SiparisOlusturCommand request, CancellationToken ct)
{
    var siparis = Siparis.Create(request.KullaniciId, request.KitapId, request.Adet);
    await _siparisRepository.AddAsync(siparis, ct);
    await _unitOfWork.SaveChangesAsync(ct); // Domain event burada toplanmış olur
    return siparis.Id;
}
```

---

## Basit Domain Event Modeli

```csharp
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

public sealed record SiparisOlusturulduDomainEvent(
    Guid SiparisId,
    Guid KullaniciId,
    DateTime OccurredOn) : IDomainEvent;
```

Aggregate içinde biriktirme:

```csharp
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

`Siparis` içinde domain event ekleme:

```csharp
public class Siparis : Entity
{
    public Guid Id { get; private set; }
    public Guid KullaniciId { get; private set; }

    public static Siparis Create(Guid kullaniciId, int kitapId, int adet)
    {
        var siparis = new Siparis
        {
            Id = Guid.NewGuid(),
            KullaniciId = kullaniciId
        };

        siparis.AddDomainEvent(new SiparisOlusturulduDomainEvent(
            siparis.Id,
            siparis.KullaniciId,
            DateTime.UtcNow));

        return siparis;
    }
}
```

---

## Outbox Pattern Neden Gerekli?

Ana problem:
- DB'ye `SaveChanges()` başarılı
- Mesaj broker'a publish başarısız

Sonuç: veri kaydedildi ama event dışarı çıkmadı (tutarsızlık).

Outbox yaklaşımı:

1. Aynı DB transaction'ında hem domain verisi hem outbox kaydı yazılır
2. Ayrı bir worker outbox tablosunu okuyup broker'a publish eder
3. Başarılı publish sonrası outbox kaydı işlendi olarak işaretlenir

Bu model **at-least-once delivery** sağlar.

Outbox'a kayıt örneği (save sırasında):

```csharp
var outbox = new OutboxMessage
{
    Id = Guid.NewGuid(),
    OccurredOnUtc = DateTime.UtcNow,
    Type = nameof(OrderCreatedIntegrationEvent),
    Payload = JsonSerializer.Serialize(integrationEvent)
};

_db.OutboxMessages.Add(outbox);
await _db.SaveChangesAsync(ct);
```

---

## Outbox Kayıt Modeli (Örnek)

```csharp
public class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTime OccurredOnUtc { get; set; }
    public string Type { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public DateTime? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
}
```

Handler/UnitOfWork tarafında amaç:
- event'i doğrudan publish etmek yerine outbox'a yazmak
- publish işini background process'e bırakmak

Outbox worker için sade örnek:

```csharp
var messages = await _db.OutboxMessages
    .Where(x => x.ProcessedOnUtc == null)
    .OrderBy(x => x.OccurredOnUtc)
    .Take(20)
    .ToListAsync(ct);

foreach (var message in messages)
{
    await _publisher.PublishAsync(message.Type, message.Payload, ct);
    message.ProcessedOnUtc = DateTime.UtcNow;
}

await _db.SaveChangesAsync(ct);
```

---

## Eventual Consistency

Event-driven sistemlerde her şey anında aynı olmayabilir.  
Bazı veriler kısa süreli farklı görünebilir; buna **eventual consistency** denir.

Örnek:
- Sipariş servisi "oluşturuldu" dedi
- Bildirim servisi e-postayı 2 sn sonra gönderdi

Bu bir bug olmak zorunda değil, tasarım kararıdır.

---

## Pratik Tasarım Kuralları

- Event isimleri geçmiş zaman olmalı (`...Olusturuldu`, `...IptalEdildi`)
- Event payload minimum olmalı (gereksiz veri taşınmamalı)
- Event versioning düşünülmeli (`v1`, `v2` stratejisi)
- Consumer idempotent olmalı (aynı event iki kez gelirse bozulmamalı)
- Domain event ile integration event'i aynı sınıf yapma

Idempotent consumer kontrolü için minimal örnek:

```csharp
if (await _db.ProcessedMessages.AnyAsync(x => x.MessageId == messageId, ct))
    return; // daha önce işlendi

// iş mantığı...
_db.ProcessedMessages.Add(new ProcessedMessage { MessageId = messageId });
await _db.SaveChangesAsync(ct);
```

---

## Sık Yapılan Hatalar

- CRUD metot çağrısını "event-driven yaptık" sanmak
- Her olayı dışarı publish etmek (gürültü ve gereksiz coupling)
- Outbox olmadan "publish sonra save" yapmak
- Event şemasını kontrolsüz değiştirmek
- Retry + idempotency tasarlamamak

---

## 500 vs 50K Kullanıcı

| Konu | 500 kullanıcı | 50K kullanıcı |
|---|---|---|
| Domain event | Faydalı | Güçlü şekilde önerilir |
| Integration event | Sınırlı ihtiyaç | Kritik |
| Outbox | Ertelenebilir | Zorunluya yakın |
| Event versioning | Basit tutulabilir | Planlı strateji şart |
| Idempotent consumer | İyi olur | Kesinlikle gerekir |

---

## Mini Özet

Event-driven mimari, modülleri gevşek bağlar ve ölçeklenebilirliği artırır.  
Bu modelin güvenli çalışması için domain/integration event ayrımı, outbox ve idempotency birlikte düşünülmelidir.

---

## Kontrol Soruları

1. Domain event ile integration event arasındaki temel fark nedir?
2. Outbox pattern hangi veri tutarsızlığını önler?
3. Eventual consistency neden bazı sistemlerde kabul edilebilir bir trade-off'tur?
4. Event consumer tarafında idempotency olmazsa ne tür sorunlar çıkar?
