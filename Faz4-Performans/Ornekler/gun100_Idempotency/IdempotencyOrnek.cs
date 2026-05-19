// GÜN 100 — Idempotency Key Pattern
// Aynı istek tekrar gelirse aynı sonuç dön — ödeme gibi kritik işlemler için
// Ağ hatası → client retry → duplicate işlem riski → idempotency ile önlenir

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Ornekler.gun100;

// --- 1. IEndpointFilter ile idempotency middleware ---
public class IdempotencyFilter : IEndpointFilter
{
    private readonly IDistributedCache _cache;

    public IdempotencyFilter(IDistributedCache cache) => _cache = cache;

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // ne yapar: idempotency key'i header'dan al
        // bunu yazmasaydık: her istek yeni işlem olarak değerlendirilirdi
        if (!context.HttpContext.Request.Headers.TryGetValue(
            "Idempotency-Key", out var key) || string.IsNullOrWhiteSpace(key))
        {
            // Key zorunlu — yoksa 422 döndür
            return Results.UnprocessableEntity(new { hata = "Idempotency-Key header zorunlu" });
        }

        string cacheKey = $"idempotency:{key}";

        // ne yapar: bu key daha önce kullanıldı mı kontrol et
        // bunu yazmasaydık: aynı key'li istek her seferinde yeni işlem başlatırdı
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached is not null)
        {
            // Daha önce işlendi — aynı response'u dön
            // ne yapar: cached response'u deserialize edip döner
            // bunu yazmasaydık: ödeme iki kez gerçekleşirdi
            context.HttpContext.Response.Headers["X-Idempotent-Replayed"] = "true";
            return JsonSerializer.Deserialize<object>(cached);
        }

        // İlk kez — işlemi yap
        var sonuc = await next(context);

        // ne yapar: sonucu 24 saat cache'le — bu süre içinde aynı key gelirse aynısını dön
        // bunu yazmasaydık: cache'e kaydetmeden idempotency sağlayamazdık
        if (sonuc is not null)
        {
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(sonuc), cacheOptions);
        }

        return sonuc;
    }
}

// --- 2. Endpoint kullanımı ---
public static class IdempotencyEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/odeme", async ([FromBody] OdemeIstegi istek, OdemeServisi servis) =>
        {
            var sonuc = await servis.OdemeYapAsync(istek);
            return Results.Ok(sonuc);
        })
        // ne yapar: bu endpoint'e gelen tüm istekler idempotency filter'dan geçer
        // bunu yazmasaydık: ağ hatası sonrası retry → çift ödeme
        .AddEndpointFilter<IdempotencyFilter>();
    }
}

// --- 3. Inbox Pattern: DB tabanlı garantili idempotency ---
public class InboxOrnek
{
    // Inbox tablosu — mesaj ID'sini bir kez işlenmiş olarak kaydeder
    // ne yapar: IdempotencyKey unique constraint → duplicate insert → exception → idempotent
    // bunu yazmasaydık: Redis TTL'i dolunca aynı key tekrar işlenebilirdi
    public async Task<bool> IdempotentIsle(string idempotencyKey, Func<Task> is_)
    {
        // CREATE UNIQUE INDEX IX_Inbox_Key ON InboxMessages(IdempotencyKey);
        // INSERT INTO InboxMessages (IdempotencyKey, Tarih) VALUES (@key, GETUTCDATE())
        // → duplicate key exception gelirse: zaten işlendi, return false
        // → başarıyla insert olduysa: işlemi yap, return true

        // (DB erişimi için DbContext inject edilmeli — basitlik için gösterilmedi)
        await is_();
        return true;
    }
}

public record OdemeIstegi(string KartNo, decimal Tutar, string Para);
public record OdemeSonucu(string IslemId, string Durum);
public class OdemeServisi
{
    public Task<OdemeSonucu> OdemeYapAsync(OdemeIstegi istek) =>
        Task.FromResult(new OdemeSonucu(Guid.NewGuid().ToString(), "Başarılı"));
}
