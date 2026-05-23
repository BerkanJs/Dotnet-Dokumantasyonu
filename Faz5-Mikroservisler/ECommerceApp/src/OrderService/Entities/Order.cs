namespace OrderService.Entities;

public class Order
{
    public Guid Id { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;
    // bunu yazmasaydık: çift tıklamada aynı sipariş iki kez oluşur

    public string Status { get; set; } = "Pending";
}
