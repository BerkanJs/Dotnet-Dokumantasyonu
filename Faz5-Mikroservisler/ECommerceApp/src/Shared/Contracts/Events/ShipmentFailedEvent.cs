namespace ECommerce.Contracts.Events;

// ShipmentService → Saga: "Kargo hazırlanamadı, compensating transaction başlat"
public record ShipmentFailedEvent(
    Guid   OrderId,
    string Reason   // "Stokta kalmadı", "Depo kapalı"
);
