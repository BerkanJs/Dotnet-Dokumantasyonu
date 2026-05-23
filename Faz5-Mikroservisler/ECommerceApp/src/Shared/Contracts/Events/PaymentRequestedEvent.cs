namespace ECommerce.Contracts.Events;

// Saga → PaymentService: "Bu siparişin ödemesini al"
public record PaymentRequestedEvent(
    Guid    OrderId,
    decimal Amount,
    string  CustomerEmail
);
