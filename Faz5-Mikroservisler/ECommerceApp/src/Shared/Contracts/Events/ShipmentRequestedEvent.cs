namespace ECommerce.Contracts.Events;

// Saga → ShipmentService: "Bu siparişin kargosunu hazırla"
public record ShipmentRequestedEvent(
    Guid   OrderId,
    string CustomerName,
    string ProductName,
    int    Quantity
);
