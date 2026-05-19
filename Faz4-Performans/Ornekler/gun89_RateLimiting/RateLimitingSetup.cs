// GÜN 89 — Rate Limiting
// ASP.NET Core 7+ built-in: Fixed Window, Sliding Window, Token Bucket, Concurrency

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.RateLimiting;

namespace Ornekler.gun89;

public static class RateLimitingSetup
{
    public static void Kaydet(WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(opt =>
        {
            // --- 1. Fixed Window: her pencerede N istek ---
            opt.AddFixedWindowLimiter("fixed", o =>
            {
                // ne yapar: 60 saniyelik pencerede en fazla 100 istek
                // bunu yazmasaydık: kullanıcı sınırsız istek atabilirdi
                o.PermitLimit = 100;
                o.Window = TimeSpan.FromSeconds(60);
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = 0; // ne yapar: kuyruk yok — hemen reddedilir
            });

            // --- 2. Sliding Window: daha akıcı, pencere kayar ---
            opt.AddSlidingWindowLimiter("sliding", o =>
            {
                // ne yapar: son 60 saniyede en fazla 100 istek (her 20s'de 1 segment)
                // bunu yazmasaydık: fixed window'da pencere başında burst olabilirdi
                o.PermitLimit = 100;
                o.Window = TimeSpan.FromSeconds(60);
                o.SegmentsPerWindow = 3; // 3 segment → 20s'de bir yenilenir
            });

            // --- 3. Token Bucket: burst'a izin verir ---
            opt.AddTokenBucketLimiter("token", o =>
            {
                // ne yapar: 20 token'lık kova, saniyede 5 token yenilenir
                // burst: anında 20 istek gönderebilir, sonra saniyede 5'e düşer
                // bunu yazmasaydık: fixed window'da burst kontrolü zor
                o.TokenLimit = 20;
                o.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
                o.TokensPerPeriod = 5;
                o.AutoReplenishment = true;
            });

            // --- 4. Concurrency: aynı anda kaç istek işlensin ---
            opt.AddConcurrencyLimiter("concurrent", o =>
            {
                // ne yapar: aynı anda en fazla 10 istek işlenir
                // bunu yazmasaydık: ağır DB sorguları paralel gelince sunucu çökerdi
                o.PermitLimit = 10;
                o.QueueLimit = 5;
            });

            // --- Reddedilen isteklere 429 dön ---
            opt.OnRejected = async (context, cancellationToken) =>
            {
                // ne yapar: rate limit aşıldığında 429 Too Many Requests döner
                // bunu yazmasaydık: default olarak bağlantı kesilirdi (daha kötü UX)
                context.HttpContext.Response.StatusCode = 429;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    // ne yapar: istemciye ne zaman tekrar deneyebileceğini söyler
                    context.HttpContext.Response.Headers.RetryAfter =
                        retryAfter.TotalSeconds.ToString("0");
                }

                await context.HttpContext.Response.WriteAsync(
                    "Çok fazla istek. Lütfen bekleyin.", cancellationToken);
            };
        });
    }

    public static void KonfigureMiddleware(WebApplication app)
    {
        // ne yapar: rate limiting middleware'ini pipeline'a ekler
        // SIRALAMA: UseRouting'den sonra, endpoint map'lemeden önce
        app.UseRateLimiter();
    }
}

// Endpoint kullanımı:
// app.MapGet("/kitaplar", () => "OK").RequireRateLimiting("fixed");
// app.MapPost("/siparis", () => "OK").RequireRateLimiting("token");

// Controller'da:
// [EnableRateLimiting("sliding")]
// public class KitapController : ControllerBase { }
