using Microsoft.AspNetCore.Mvc.Filters;

namespace KitabeviMVC.Filters;

// ─────────────────────────────────────────────────────────────────────
// Gün 19 — Audit Log Filter
//
// Gerçek ihtiyaç: Bankacılık, sağlık, e-ticaret gibi sektörlerde
// "kim, ne zaman, hangi kaynağa, ne yaptı" kaydı tutmak yasal zorunluluk.
//
// Neden middleware değil?
//   Middleware URL'i ve HTTP methodunu bilir ama:
//   - Kullanıcı kimliğine (User.Identity) erişimi daha zordur
//   - RouteData'ya (controller, action, id) erişimi yoktur
//   - ActionArguments'a (form'dan gelen model) erişimi yoktur
//   Filter bu bilgilerin hepsine ActionExecutingContext üzerinden ulaşır.
// ─────────────────────────────────────────────────────────────────────
public class AuditFilter : IAsyncActionFilter
{
    private readonly ILogger<AuditFilter> _logger;

    public AuditFilter(ILogger<AuditFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // ── Audit: kim, ne istiyor, hangi kayıt? ──────────────────

        // context.HttpContext.User.Identity?.Name →
        //   giriş yapmış kullanıcının adı (cookie veya JWT'den gelir).
        //   Giriş yapılmamışsa null — "??" ile "Anonim" fallback.
        var kullanici = context.HttpContext.User.Identity?.Name ?? "Anonim";

        // context.RouteData.Values → URL'den parse edilmiş segmentler.
        // /kitaplar/sil/42 → { controller: "Kitap", action: "Sil", id: "42" }
        var controller = context.RouteData.Values["controller"];
        var action     = context.RouteData.Values["action"];
        var id         = context.RouteData.Values["id"]; // yoksa null — sorun değil

        // context.HttpContext.Request.Method → "GET", "POST", "DELETE" vs.
        var method = context.HttpContext.Request.Method;

        // Action çalışmadan önce "kim ne yapmaya çalışıyor" kaydını at.
        // Gerçek projede bu satır DB'ye (AuditLog tablosu) veya
        // merkezi log sistemine (Elasticsearch, Seq) gider.
        _logger.LogInformation(
            "[Audit] {Kullanici} | {Method} {Controller}/{Action} | id={Id}",
            kullanici, method, controller, action, id);

        // ── Action çalışır ─────────────────────────────────────────
        var executed = await next();

        // ── Audit: sonuç ne oldu? ──────────────────────────────────

        if (executed.Exception is not null)
        {
            // Action exception fırlattı — sonucu hata olarak kaydet.
            // GlobalHataFilter ayrıca bu hatayı yakalayıp işleyecek.
            _logger.LogWarning(
                "[Audit] {Kullanici} | {Method} {Controller}/{Action} | id={Id} → HATA: {HataMesaji}",
                kullanici, method, controller, action, id,
                executed.Exception.Message);
        }
        else
        {
            // executed.Result → action'ın döndürdüğü IActionResult
            // Örnek: "RedirectToActionResult", "ViewResult", "NotFoundResult"
            var sonuc = executed.Result?.GetType().Name ?? "-";

            _logger.LogInformation(
                "[Audit] {Kullanici} | {Method} {Controller}/{Action} | id={Id} → {Sonuc}",
                kullanici, method, controller, action, id, sonuc);
        }
    }
}
