using ECommerce.Contracts.Events;
using MassTransit;

namespace NotificationService.Consumers;

// IConsumer<T>: T tipindeki event'i bu sınıf işler
// MassTransit, queue'dan mesaj gelince bu Consume metodunu çağırır
public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger)
        => _logger = logger;

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var order = context.Message;

        _logger.LogInformation(
            "Sipariş bildirimi: OrderId={OrderId} | {Email} | {Product} x{Qty} | {Total:C}",
            order.OrderId, order.CustomerEmail,
            order.ProductName, order.Quantity, order.TotalAmount
        );

        // Gerçek SMTP/SendGrid çağrısı ilerleyen günlerde eklenecek
        await Task.Delay(50); // simüle I/O

        // Consume başarıyla bitince MassTransit otomatik Ack gönderir
    }
}
