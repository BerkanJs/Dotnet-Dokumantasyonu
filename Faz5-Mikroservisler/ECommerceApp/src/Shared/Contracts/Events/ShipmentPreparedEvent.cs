namespace ECommerce.Contracts.Events;

// ShipmentService → Saga: "Kargo hazırlandı, takip numarası oluşturuldu"
public record ShipmentPreparedEvent(
    Guid   OrderId,
    string TrackingNumber   // "TR-123456789"
);
