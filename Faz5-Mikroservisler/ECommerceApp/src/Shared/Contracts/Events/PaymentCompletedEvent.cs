namespace ECommerce.Contracts.Events;

// PaymentService → Saga: "Ödeme alındı"
public record PaymentCompletedEvent(
    Guid   OrderId,
    string TransactionId   // ödeme referans numarası (Stripe, İyzico vb.)
);
