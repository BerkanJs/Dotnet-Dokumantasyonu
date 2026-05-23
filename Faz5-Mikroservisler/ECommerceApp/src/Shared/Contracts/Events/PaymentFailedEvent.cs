namespace ECommerce.Contracts.Events;

// PaymentService → Saga: "Ödeme başarısız, compensating transaction tetikle"
public record PaymentFailedEvent(
    Guid   OrderId,
    string Reason   // "Yetersiz bakiye", "Kart reddedildi" vb.
);
