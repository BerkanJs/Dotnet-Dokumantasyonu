using ECommerce.Contracts.Events;
using MassTransit;

namespace NotificationService.Consumers;

// Saga'dan gelir: tüm adımlar başarılı, müşteriyi bildir
public class OrderCompletedConsumer : IConsumer<OrderCompletedEvent>
{
    private readonly ILogger<OrderCompletedConsumer> _logger;

    public OrderCompletedConsumer(ILogger<OrderCompletedConsumer> logger)
        => _logger = logger;

    public Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        var e = context.Message;

        // Gerçek projede: SMTP / SendGrid
        _logger.LogInformation(
            "📧 Email gönderildi: Siparişiniz yolda! | {Email} | {Amount:C}",
            e.CustomerEmail, e.TotalAmount);

        return Task.CompletedTask;
    }
}
