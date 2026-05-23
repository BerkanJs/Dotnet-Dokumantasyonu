using ECommerce.Contracts.Events;
using MassTransit;

namespace NotificationService.Consumers;

// Compensating transaction bildirimi: kargo başarısız → ödeme iade edildi
public class PaymentRefundedConsumer : IConsumer<PaymentRefundedEvent>
{
    private readonly ILogger<PaymentRefundedConsumer> _logger;

    public PaymentRefundedConsumer(ILogger<PaymentRefundedConsumer> logger)
        => _logger = logger;

    public Task Consume(ConsumeContext<PaymentRefundedEvent> context)
    {
        var e = context.Message;

        _logger.LogInformation(
            "📧 Email gönderildi: {Amount:C} iade edildi. Sebep: {Reason} | {Email}",
            e.Amount, e.Reason, e.CustomerEmail);
        // bunu yazmasaydık: müşteri paranın iade edildiğinden haberdar olmaz

        return Task.CompletedTask;
    }
}
