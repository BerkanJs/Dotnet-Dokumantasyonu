using FluentValidation;
using KitabeviMediatr.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace KitabeviMediatr.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
//                                    ↑ ASP.NET Core 8 yerleşik interface
//                                      bunu yazmasaydık → her Controller try/catch yazardı
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx,
        Exception exception,
        CancellationToken ct)
    {
        var (statusCode, baslik) = exception switch
        {
            DomainException     => (400, "İş Kuralı Hatası"),
            ValidationException => (422, "Doğrulama Hatası"),
            _                   => (500, "Sunucu Hatası")
        };
        // ↑ switch expression: exception tipine göre HTTP kodu
        //   bunu yazmasaydık → her Controller ayrı try/catch yazardı, kod tekrarı olurdu
        //   Gün59'da: SiparisController'da catch (DomainException) vardı
        //   Gün60'da: tüm hata yönetimi tek yerde — tutarlı hata formatı

        if (statusCode == 500)
            _logger.LogError(exception, "Beklenmedik hata");

        ctx.Response.StatusCode = statusCode;

        await ctx.Response.WriteAsJsonAsync(new
        {
            tip = exception.GetType().Name,
            baslik,
            mesaj = exception.Message,
            // 500 hatalarında stack trace döndürme — güvenlik
            detay = statusCode < 500 ? exception.Message : null
        }, ct);

        return true;
        // ↑ true: "ben hallettim, başkası bakmasın"
        //   false dönseydi → ASP.NET Core kendi default handler'ına devam ederdi
    }
}
