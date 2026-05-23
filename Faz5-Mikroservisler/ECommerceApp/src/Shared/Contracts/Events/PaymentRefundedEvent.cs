namespace ECommerce.Contracts.Events;

// Saga → NotificationService: Compensating transaction bildirimi
// Kargo başarısız olunca Saga bu event'i yayınlar
public record PaymentRefundedEvent(
    Guid    OrderId,
    string  CustomerEmail,
    decimal Amount,
    string  Reason
);
