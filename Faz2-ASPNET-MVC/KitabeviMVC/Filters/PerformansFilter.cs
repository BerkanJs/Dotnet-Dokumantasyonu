using Microsoft.AspNetCore.Mvc.Filters;

namespace KitabeviMVC.Filters;

// ─────────────────────────────────────────────────────────────────────
// Gün 19 — Performans İzleme Filter
//
// Gerçek ihtiyaç: Production'da bir action aniden yavaşladığında
// (N+1 sorunu, eksik index, dış servis gecikmesi) bunu otomatik
// tespit etmek gerekir. Her action'a stopwatch koymak yerine
// bu tek filter tüm uygulamayı kapsar.
//
// Eşiği aşan action'lar LogWarning ile işaretlenir.
// Application Insights, Seq veya Grafana gibi araçlar bu uyarılara
// alert kurar → ekip anında haberdar olur.
//
// Neden middleware değil?
//   Middleware action adını bilmez — sadece URL'i bilir.
//   "Hangi action yavaş?" sorusuna RouteData ile cevap veriyoruz.
// ─────────────────────────────────────────────────────────────────────
public class PerformansFilter : IAsyncActionFilter
{
    // Kaç ms'den uzun süren action'lar uyarı alsın?
    // Gerçek projede appsettings.json'dan IOptions<T> ile okunur (Gün 16).
    private const int UyariEsigiMs = 500;

    private readonly ILogger<PerformansFilter> _logger;

    public PerformansFilter(ILogger<PerformansFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // Stopwatch → DateTime.UtcNow'dan çok daha hassas.
        // DateTime farkı milisaniye hassasiyetindeyken Stopwatch
        // CPU tick'i sayar — kısa süreler için doğru ölçüm verir.
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await next(); // action çalışır

        sw.Stop();
        var sure = sw.ElapsedMilliseconds;

        var controller = context.RouteData.Values["controller"];
        var action     = context.RouteData.Values["action"];

        if (sure > UyariEsigiMs)
        {
            // LogWarning → izleme araçlarında alert üretir.
            // Bu log satırı "yavaş action" dashboard'ında görünür.
            _logger.LogWarning(
                "[Performans] YAVAŞ: {Controller}/{Action} {Sure}ms " +
                "(eşik: {Esik}ms) | Path: {Path}",
                controller, action,
                sure, UyariEsigiMs,
                context.HttpContext.Request.Path);
        }
        else
        {
            // Normal durum — Debug seviyesinde log.
            // Varsayılan log seviyesi Information olduğu için
            // production'da bu satır hiç yazılmaz, performans kaybı yok.
            _logger.LogDebug(
                "[Performans] {Controller}/{Action} {Sure}ms",
                controller, action, sure);
        }
    }
}
