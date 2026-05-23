namespace ECommerce.Contracts.Events;

// Servisler arası paylaşılan event — OrderService yayar, dinleyenler alır
// record: immutable ve value equality — mesaj nesneleri için ideal
// bunu record yerine class yapsaydık: MassTransit serileştirirken sorun çıkabilir
public record OrderCreatedEvent(
    Guid     OrderId,
    string   CustomerEmail,
    string   CustomerName,
    Guid     ProductId,
    string   ProductName,
    int      Quantity,
    decimal  TotalAmount,
    DateTime CreatedAt
)
{
    // Gün 126 — Kafka idempotency için EventId
    // Non-positional property: mevcut constructor çağrılarını bozmadan eklendi
    // bunu yazmasaydık: Kafka at-least-once'da aynı mesaj 2 kez gelirse duplicate işleme olur
    public Guid EventId { get; init; } = Guid.NewGuid();
}
