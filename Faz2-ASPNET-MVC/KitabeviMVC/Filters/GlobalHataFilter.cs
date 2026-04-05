using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KitabeviMVC.Filters;

// ─────────────────────────────────────────────────────────────────────
// Gün 19 — IExceptionFilter
//
// Action veya diğer filter'lardan fırlatılan yakalanmamış hataları yakalar.
// Her action'a try/catch yazmak yerine bu tek filter tüm hataları yönetir.
//
// Neleri YAKALAMAZ:
//   - Middleware katmanındaki hatalar (UseExceptionHandler onları yakalar)
//   - Routing öncesi hatalar
//
// Bu yüzden Program.cs'de hem bu filter hem UseExceptionHandler birlikte var:
//   UseExceptionHandler → genel fallback (middleware hataları)
//   Bu filter          → controller/action hataları, tip bazlı ayrıştırma
// ─────────────────────────────────────────────────────────────────────
public class GlobalHataFilter : IExceptionFilter
{
    private readonly ILogger<GlobalHataFilter> _logger;
    private readonly IWebHostEnvironment _env;

    // IWebHostEnvironment → uygulamanın hangi ortamda çalıştığını bildirir.
    // Development'ta stack trace göstermek için kullanacağız.
    public GlobalHataFilter(ILogger<GlobalHataFilter> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env    = env;
    }

    public void OnException(ExceptionContext context)
    {
        var hata = context.Exception;

        _logger.LogError(
            hata,
            "[HataFilter] Yakalanmamış hata: {Tip} — {Mesaj}",
            hata.GetType().Name,
            hata.Message);

        // "switch expression" (C# 8+) — her satır "tip => sonuç" kalıbı.
        // Hata tipine göre HTTP status kodu ve başlık belirleniyor.
        // "_" (underscore) → default durum, diğer tüm tipler.
        var (statusKod, baslik) = hata switch
        {
            KeyNotFoundException        => (StatusCodes.Status404NotFound,
                                           "İstenen kaynak bulunamadı"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden,
                                           "Bu işlem için yetkiniz yok"),
            ArgumentException           => (StatusCodes.Status400BadRequest,
                                           "Geçersiz istek parametresi"),
            _                           => (StatusCodes.Status500InternalServerError,
                                           "Sunucu tarafında beklenmedik bir hata oluştu")
        };

        // ProblemDetails — RFC 9457 standardı.
        // Hem tarayıcı hem API istemcisi bu formatta hata yanıtı bekler.
        var problem = new ProblemDetails
        {
            Status   = statusKod,
            Title    = baslik,
            // context.HttpContext.Request.Path → "/kitaplar/detay/999"
            Instance = context.HttpContext.Request.Path
        };

        // Development ortamında hata detayını da ekle — Production'da gizle.
        // _env.IsDevelopment() → ASPNETCORE_ENVIRONMENT=Development ise true.
        if (_env.IsDevelopment())
        {
            // "Detail" → ProblemDetails'in opsiyonel açıklama alanı
            problem.Detail = hata.ToString(); // stack trace dahil tam hata metni
        }

        // ObjectResult: herhangi bir nesneyi HTTP yanıtı olarak sarmalar.
        // StatusCode'u burada set etmek zorundayız — ProblemDetails.Status
        // property'si sadece JSON'a yazılır, HTTP status kodunu set etmez.
        context.Result = new ObjectResult(problem)
        {
            StatusCode = statusKod
        };

        // true → "bu hatayı ben hallettim, bir üstteki middleware'e iletme"
        // false bırakırsak hata UseExceptionHandler'a da düşer.
        context.ExceptionHandled = true;
    }
}
