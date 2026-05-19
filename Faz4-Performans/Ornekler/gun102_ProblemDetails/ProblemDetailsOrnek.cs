// GÜN 102 — Problem Details (RFC 9457) ve Global Exception Handler
// Tüm API hataları tek formatta → istemciler kolayca parse edebilir

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Ornekler.gun102;

// --- 1. Program.cs kurulum ---
public static class ProblemDetailsSetup
{
    public static void Kaydet(WebApplicationBuilder builder)
    {
        // ne yapar: ProblemDetails servisini DI'a kaydeder, özelleştirme imkanı tanır
        // bunu yazmasaydık: exception handler manuel ProblemDetails objesi oluşturmak zorunda kalırdı
        builder.Services.AddProblemDetails(opt =>
        {
            opt.CustomizeProblemDetails = ctx =>
            {
                // ne yapar: her ProblemDetails response'una instance ve requestId ekler
                // bunu yazmasaydık: hangi isteğin hata verdiğini loglarla eşleştiremezdik
                ctx.ProblemDetails.Instance =
                    $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
                ctx.ProblemDetails.Extensions["requestId"] =
                    ctx.HttpContext.TraceIdentifier;
            };
        });

        // ne yapar: global exception handler'ı DI'a kaydeder
        // bunu yazmasaydık: her controller'da try-catch yazmak zorunda kalırdık
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    }

    public static void Konfigur(WebApplication app)
    {
        // ne yapar: işlenmeyen exception'ları yakalar, ProblemDetails olarak döner
        // bunu yazmasaydık: unhandled exception → 500 + HTML error page (JSON API için yanlış)
        app.UseExceptionHandler();
        app.UseStatusCodePages(); // ne yapar: 404/405 gibi HTTP hatalarını da ProblemDetails'e çevirir
    }
}

// --- 2. Global Exception Handler ---
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // ne yapar: exception tipine göre HTTP status kodu belirler
        // bunu yazmasaydık: her exception 500 olurdu — istemciler hata tipini bilemezdi
        var (statusCode, title) = exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Kaynak bulunamadı"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Yetkisiz erişim"),
            ValidationException ve => (StatusCodes.Status422UnprocessableEntity, "Doğrulama hatası"),
            _ => (StatusCodes.Status500InternalServerError, "Sunucu hatası")
        };

        // ne yapar: hata logla ama hassas detayları response'a yazma
        // bunu yazmasaydık: stack trace istemciye gönderilirdi — güvenlik açığı
        _logger.LogError(exception, "İşlenmeyen hata: {Message}", exception.Message);

        httpContext.Response.StatusCode = statusCode;

        // ne yapar: RFC 9457 formatında JSON yanıt yazar
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,  // production'da sadece safe mesajlar
            Type = $"https://api.kitabevi.com/errors/{statusCode}"
        }, cancellationToken);

        return true; // ne yapar: exception handle edildi — pipeline devam etme
    }
}

// --- 3. ValidationProblemDetails: field bazlı hata ---
public static class ValidationHatasi
{
    public static IResult DondurValidationHatasi(Dictionary<string, string[]> hatalar)
    {
        // ne yapar: field bazlı validation hataları için standart format
        // bunu yazmasaydık: frontend hangi alanın hatalı olduğunu bilemezdi
        return Results.ValidationProblem(hatalar,
            title: "Girdi doğrulama hatası",
            type: "https://api.kitabevi.com/errors/validation");

        // Response:
        // {
        //   "type": "https://api.kitabevi.com/errors/validation",
        //   "title": "Girdi doğrulama hatası",
        //   "status": 422,
        //   "errors": {
        //     "ad": ["Ad alanı zorunlu"],
        //     "fiyat": ["Fiyat pozitif olmalı"]
        //   }
        // }
    }
}

public class ValidationException : Exception
{
    public ValidationException(string msg) : base(msg) { }
}
