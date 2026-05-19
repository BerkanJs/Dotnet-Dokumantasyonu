// GÜN 93 — Health Checks
// Liveness: "uygulama hayatta mı?" → hayır → pod yeniden başlatılır
// Readiness: "trafik alabilir mi?" → hayır → load balancer dışına çıkarılır

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ornekler.gun93;

// --- 1. Custom Health Check: dış bağımlılık kontrolü ---
public class RedisHealthCheck : IHealthCheck
{
    private readonly StackExchange.Redis.IConnectionMultiplexer _redis;

    public RedisHealthCheck(StackExchange.Redis.IConnectionMultiplexer redis)
        => _redis = redis;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // ne yapar: Redis'e PING atar, cevap beklenir
            // bunu yazmasaydık: Redis çökmüş olsa bile sağlıklı görünürdük
            await _redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy("Redis bağlantısı başarılı");
        }
        catch (Exception ex)
        {
            // ne yapar: degraded (bozuk ama kurtarılabilir) vs unhealthy (kritik) ayrımı
            // bunu yazmasaydık: tüm hatalar aynı şiddetle raporlanırdı
            return HealthCheckResult.Unhealthy("Redis bağlantısı başarısız", ex);
        }
    }
}

// --- 2. Program.cs kayıt ---
public static class HealthCheckSetup
{
    public static void Kaydet(WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            // ne yapar: EF Core DbContext üzerinden DB bağlantısını kontrol eder
            // bunu yazmasaydık: DB çökmüş olsa bile readiness healthy dönürdü
            .AddDbContextCheck<AppDbContext>(
                name: "veritabani",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "readiness" })

            // ne yapar: Redis ping kontrolü — custom health check
            .AddCheck<RedisHealthCheck>(
                name: "redis",
                failureStatus: HealthStatus.Degraded,   // kritik değil, degraded yeterli
                tags: new[] { "readiness" })

            // ne yapar: dış URL'ye GET atar, 200 beklenir
            .AddUrlGroup(
                new Uri("https://api.ispartnerim.com/health"),
                name: "dis-api",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "readiness" });
    }

    public static void KonfigureEndpoint(WebApplication app)
    {
        // Liveness: sadece uygulamanın çalışıp çalışmadığı — DB/Redis kontrol yok
        // ne yapar: /health/live → her zaman hızlı cevap ver
        // bunu yazmasaydık: DB yavaşsa Kubernetes pod'u gereksiz yere yeniden başlatırdı
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false, // hiçbir check çalıştırma — sadece "uygulama ayakta"
            ResponseWriter = YazDegistir
        });

        // Readiness: tüm bağımlılıklar hazır mı?
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            // ne yapar: sadece "readiness" tag'li check'leri çalıştır
            // bunu yazmasaydık: liveness check'leri readiness'ı yavaşlatırdı
            Predicate = check => check.Tags.Contains("readiness"),
            ResponseWriter = YazDegistir
        });
    }

    private static async Task YazDegistir(
        Microsoft.AspNetCore.Http.HttpContext ctx,
        HealthReport rapor)
    {
        ctx.Response.ContentType = "application/json";

        // ne yapar: tüm check'lerin durumunu JSON olarak yazar
        // bunu yazmasaydık: sadece "Healthy" / "Unhealthy" text dönürdü
        var sonuc = new
        {
            durum = rapor.Status.ToString(),
            sure = rapor.TotalDuration.TotalMilliseconds,
            kontroller = rapor.Entries.ToDictionary(
                e => e.Key,
                e => new { durum = e.Value.Status.ToString(), aciklama = e.Value.Description })
        };

        await System.Text.Json.JsonSerializer.SerializeAsync(ctx.Response.Body, sonuc);
    }
}

// Kubernetes probe örneği (deployment.yaml):
// livenessProbe:
//   httpGet:
//     path: /health/live
//     port: 8080
//   initialDelaySeconds: 10
//   periodSeconds: 5
//
// readinessProbe:
//   httpGet:
//     path: /health/ready
//     port: 8080
//   initialDelaySeconds: 15
//   periodSeconds: 10

public class AppDbContext : DbContext { }
