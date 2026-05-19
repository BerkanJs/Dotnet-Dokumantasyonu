using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KitabeviMediatr.Application.Behaviors;

public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
//    ↑ generic: her request/response tipi için çalışır
//      bunu yazmasaydık → her handler'a ayrı logging yazmak zorunda kalırdık
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next, // ← sonraki pipeline adımı
        CancellationToken ct)
    {
        var requestAdi = typeof(TRequest).Name;

        _logger.LogInformation("→ {Request} başladı: {@Payload}", requestAdi, request);

        var sw = Stopwatch.StartNew();
        var response = await next();
        //                    ↑ bir sonraki behavior veya asıl handler çağrılıyor
        //                      bunu yazmasaydık → request handler'a hiç ulaşmazdı
        sw.Stop();

        _logger.LogInformation("← {Request} tamamlandı: {Ms}ms", requestAdi, sw.ElapsedMilliseconds);

        return response;
    }
}
