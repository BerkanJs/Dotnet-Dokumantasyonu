namespace KitabeviMVC.Middleware;

// Her HTTP isteğinde çalışır — isteğin geldiğini, yanıtın ne kadar sürdüğünü loglar.
// Class-based middleware: DI ile ILogger alıyor, test edilebilir, ayrı dosyada duruyor.
public class IstekLoglamaMiddleware
{
    private readonly RequestDelegate _next;
    // RequestDelegate = "bir sonraki middleware'e geç" demek
    // ASP.NET Core bunu otomatik inject eder

    private readonly ILogger<IstekLoglamaMiddleware> _logger;

    public IstekLoglamaMiddleware(
        RequestDelegate next,
        ILogger<IstekLoglamaMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    // ASP.NET Core bu metodu her request'te çağırır
    public async Task InvokeAsync(HttpContext context)
    {
        var baslangic = DateTime.UtcNow;

        // → İstek geliyor
        _logger.LogInformation(
            "→ {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        await _next(context);   // bir sonraki middleware'e gönder, bekle

        // ← Yanıt döndü (next() bitti, burası response dönüşünde çalışır)
        var sure = (DateTime.UtcNow - baslangic).TotalMilliseconds;

        _logger.LogInformation(
            "← {StatusCode} {Path} ({Sure}ms)",
            context.Response.StatusCode,
            context.Request.Path,
            sure);
    }
}

// Extension method — Program.cs'de app.UseIstekLoglama() diye kullanmak için
public static class IstekLoglamaMiddlewareExtensions
{
    public static IApplicationBuilder UseIstekLoglama(this IApplicationBuilder app)
        => app.UseMiddleware<IstekLoglamaMiddleware>();
}
