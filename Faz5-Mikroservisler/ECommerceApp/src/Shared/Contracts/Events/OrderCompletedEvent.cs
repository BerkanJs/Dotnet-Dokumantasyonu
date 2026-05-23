namespace ECommerce.Contracts.Events;

// Saga → NotificationService: "Sipariş tamamlandı, müşteriyi bildir"
public record OrderCompletedEvent(
    Guid    OrderId,
    string  CustomerEmail,
    decimal TotalAmount
);
