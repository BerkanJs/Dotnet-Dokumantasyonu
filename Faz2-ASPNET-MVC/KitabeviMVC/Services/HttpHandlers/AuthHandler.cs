using System.Net.Http.Headers; // AuthenticationHeaderValue için — bunu yazmasaydık Bearer token header formatını manuel string ile kurmak zorunda kalırdık

namespace KitabeviMVC.Services.HttpHandlers;

// ─────────────────────────────────────────────────────────────────────────────
// TOKEN SERVİSİ SÖZLEŞME (INTERFACE)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Kimlik doğrulama token'ı sağlayan servisler için sözleşme.
/// Interface olarak tanımlanması: AuthHandler'ın gerçek token servisine bağımlı olmamasını sağlar.
/// Test sırasında ITokenServisi'ni mock'layarak gerçek OAuth sunucusuna ihtiyaç duymadan
/// test edebiliriz.
/// </summary>
public interface ITokenServisi
{
    /// <summary>
    /// Geçerli bir Bearer token döndürür.
    /// Impl. kararı: önbellekten mi çekecek, süresi dolmuşsa yenilecek mi —
    /// bu detaylar implementasyona bırakılır, AuthHandler bunları bilmez.
    /// </summary>
    Task<string> TokenGetirAsync();
    // Task<string>: asenkron — token alma işlemi ağa gidebilir (OAuth2 token endpoint'i).
    // Bunu senkron yapsaydık: async pipeline içinde blocking call → deadlock riski.
}

// ─────────────────────────────────────────────────────────────────────────────
// AUTH HANDLER
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Her giden HTTP isteğine otomatik olarak Bearer token ekler.
///
/// Neden DelegatingHandler olarak? Çünkü token ekleme mantığı her HTTP çağrısında
/// tekrarlanır. Bu handler'ı bir kez kaydet, tüm çağrılara otomatik uygulanır.
/// Alternatif: her servis metodunda manuel token ekleme → DRY ihlaali, unutma riski.
/// </summary>
public class AuthHandler : DelegatingHandler
// DelegatingHandler: HttpMessageHandler zincirinin bir halkası.
// Bunu yazmassaydık sınıf AddHttpMessageHandler<AuthHandler>() ile kayıt edilemezdi.
{
    private readonly ITokenServisi _tokenServisi;
    // ITokenServisi: somut implementasyon değil interface inject ediliyor.
    // Bunu ITokenServisi yerine somut sınıf yapsaydık: test sırasında mock'layamazdık,
    // gerçek OAuth sunucusuna bağımlı hale gelirdik.

    public AuthHandler(ITokenServisi tokenServisi)
    {
        _tokenServisi = tokenServisi
            ?? throw new ArgumentNullException(nameof(tokenServisi));
        // null guard: DI container doğru yapılandırılmamışsa hemen anlaşılır hata.
        // Bunu yazmasaydık NullReferenceException ileride, beklenmedik bir yerde fırlatılırdı.
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    // override: bu metodun gövdesi handler'ın tüm işini yapar.
    // Bunu override etmemeseydik handler sadece isteği geçirir, token eklemezdi.
    {
        // ── 1. Token al ──────────────────────────────────────────────────────────

        var token = await _tokenServisi.TokenGetirAsync();
        // await: token alma işlemi tamamlanana kadar thread bloke olmadan bekler.
        // Bunu senkron yapmak (.Result) isteseydik: ASP.NET Core thread pool'unu bloke eder,
        // yüksek yük altında deadlock veya thread starvation oluşurdu.

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "AuthHandler: ITokenServisi boş token döndürdü. " +
                "Token servisi yapılandırmasını kontrol edin.");
            // Boş token ile istek göndermek 401 alır, ancak hangi katmanda sorun olduğu belirsiz kalır.
            // Bunu erken fırlatmak: hatanın kaynağı hemen belli olur.
        }

        // ── 2. Authorization header'ı ekle ──────────────────────────────────────

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // AuthenticationHeaderValue: RFC 7235 uyumlu "Bearer <token>" formatını doğru kurar.
        // Manuel string kullansaydık: request.Headers.Add("Authorization", "Bearer " + token)
        // — bu çalışır ama header zaten varsa ArgumentException fırlatır; tip güvensizdir.
        // Bunu hiç yazmasaydık: dış API 401 Unauthorized dönerdi, hata izlemek zorlaşırdı.

        // ── 3. İsteği zincire gönder ─────────────────────────────────────────────

        var response = await base.SendAsync(request, cancellationToken);
        // base.SendAsync: bir sonraki handler'a (veya gerçek ağa) isteği ilet.
        // Bu MUTLAKA çağrılmalıdır — çağırmasaydık istek hiç gitmez.

        // ── 4. 401 durumunda token yenileme denemesi (opsiyonel ama önemli) ─────

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Senaryo: token önbellekte taze görünüyordu ama sunucu geçersiz saydı.
            // Çözüm: token'ı yenile ve bir kez daha dene.

            response.Dispose();
            // Eski yanıtı dispose et: HttpContent kaynaklarını serbest bırak.
            // Bunu yazmasaydık: dispose edilmemiş HttpResponseMessage birikir, bellek sızıntısı.

            var yeniToken = await _tokenServisi.TokenGetirAsync();
            // Token servisinin önbelleği temizlemesi beklenir; implementasyona bırakılır.

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", yeniToken);
            // Yeni token ile header'ı güncelle.

            response = await base.SendAsync(request, cancellationToken);
            // İkinci deneme — bunu yazmassaydık: kullanıcı 401 alır, token yenilenmez,
            // her işlemde manuel yeniden giriş gerekir.
        }

        return response;
        // Nihai yanıtı (başarılı veya başarısız) çağıran koda döndür.
    }
}
