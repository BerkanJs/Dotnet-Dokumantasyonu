// GÜN 101 — Structured Logging: Serilog
// Text log: "Kullanıcı 42 giriş yaptı" → aranabilir değil
// Structured log: {UserId: 42, Action: "Login"} → filtrele, aggregate et, alert kur

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Context;
using Serilog.Enrichers.Span;

namespace Ornekler.gun101;

// --- 1. Program.cs Serilog kurulumu ---
public static class SerilogSetup
{
    public static void Konfigur(WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            // ne yapar: her log'a makine adı, ortam, uygulama adı otomatik eklenir
            // bunu yazmasaydık: hangi pod'dan geldiğini bilemezdik (Kubernetes ortamında kritik)
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.FromLogContext()        // ne yapar: LogContext.PushProperty() ile eklenen alanlar
            .Enrich.WithSpan()              // ne yapar: OpenTelemetry TraceId, SpanId ekler
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} " +
                                "{Properties:j}{NewLine}{Exception}")
            .WriteTo.Seq("http://localhost:5341") // log aggregation sunucusu
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
            // ne yapar: EF Core sorgularını sadece Warning'den itibaren logla — gürültüyü azalt
            .CreateLogger();

        // ne yapar: ASP.NET Core'un default logger'ını Serilog ile değiştirir
        // bunu yazmasaydık: iki ayrı log sistemi yan yana çalışırdı
        builder.Host.UseSerilog();
    }
}

// --- 2. Request logging middleware ---
public static class RequestLoggingSetup
{
    public static void Konfigur(WebApplication app)
    {
        // ne yapar: her HTTP isteği için CorrelationId, Method, Path, StatusCode, Duration loglar
        // bunu yazmasaydık: hangi isteğin ne kadar sürdüğünü takip edemezdik
        app.UseSerilogRequestLogging(opt =>
        {
            opt.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                // ne yapar: her request log'una ek bilgi ekler
                // bunu yazmasaydık: sadece Method/Path/Status bilirdik, kim yaptığını bilemezdik
                diagnosticContext.Set("UserId",
                    httpContext.User.FindFirst("sub")?.Value ?? "anonymous");
                diagnosticContext.Set("ClientIp",
                    httpContext.Connection.RemoteIpAddress?.ToString());
            };
        });
    }
}

// --- 3. Custom enricher: Correlation ID ---
public class CorrelationIdEnricher : Serilog.Core.ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContext;

    public CorrelationIdEnricher(IHttpContextAccessor httpContext)
        => _httpContext = httpContext;

    public void Enrich(Serilog.Events.LogEvent logEvent, Serilog.Core.ILogEventPropertyFactory factory)
    {
        // ne yapar: her log'a X-Correlation-ID header'dan alınan değeri ekler
        // bunu yazmasaydık: birden fazla servisi kapsayan işlemleri trace edemezdik
        var correlationId = _httpContext.HttpContext?
            .Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        logEvent.AddPropertyIfAbsent(
            factory.CreateProperty("CorrelationId", correlationId));
    }
}

// --- 4. Hassas veri maskeleme ---
public class MaskeleDestructuringPolicy : Serilog.Core.IDestructuringPolicy
{
    public bool TryDestructure(object value, Serilog.Core.ILogEventPropertyValueFactory factory,
        out Serilog.Events.LogEventPropertyValue result)
    {
        if (value is OdemeIstegi istek)
        {
            // ne yapar: kart numarasının son 4 hanesini loglar, kalanı maskeler
            // bunu yazmasaydık: kredi kartı numarası log'lara düşerdi — KVKK ihlali
            result = factory.CreatePropertyValue(new
            {
                KartNo = $"****-****-****-{istek.KartNo[^4..]}",
                Tutar = istek.Tutar
            }, true);
            return true;
        }

        result = null!;
        return false;
    }
}

// --- 5. Kullanım örneği ---
public class SiparisServisi
{
    private readonly ILogger<SiparisServisi> _logger;

    public SiparisServisi(ILogger<SiparisServisi> logger) => _logger = logger;

    public async Task OlusturAsync(string kullaniciId, int kitapId)
    {
        // ne yapar: log context'e KullaniciId ekler — bu scope içindeki tüm log'lara eklenir
        // bunu yazmasaydık: her _logger.LogXxx çağrısında KullaniciId ayrıca geçirdik
        using (LogContext.PushProperty("KullaniciId", kullaniciId))
        using (LogContext.PushProperty("KitapId", kitapId))
        {
            // Serilog message template: {} içindeki isimler property olur — text değil
            // ne yapar: {KullaniciId} ve {KitapId} ayrı field olarak kaydedilir
            // bunu yazmasaydık: string concat log arama/filtrelemesini imkansız kılardı
            _logger.LogInformation("Sipariş oluşturma başladı. KullaniciId:{KullaniciId}, KitapId:{KitapId}",
                kullaniciId, kitapId);

            await Task.Delay(10);

            _logger.LogInformation("Sipariş oluşturuldu");
        }
    }
}

public record OdemeIstegi(string KartNo, decimal Tutar);
