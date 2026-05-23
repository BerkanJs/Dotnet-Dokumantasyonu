using ECommerce.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Entities;
using OrderService.HttpClients;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly OrderDbContext     _db;
    private readonly IPublishEndpoint   _publishEndpoint;
    private readonly IProductHttpClient _productClient;
    // _productClient: CB + Retry + Timeout sarmalı (Gün 130-131)
    // BrokenCircuitException → CB OPEN (0ms)
    // TimeoutRejectedException → 3 retry'ın tümü timeout oldu (Gün 131)

    public OrderController(
        OrderDbContext     db,
        IPublishEndpoint   publishEndpoint,
        IProductHttpClient productClient)
    {
        _db              = db;
        _publishEndpoint = publishEndpoint;
        _productClient   = productClient;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        // ── Gün 130: Stok Kontrolü (Circuit Breaker koruması altında) ────────
        ProductInfo? product;
        try
        {
            product = await _productClient.GetProductAsync(request.ProductId);
            // Circuit Breaker OPEN ise bu satır çalışmadan BrokenCircuitException fırlatılır (0 ms bekleme)
            // Circuit Breaker CLOSED → ProductService'e HTTP gider, 5 sn timeout var
            // bunu yazmasaydık: ProductService çökünce thread bekler, OrderService de cascade etkilenir
        }
        catch (BrokenCircuitException)
        {
            // Gün 130 — CB OPEN: ProductService erişilemez, 0ms bekleme
            // bunu yazmasaydık: 500 döner → kullanıcı "Sistem hatası" görür
            return StatusCode(503, new
            {
                Message           = "Stok kontrolü şu an yapılamıyor, lütfen birkaç dakika sonra tekrar deneyin.",
                RetryAfterSeconds = 30
            });
        }
        catch (RateLimiterRejectedException)
        {
            // Gün 132 — Bulkhead: 10 eş zamanlı slot doldu, 11. istek reddedildi
            // bunu yazmasaydık: 500 döner — kullanıcı "sistem hatası" görür, gerçekte kapasite doldu
            return StatusCode(503, new
            {
                Message           = "Sistem şu an yoğun, lütfen birkaç saniye sonra tekrar deneyin.",
                RetryAfterSeconds = 5
            });
        }
        catch (TimeoutRejectedException)
        {
            // Gün 131 — 3 retry'ın tümü 4sn timeout'a takıldı → ProductService çok yavaş
            // bunu yazmasaydık: TimeoutRejectedException 500'e dönüşür → kullanıcı nedenini bilmez
            return StatusCode(503, new
            {
                Message           = "Ürün servisi şu an çok yavaş yanıt veriyor, lütfen kısa süre sonra tekrar deneyin.",
                RetryAfterSeconds = 10
            });
        }
        catch (HttpRequestException)
        {
            // Ağ hatası: DNS, bağlantı reddedildi vb.
            return StatusCode(503, new { Message = "Ürün servisi geçici olarak erişilemez." });
        }

        if (product is null)
            return NotFound(new { Message = "Ürün bulunamadı." });

        if (product.IsStockVerified && !product.InStock)
            return BadRequest(new { Message = $"'{product.Name}' stokta bulunmuyor." });
        // IsStockVerified=false → cache/unknown fallback geldi → stok bilinemedi
        // Siparişi yine de al; Saga stok doğrulamasını async yapacak (ShipmentService)
        // bunu yazmasaydık: fallback durumunda da "stokta yok" derdik → yanlış red
        // ─────────────────────────────────────────────────────────────────────

        // ── Gün 127: Idempotency ──────────────────────────────────────────────
        var idempotencyKey = $"{request.CustomerEmail}:{request.ProductId}:{DateTime.UtcNow:yyyyMMddHHmm}";
        // bunu yazmasaydık: çift tıklamada iki sipariş oluşur, iki kez ödeme alınır

        if (await _db.Orders.AnyAsync(o => o.IdempotencyKey == idempotencyKey))
            return Conflict(new { Message = "Bu sipariş zaten işleniyor, lütfen bekleyin." });

        var order = new Order
        {
            Id             = Guid.NewGuid(),
            CustomerEmail  = request.CustomerEmail,
            CustomerName   = request.CustomerName,
            ProductId      = request.ProductId,
            ProductName    = product.Name,    // ProductService'ten geldi — doğrulandı
            Quantity       = request.Quantity,
            TotalAmount    = product.Price * request.Quantity,  // fiyat ProductService'ten — manipülasyon yok
            CreatedAt      = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey,
            Status         = "Pending"
        };

        _db.Orders.Add(order);

        // ── Gün 127: Outbox ───────────────────────────────────────────────────
        await _publishEndpoint.Publish(new OrderCreatedEvent(
            OrderId:       order.Id,
            CustomerEmail: order.CustomerEmail,
            CustomerName:  order.CustomerName,
            ProductId:     order.ProductId,
            ProductName:   order.ProductName,
            Quantity:      order.Quantity,
            TotalAmount:   order.TotalAmount,
            CreatedAt:     order.CreatedAt
        ));
        // bunu yazmasaydık: DB + broker iki ayrı işlem → crash'te tutarsızlık

        await _db.SaveChangesAsync();
        // TEK SaveChanges: Orders + OutboxMessages aynı anda commit edilir

        return CreatedAtAction(nameof(CreateOrder), new { id = order.Id }, new { order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        var orders = await _db.Orders
            .Select(o => new { o.Id, o.CustomerName, o.ProductName, o.TotalAmount, o.Status })
            .ToListAsync();

        return Ok(orders);
    }
}

public record CreateOrderRequest(
    string  CustomerEmail,
    string  CustomerName,
    Guid    ProductId,
    int     Quantity
);
