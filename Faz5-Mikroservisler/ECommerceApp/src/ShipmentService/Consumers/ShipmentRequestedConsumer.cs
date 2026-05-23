using ECommerce.Contracts.Events;
using MassTransit;

namespace ShipmentService.Consumers;

public class ShipmentRequestedConsumer : IConsumer<ShipmentRequestedEvent>
{
    private readonly ILogger<ShipmentRequestedConsumer> _logger;

    public ShipmentRequestedConsumer(ILogger<ShipmentRequestedConsumer> logger)
        => _logger = logger;

    public async Task Consume(ConsumeContext<ShipmentRequestedEvent> context)
    {
        var req = context.Message;

        _logger.LogInformation(
            "📦 Kargo hazırlanıyor | OrderId={OrderId} | {Product} x{Qty}",
            req.OrderId, req.ProductName, req.Quantity);

        // Gerçek projede: stok kontrolü, depo yönetim sistemi entegrasyonu
        await Task.Delay(800);

        // %90 başarı, %10 başarısız
        var isSuccessful = Random.Shared.NextDouble() > 0.1;

        if (isSuccessful)
        {
            var trackingNumber = $"TR-{Random.Shared.Next(100000000, 999999999)}";

            await context.Publish(new ShipmentPreparedEvent(req.OrderId, trackingNumber));
            // bunu yazmasaydık: Saga ShipmentPrepared alamaz → AwaitingShipment'ta kalır

            _logger.LogInformation(
                "✅ Kargo hazır | OrderId={OrderId} | Takip={Tracking}",
                req.OrderId, trackingNumber);
        }
        else
        {
            await context.Publish(new ShipmentFailedEvent(req.OrderId, "Stokta kalmadı"));
            // Saga compensating transaction başlatır: ödeme iade + sipariş iptal
            // bunu yazmasaydık: kargo başarısız ama Saga bunu bilmez → askıda sipariş

            _logger.LogWarning(
                "❌ Kargo başarısız | OrderId={OrderId}", req.OrderId);
        }
    }
}
