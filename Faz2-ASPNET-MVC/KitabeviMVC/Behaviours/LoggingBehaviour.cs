using MediatR;
using System.Diagnostics;

namespace KitabeviMVC.Behaviours;

// Gün 35: MediatR Pipeline Behaviour — her Send() çağrısında otomatik devreye girer.
// Her handler'a ayrı ayrı log yazmak yerine tek noktada halleder.
// IPipelineBehavior<TRequest, TResponse>: tüm request/response çiftleri için generic.
public class LoggingBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,  // pipeline'daki bir sonraki adım (gerçek handler)
        CancellationToken ct)
    {
        var ad = typeof(TRequest).Name;

        _logger.LogInformation("→ {Request} başladı", ad);
        // bunu her handler'a yazsaydık: 20 handler → 20 kez aynı loglama kodu

        var sw = Stopwatch.StartNew();

        var result = await next();
        // next(): bir sonraki behaviour veya asıl handler çalışır
        // bunu await etmeseydin: asıl iş bitmeden süre ölçülürdü

        sw.Stop();

        if (sw.ElapsedMilliseconds > 500)
            _logger.LogWarning("← {Request} YAVAŞ: {Ms}ms", ad, sw.ElapsedMilliseconds);
            // 500ms eşiği: yavaş handler'ları tespit et, optimize et
        else
            _logger.LogInformation("← {Request} {Ms}ms", ad, sw.ElapsedMilliseconds);

        return result;
    }
}
