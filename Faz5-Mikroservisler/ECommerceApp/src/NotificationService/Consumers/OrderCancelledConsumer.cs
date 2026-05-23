using ECommerce.Contracts.Events;
using MassTransit;

namespace NotificationService.Consumers;

// Saga'dan gelir: ödeme ya da kargo başarısız, müşteriyi bildir
public class OrderCancelledConsumer : IConsumer<OrderCancelledEvent>
{
    private readonly ILogger<OrderCancelledConsumer> _logger;

    public OrderCancelledConsumer(ILogger<OrderCancelledConsumer> logger)
        => _logger = logger;

    public Task Consume(ConsumeContext<OrderCancelledEvent> context)
    {
        var e = context.Message;

        _logger.LogInformation(
            "📧 Email gönderildi: Siparişiniz iptal edildi. Sebep: {Reason} | {Email}",
            e.Reason, e.CustomerEmail);

        return Task.CompletedTask;
    }
}
