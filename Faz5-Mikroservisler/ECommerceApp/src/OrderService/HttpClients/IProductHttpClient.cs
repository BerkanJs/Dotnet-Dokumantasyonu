namespace OrderService.HttpClients;

/// <summary>
/// ProductService'e HTTP üzerinden bağlanan istemci arayüzü.
/// CB + ConcurrencyLimiter + Retry + Timeout + Cache Fallback — Gün 130-133
/// </summary>
public interface IProductHttpClient
{
    /// <summary>
    /// Ürün bilgisi ve stok durumunu ProductService'ten çeker.
    /// Servis erişilemez durumdaysa cache'den döner; cache'de de yoksa Unknown döner.
    /// </summary>
    Task<ProductInfo?> GetProductAsync(Guid productId);
}

/// <summary>ProductService'ten dönen ürün bilgisi</summary>
public record ProductInfo(
    Guid    ProductId,
    string  Name,
    int     Stock,
    decimal Price,
    bool    InStock,
    bool    IsStockVerified = true
    // false → stok bilgisi fallback'ten geldi, doğrulanamadı (Gün 133)
);

/// <summary>Gün 133 — Fallback: Servis erişilemediğinde dönen varsayılan yanıt</summary>
public static class ProductInfoFallback
{
    public static ProductInfo Unknown(Guid productId) => new(
        ProductId:       productId,
        Name:            "Ürün",
        Stock:           0,
        Price:           0m,
        InStock:         false,
        IsStockVerified: false
        // IsStockVerified=false → controller "stok doğrulanamadı, sipariş alınıyor" moduna geçer
        // bunu yazmasaydık: null dönerdik → controller NotFound dönerdi → müşteri ürünü göremez
    );
}
