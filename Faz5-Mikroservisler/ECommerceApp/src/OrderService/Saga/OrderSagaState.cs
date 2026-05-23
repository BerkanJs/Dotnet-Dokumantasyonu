using MassTransit;

namespace OrderService.Saga;

public class OrderSagaState : SagaStateMachineInstance
{
    public Guid   CorrelationId { get; set; }
    public string CurrentState  { get; set; } = string.Empty;

    // Sipariş bilgileri
    public Guid    OrderId       { get; set; }
    public string  CustomerEmail { get; set; } = string.Empty;
    public string  CustomerName  { get; set; } = string.Empty;
    public string  ProductName   { get; set; } = string.Empty;
    public int     Quantity      { get; set; }
    public decimal TotalAmount   { get; set; }

    // Ödeme bilgileri — kargo başarısız olursa neyi iade edeceğimizi biliriz
    public DateTime? PaidAt        { get; set; }
    public string?   TransactionId { get; set; }
    // bunu yazmasaydık: ShipmentFailed gelince iade için transaction bilgisi yok

    // Kargo bilgileri
    public DateTime? ShippedAt      { get; set; }
    public string?   TrackingNumber { get; set; }

    // İptal/hata bilgileri
    public string? FailureReason { get; set; }
}
