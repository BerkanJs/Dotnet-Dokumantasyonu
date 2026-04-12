using System.Diagnostics; // Stopwatch için — bunu yazmasaydık elapsed süreyi ölçemezdik
using Microsoft.Extensions.Logging; // ILogger için — bunu yazmasaydık loglama yapamaz, hata ayıklama körleşirdi

namespace KitabeviMVC.Services.HttpHandlers;

/// <summary>
/// Her giden HTTP isteğini ve gelen yanıtı otomatik olarak loglar.
/// Cross-cutting concern: bu loglama mantığını her servis sınıfına yazmak yerine
/// bir kez burada tanımlayıp tüm HTTP çağrılarına uygulanır.
/// </summary>
public class LoggingHandler : DelegatingHandler
// DelegatingHandler: HttpMessageHandler zincirinde bir halka oluşturur.
// Bunu yazmassaydık bu sınıf DI pipeline'ına dahil edilemezdi;
// her servis kendi içinde manuel loglama yapmak zorunda kalırdı.
{
    private readonly ILogger<LoggingHandler> _logger;
    // ILogger: ASP.NET Core'un yerleşik loglama soyutlaması.
    // Bunu inject etmeseydik: Console.WriteLine kullanmak zorunda kalırdık;
    // log level (Debug/Info/Warning), structured logging, Serilog entegrasyonu çalışmazdı.

    public LoggingHandler(ILogger<LoggingHandler> logger)
    {
        _logger = logger;
        // Constructor injection: bağımlılık dışarıdan verilir, sınıf kendi logger'ını oluşturmaz.
        // Bunu yazmassaydık (new ILogger yapamayız zaten) loglama imkansız olurdu.
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,   // giden HTTP isteği: method, URL, headers, body
        CancellationToken cancellationToken) // işlem iptal sinyali: timeout veya kullanıcı isteği
    // override: DelegatingHandler'ın bu metodu bizim gerçek iş yaptığımız nokta.
    // Bunu override etmeseydik handler hiçbir şey yapmaz, sadece isteği geçirirdi.
    {
        // ── İstek gitmeden önce: giden isteği logla ──────────────────────────────

        var requestId = Guid.NewGuid().ToString("N")[..8];
        // Kısa bir istek kimliği oluşturur (örn: "a3f9b12c").
        // Bunu yazmassaydık birden fazla eş zamanlı istek log'da karışır,
        // hangi yanıtın hangi isteğe ait olduğunu anlayamazdık.

        _logger.LogInformation(
            "[HTTP-OUT] [{RequestId}] {Method} {Uri}",
            requestId,
            request.Method,        // GET, POST, PUT, DELETE vs.
            request.RequestUri);   // tam URL — bunu loglamasaydık hangi endpoint'e gittiğimizi bilemezdik
        // LogInformation: structured log — Serilog veya Seq ile filtrelenebilir.
        // Bunu LogDebug yapmak isteseydik; prod ortamında Info seviyesinde görünmez olurdu.

        // ── Süre ölçümünü başlat ─────────────────────────────────────────────────

        var stopwatch = Stopwatch.StartNew();
        // Stopwatch: yüksek çözünürlüklü zamanlayıcı, DateTime.Now'dan çok daha hassas.
        // Bunu yazmassaydık yanıt süresini ölçemezdik; yavaş endpoint'leri tespit edemezdik.

        HttpResponseMessage response;
        // response: dış servisten dönen HTTP yanıtı.

        try
        {
            response = await base.SendAsync(request, cancellationToken);
            // base.SendAsync: zincirdeki bir sonraki handler'a (veya gerçek ağa) isteği gönderir.
            // Bu MUTLAKA çağrılmalıdır — bunu çağırmasaydık istek hiç ağa çıkmaz,
            // handler sadece isteği yutar ve uygulama sessizce çalışmaz hale gelirdi.
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            // Hata durumunda da süreyi ölçmeyi durdurmak iyi pratiktir.

            _logger.LogError(
                ex,
                "[HTTP-ERR] [{RequestId}] {Method} {Uri} — {ElapsedMs}ms içinde başarısız",
                requestId,
                request.Method,
                request.RequestUri,
                stopwatch.ElapsedMilliseconds);
            // Ağ hatası, timeout, DNS çözümlenememesi gibi durumları yakalar.
            // Bunu catch etmeseydik hata loglanmadan üst katmana fırlatılır;
            // neyin ne zaman başarısız olduğunu anlayamazdık.

            throw;
            // Exception'ı yeniden fırlat: biz sadece loglama yapıyoruz, kararı üst katman verir.
            // Bunu yazmasaydık hata yutulur, çağıran kod başarılı sanırdı — çok tehlikeli.
        }

        // ── Yanıt geldikten sonra: yanıtı logla ─────────────────────────────────

        stopwatch.Stop();
        // Süre ölçümünü durdur: tam elapsed değerine ulaşılır.
        // Bunu çağırmasaydık ElapsedMilliseconds değeri anlık değil sürekli artardı.

        var statusCode = (int)response.StatusCode;
        // int cast: 200, 404, 500 gibi sayısal kodu alır — LogLevel kararı için kullanılır.
        // Bunu yazmassaydık sadece "OK", "NotFound" gibi enum adını loglamak zorunda kalırdık.

        if (statusCode >= 500)
        {
            _logger.LogError(
                "[HTTP-IN]  [{RequestId}] {StatusCode} — {ElapsedMs}ms (SUNUCU HATASI)",
                requestId, statusCode, stopwatch.ElapsedMilliseconds);
            // 5xx: sunucu tarafı hata — Error seviyesinde logla, alert tetiklenebilsin.
        }
        else if (statusCode >= 400)
        {
            _logger.LogWarning(
                "[HTTP-IN]  [{RequestId}] {StatusCode} — {ElapsedMs}ms (İSTEMCİ HATASI)",
                requestId, statusCode, stopwatch.ElapsedMilliseconds);
            // 4xx: istemci hatası (404, 401 vs.) — Warning seviyesinde logla.
            // Bunu Error yapmamalıyız: 404 normal bir iş akışı olabilir (kitap bulunamadı).
        }
        else
        {
            _logger.LogInformation(
                "[HTTP-IN]  [{RequestId}] {StatusCode} — {ElapsedMs}ms",
                requestId, statusCode, stopwatch.ElapsedMilliseconds);
            // 2xx/3xx: başarılı — sadece bilgi amaçlı logla.
        }

        return response;
        // İşlenmiş yanıtı zincirde bir üstteki katmana (çağıran koda) döndür.
        // Bunu yazmassaydık compiler hatası alırdık; ya da null dönseydi NullReferenceException.
    }
}
