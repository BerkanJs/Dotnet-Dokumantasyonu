using System.Security.Claims;
using KitabeviMVC.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace KitabeviMVC.Controllers;

// Gün 20: Authentication — kullanıcı girişi ve çıkışı.
//
// Gerçek projede kullanıcılar veritabanından çekilir (ASP.NET Core Identity veya özel tablo).
// Burada iki sabit kullanıcı ile kavramı gösteriyoruz:
//   ali@kitabevi.com  / sifre123  → Admin rolü
//   veli@kitabevi.com / sifre456  → Editor rolü
//
// Spring Security karşılığı:
//   LoginController + UserDetailsService + AuthenticationManager
[Route("hesap")]
public class HesapController : Controller
{
    private readonly ILogger<HesapController> _logger;

    public HesapController(ILogger<HesapController> logger)
    {
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /hesap/giris → giriş formunu göster
    //
    // "returnUrl" → [Authorize] ile engellenen sayfanın adresi.
    // Cookie middleware bunu query string'e otomatik ekler:
    //   /hesap/giris?ReturnUrl=%2Fkitaplar%2Fekle
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("giris")]
    public IActionResult Giris(string? returnUrl = null)
    {
        // Zaten giriş yapmışsa direkt gönder
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new GirisViewModel { ReturnUrl = returnUrl });
    }

    // ─────────────────────────────────────────────────────────────────
    // POST /hesap/giris → formu doğrula, oturum aç
    // ─────────────────────────────────────────────────────────────────
    [HttpPost("giris")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Giris(GirisViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // ── Kullanıcıyı doğrula ────────────────────────────────────────
        // Gerçek projede: await _kullaniciServisi.DogrulaAsync(model.Eposta, model.Sifre)
        // Şifre hash'lenmiş halde DB'de saklanır, karşılaştırma hash üzerinden yapılır.
        var (gecerli, rol) = KullaniciyiDogrula(model.Eposta, model.Sifre);

        if (!gecerli)
        {
            // Hangi alanın yanlış olduğunu söyleme — güvenlik gereği.
            // "Email yanlış" veya "Şifre yanlış" demek kullanıcı adını açık eder.
            ModelState.AddModelError(string.Empty, "E-posta veya şifre hatalı.");
            return View(model);
        }

        // ── Claims oluştur ────────────────────────────────────────────
        // Claim: kullanıcı hakkında bilgi parçası — anahtar-değer çifti.
        // Bu bilgiler şifreli cookie'ye yazılır, her istekte otomatik okunur.
        var claims = new List<Claim>
        {
            // ClaimTypes.Name     → User.Identity.Name olarak okunur
            new(ClaimTypes.Name, model.Eposta),

            // ClaimTypes.NameIdentifier → kullanıcı ID'si (burada email, gerçekte DB id'si)
            new(ClaimTypes.NameIdentifier, model.Eposta),

            // ClaimTypes.Role → [Authorize(Roles = "Admin")] bu claim'i okur
            new(ClaimTypes.Role, rol),

            // Özel claim: kullanıcıya özel iş kuralları için
            // Örn: policy "emailOnaylandi" claim'ini zorunlu tutabilir
            new("emailOnaylandi", "true")
        };

        // "ClaimsIdentity" → bir kimlik kaynağını temsil eder.
        // "CookieAuthenticationDefaults.AuthenticationScheme" → cookie şeması seçildi.
        // Scheme, hangi middleware'in bu kimliği işleyeceğini belirtir.
        var kimlik = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        // "ClaimsPrincipal" → kullanıcının tamamı (birden fazla kimlik olabilir).
        // Controller'daki her action'dan "User" property'si ile erişilir.
        var kullanici = new ClaimsPrincipal(kimlik);

        // ── Cookie yaz ────────────────────────────────────────────────
        // "HttpContext.SignInAsync" → kullanıcıyı oturum açmış olarak işaretle.
        // Framework claims'i şifreler, cookie olarak tarayıcıya gönderir.
        // Sonraki her istekte bu cookie otomatik okunur, User nesnesi doldurulur.
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            kullanici,
            new AuthenticationProperties
            {
                // true → tarayıcı kapansa da cookie silinmez (beni hatırla)
                // false → oturum cookie'si — tarayıcı kapanınca silinir
                IsPersistent = false,

                // Cookie'nin geçerlilik süresi — Program.cs'deki ExpireTimeSpan'ı ezer
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        _logger.LogInformation("[Auth] Giriş yapıldı: {Eposta} | Rol: {Rol}", model.Eposta, rol);

        // ── Yönlendir ──────────────────────────────────────────────────
        // "Url.IsLocalUrl" → open redirect saldırısını önler.
        // ReturnUrl dış siteye işaret ediyorsa (http://evil.com) anasayfaya gönder.
        if (Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    // ─────────────────────────────────────────────────────────────────
    // POST /hesap/cikis → oturumu sonlandır
    //
    // GET değil POST — CSRF koruması için.
    // Tarayıcıda /hesap/cikis linkine gidilmesiyle oturum kapanmamalı.
    // ─────────────────────────────────────────────────────────────────
    [HttpPost("cikis")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cikis()
    {
        var eposta = User.Identity?.Name ?? "Bilinmeyen";

        // Cookie'yi sil — tarayıcıdan da, sunucu tarafındaki şifreli içerikten de
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        _logger.LogInformation("[Auth] Çıkış yapıldı: {Eposta}", eposta);

        return RedirectToAction("Index", "Home");
    }

    // ─────────────────────────────────────────────────────────────────
    // Yardımcı metod — gerçek projede bu servis katmanında olur
    // ─────────────────────────────────────────────────────────────────
    private static (bool gecerli, string rol) KullaniciyiDogrula(string eposta, string sifre)
    {
        // Gerçek projede: DB'den kullanıcıyı bul → hash karşılaştır
        return (eposta, sifre) switch
        {
            ("ali@kitabevi.com",  "sifre123") => (true, "Admin"),
            ("veli@kitabevi.com", "sifre456") => (true, "Editor"),
            _ => (false, string.Empty)
        };
    }
}
