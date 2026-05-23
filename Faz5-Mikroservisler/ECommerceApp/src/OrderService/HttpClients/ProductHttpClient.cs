using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace OrderService.HttpClients;

public class ProductHttpClient : IProductHttpClient
{
    private readonly HttpClient   _httpClient;
    private readonly IMemoryCache _cache;
    // _cache: fallback için — ProductService erişilemezse en son bilinen değer (Gün 133)
    private readonly ILogger<ProductHttpClient> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    // bunu yazmasaydık: her fallback'te önbelleksiz veri yoktur, Unknown dönmek zorunda kalırız

    public ProductHttpClient(HttpClient httpClient, IMemoryCache cache,
                             ILogger<ProductHttpClient> logger)
    {
        _httpClient = httpClient;
        _cache      = cache;
        _logger     = logger;
    }

    public async Task<ProductInfo?> GetProductAsync(Guid productId)
    {
        var cacheKey = $"product:{productId}";

        try
        {
            var response = await _httpClient.GetAsync($"api/products/{productId}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _cache.Remove(cacheKey); // Ürün silindi — eski cache'i temizle
                return null;
            }

            response.EnsureSuccessStatusCode();
            var product = await response.Content.ReadFromJsonAsync<ProductInfo>();

            if (product is not null)
                _cache.Set(cacheKey, product, CacheTtl);
            // Başarılı yanıtı cache'e yaz — sonraki fallback için hazır
            // bunu yazmasaydık: fallback'te cache boş kalır, Unknown dönmek zorunda kalırız

            return product;
        }
        catch (Exception ex) when (
            ex is BrokenCircuitException       // Gün 130: CB açık
               or RateLimiterRejectedException  // Gün 132: bulkhead dolu
               or TimeoutRejectedException      // Gün 131: tüm retry'lar timeout
               or HttpRequestException)         // Ağ hatası
        {
            _logger.LogWarning(
                "⚠️ ProductService erişilemiyor ({Type}), cache kontrol ediliyor: {Id}",
                ex.GetType().Name, productId);

            // Fallback 1: Cache'de eski veri var mı?
            if (_cache.TryGetValue(cacheKey, out ProductInfo? cached))
            {
                _logger.LogInformation("📦 Cache fallback — {Id}", productId);
                return cached;
                // bunu yazmasaydık: exception fırlatırdık → controller 503 dönerdi
            }

            // Fallback 2: Cache'de de yok → varsayılan
            _logger.LogWarning("❓ Cache'de de yok → Unknown fallback: {Id}", productId);
            return ProductInfoFallback.Unknown(productId);
            // bunu yazmasaydık: null dönerdik → controller NotFound dönerdi → müşteri ürünü göremez
        }
    }
}
