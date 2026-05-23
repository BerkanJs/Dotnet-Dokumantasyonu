using ECommerce.Contracts.Events;
using MassTransit;

namespace AnalyticsService.Consumers;

// IConsumer<T>: MassTransit, Kafka'dan mesaj gelince bu Consume metodunu çağırır
public class OrderAnalyticsConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly ILogger<OrderAnalyticsConsumer> _logger;

    // Demo için in-memory istatistik — gerçek projede DB veya Redis kullan
    // static: uygulama ayakta kaldığı sürece veri korunsun
    private static readonly Dictionary<string, int> _bookSales  = new();
    private static readonly HashSet<Guid>           _seenEvents = new();
    // _seenEvents: idempotency — aynı EventId iki kez gelirse tekrar işleme
    // bunu yazmasaydık: Kafka at-least-once garantisi nedeniyle duplicate sayım olur

    public OrderAnalyticsConsumer(ILogger<OrderAnalyticsConsumer> logger)
        => _logger = logger;

    public Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var @event = context.Message;

        // İdempotency: bu EventId'yi daha önce işledik mi?
        // Gerçek projede bu kontrolü DB'de yap (HashSet restart'ta sıfırlanır)
        if (_seenEvents.Contains(@event.EventId))
        {
            _logger.LogWarning("⚠️  Duplicate event atlandı | EventId={EventId}", @event.EventId);
            return Task.CompletedTask;
            // bunu yazmasaydık: aynı sipariş iki kez sayılır, istatistik bozulur
        }

        _seenEvents.Add(@event.EventId);

        _bookSales.TryGetValue(@event.ProductName, out var current);
        _bookSales[@event.ProductName] = current + 1;

        _logger.LogInformation(
            "📊 Satış kaydedildi | Ürün: {Product} | Toplam: {Count} adet | OrderId: {OrderId} | Tutar: {Amount:C}",
            @event.ProductName,
            _bookSales[@event.ProductName],
            @event.OrderId,
            @event.TotalAmount
        );

        // Consume başarıyla bitince MassTransit otomatik Ack + offset commit yapar
        // bunu yazmasaydık: exception'da mesaj tekrar kuyruğa döner, işlem iptal sayılır
        return Task.CompletedTask;
    }
}
