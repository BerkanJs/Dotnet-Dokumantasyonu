using ECommerce.Contracts.Events;
using MassTransit;

namespace PaymentService.Consumers;

// Saga'dan gelen "ödemeyi işle" komutunu dinler
public class PaymentRequestedConsumer : IConsumer<PaymentRequestedEvent>
{
    private readonly ILogger<PaymentRequestedConsumer> _logger;

    public PaymentRequestedConsumer(ILogger<PaymentRequestedConsumer> logger)
        => _logger = logger;

    public async Task Consume(ConsumeContext<PaymentRequestedEvent> context)
    {
        var req = context.Message;

        _logger.LogInformation(
            "💳 Ödeme işleniyor | OrderId={OrderId} | Tutar={Amount:C} | Müşteri={Email}",
            req.OrderId, req.Amount, req.CustomerEmail);

        // Gerçek projede: Stripe / İyzico / Iyzipay API çağrısı
        // Demo: ödeme işlemi ~500ms sürüyor
        await Task.Delay(500);

        // %80 başarı, %20 başarısız — demo amaçlı
        var isSuccessful = Random.Shared.NextDouble() > 0.2;

        if (isSuccessful)
        {
            var transactionId = $"TXN-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            // Saga'ya "ödeme tamam" bildir → Saga Completed durumuna geçer
            await context.Publish(new PaymentCompletedEvent(req.OrderId, transactionId));
            // bunu yazmasaydık: Saga PaymentCompleted event'ini alamaz → AwaitingPayment'ta kalır

            _logger.LogInformation(
                "✅ Ödeme başarılı | OrderId={OrderId} | TxnId={TxnId}",
                req.OrderId, transactionId);
        }
        else
        {
            // Saga'ya "ödeme başarısız" bildir → Saga compensating transaction başlatır
            await context.Publish(new PaymentFailedEvent(req.OrderId, "Yetersiz bakiye"));
            // bunu yazmasaydık: başarısız ödeme Saga'ya bildirilmez → sipariş askıda kalır

            _logger.LogWarning(
                "❌ Ödeme başarısız | OrderId={OrderId} | Sebep=Yetersiz bakiye",
                req.OrderId);
        }
    }
}
