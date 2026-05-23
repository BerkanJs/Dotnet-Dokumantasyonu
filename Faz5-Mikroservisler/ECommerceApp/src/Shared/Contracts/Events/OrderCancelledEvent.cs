namespace ECommerce.Contracts.Events;

// Saga → NotificationService: "Sipariş iptal edildi, müşteriyi bildir"
// Compensating transaction sonucu yayınlanır
public record OrderCancelledEvent(
    Guid   OrderId,
    string CustomerEmail,
    string Reason
);
