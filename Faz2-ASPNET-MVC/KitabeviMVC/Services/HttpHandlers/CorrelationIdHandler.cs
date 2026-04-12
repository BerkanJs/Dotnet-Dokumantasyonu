namespace KitabeviMVC.Services.HttpHandlers;

/// <summary>
/// Her giden HTTP isteğine X-Correlation-Id header'ı ekler.
///
/// Neden gerekli?
///   Bir kullanıcı işlemi birden fazla servisi çağırır:
///   KitabeviMVC → FiyatServisi → StokServisi → ÖdemeServisi
///   Her servis ayrı log üretir. Hata olduğunda hangi isteğin hangi
///   log satırına karşılık geldiğini bulmak imkansız olur.
///   Correlation ID sayesinde tüm servislerdeki log satırları tek bir
///   işlem kimliğiyle ilişkilendirilir → distributed tracing mümkün olur.
///
/// Gerçek hayat örneği:
///   Kullanıcı "Sipariş ver" butonuna bastı → istek ID: "abc-123"
///   KitabeviMVC logu: "Sipariş alındı [abc-123]"
///   StokServisi logu: "Stok düşüldü [abc-123]"
///   ÖdemeServisi logu: "Ödeme başarısız [abc-123]"
///   → Tek sorguda tüm zinciri takip edebilirsin.
/// </summary>
public class CorrelationIdHandler : DelegatingHandler
// DelegatingHandler: HTTP pipeline'ına eklenen ara katman.
// Bunu yazmassaydık bu sınıf AddHttpMessageHandler() ile kayıt edilemez,
// pipeline'a dahil edilemezdi.
{
    // Header adı sabit — tüm servisler aynı header adını kullanmalı.
    // Bunu yazmassaydık bazı servisler "X-Request-Id" bazıları "Correlation-Id" kullanır,
    // log sorguları tutarsız olurdu.
    private const string CorrelationIdHeaderAdi = "X-Correlation-Id";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // ── Mevcut Correlation ID'yi koru ya da yenisini oluştur ──────────────────

        if (!request.Headers.Contains(CorrelationIdHeaderAdi))
        {
            // İstek zaten bir Correlation ID taşımıyorsa (ilk servis çağrısı):
            // yeni bir UUID oluştur ve ekle.
            var yeniId = Guid.NewGuid().ToString();
            // Guid.NewGuid(): küresel olarak benzersiz kimlik, çakışma ihtimali astronomik düzeyde düşük.
            // ToString(): "3f2504e0-4f89-11d3-9a0c-0305e82c3301" formatında string üretir.
            // Bunu yazmassaydık her dış servis çağrısı farklı (veya hiç olmayan) kimlikle gider.

            request.Headers.Add(CorrelationIdHeaderAdi, yeniId);
            // Header'ı isteğe ekle — dış servis bu header'ı okuyabilir.
            // Bunu yazmassaydık dış servis correlation ID'yi bilemez,
            // kendi loglarında bu işlemi izole edemez.
        }
        // else: Üst katmandan (ör. başka bir middleware) gelen ID varsa koruyoruz.
        // Bu sayede A → B → C zincirinde ID değişmez, tüm hop'larda aynı kalır.

        // ── İsteği bir sonraki handler'a (veya gerçek ağa) gönder ────────────────
        return await base.SendAsync(request, cancellationToken);
        // base.SendAsync: bu çağrı yapılmazsa istek asla ağa çıkmaz.
        // Handler zinciri: CorrelationIdHandler → LoggingHandler → AuthHandler → Ağ
        // Sıra önemlidir: Correlation ID eklendikten SONRA loglama çalışırsa
        // log satırlarında ID görünür.
    }
}
