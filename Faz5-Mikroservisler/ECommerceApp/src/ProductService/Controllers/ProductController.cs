using Microsoft.AspNetCore.Mvc;

namespace ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    // Gerçek projede: EF Core + DB'den çekilir
    // Şimdilik static data — Circuit Breaker senaryosunu göstermek için yeterli
    private static readonly Dictionary<Guid, (string Name, int Stock, decimal Price)> _products = new()
    {
        [Guid.Parse("11111111-1111-1111-1111-111111111111")] = ("Clean Code",               45,  149.90m),
        [Guid.Parse("22222222-2222-2222-2222-222222222222")] = ("Domain-Driven Design",     12,  299.00m),
        [Guid.Parse("33333333-3333-3333-3333-333333333333")] = ("The Pragmatic Programmer",  0,  189.50m),
    };

    [HttpGet("{productId:guid}")]
    public IActionResult GetProduct(Guid productId)
    {
        if (!_products.TryGetValue(productId, out var product))
            return NotFound(new { Message = "Ürün bulunamadı" });

        return Ok(new
        {
            ProductId = productId,
            Name      = product.Name,
            Stock     = product.Stock,
            Price     = product.Price,
            InStock   = product.Stock > 0
        });
        // bunu yazmasaydık: OrderService stok kontrolü yapamaz, herhangi bir ürünü satabilir
    }
}
