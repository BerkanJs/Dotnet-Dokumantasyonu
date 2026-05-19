// GÜN 107 — CORS ve CSRF
// CORS: browser güvenliği — farklı origin'den gelen istek engellenebilir
// CSRF: sahte istek saldırısı — kullanıcının cookie'si ile istek gönderilir

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Ornekler.gun107;

// --- 1. CORS kurulum ---
public static class CorsSetup
{
    public static void Kaydet(WebApplicationBuilder builder)
    {
        builder.Services.AddCors(opt =>
        {
            // Production: sadece kendi origin'lerine izin ver
            opt.AddPolicy("Production", policy => policy
                // ne yapar: sadece bu origin'ler API'ya istek atabilir
                // bunu yazmasaydık: AllowAnyOrigin() → herhangi bir site API'ı kullanabilirdi
                .WithOrigins(
                    "https://app.kitabevi.com",
                    "https://www.kitabevi.com")
                .WithMethods("GET", "POST", "PUT", "DELETE")
                .WithHeaders("Authorization", "Content-Type", "X-Correlation-ID")
                // ne yapar: cookie/Authorization header'ın cross-origin isteklerle gönderilmesine izin verir
                // bunu yazmasaydık: tarayıcı JWT token içeren header'ı göndermezdi
                .AllowCredentials()
                // ne yapar: preflight sonucunu 1 saat cache'le — her istek için OPTIONS atma
                // bunu yazmasaydık: her POST/PUT isteği öncesinde OPTIONS isteği giderdi → 2x round-trip
                .SetPreflightMaxAge(TimeSpan.FromHours(1)));

            // Development: her origin'e izin ver
            opt.AddPolicy("Development", policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
        });
    }

    public static void Konfigur(WebApplication app)
    {
        var env = app.Environment;
        // ne yapar: ortama göre doğru CORS policy'yi uygular
        // SIRALAMA ÖNEMLİ: UseRouting'den ÖNCE, UseAuthentication'dan ÖNCE
        app.UseCors(env.IsDevelopment() ? "Development" : "Production");
    }
}

// --- 2. CSRF koruması: Anti-Forgery Token ---
// CSRF saldırısı:
// 1. Kullanıcı kitabevi.com'a giriş yapar → cookie set edilir
// 2. Saldırgan bank.evil.com açar → kitabevi.com/siparis-ver endpoint'ine POST atar
// 3. Browser cookie'yi otomatik ekler → sipariş verilir!

// Çözüm: her form'a gizli token ekle → sunucu doğrular
public static class CsrfSetup
{
    public static void Kaydet(WebApplicationBuilder builder)
    {
        // ne yapar: anti-forgery servisini kaydeder
        // bunu yazmasaydık: CSRF token oluşturma/doğrulama servisi olmaz
        builder.Services.AddAntiforgery(opt =>
        {
            opt.HeaderName = "X-CSRF-TOKEN";    // SPA'lar header ile gönderir
            opt.Cookie.Name = "XSRF-TOKEN";     // Angular/Axios otomatik okur
            // ne yapar: cookie'yi JavaScript'in okuyabileceği şekilde set et
            // bunu yazmasaydık: SPA framework token'ı okuyamazdı
            opt.Cookie.HttpOnly = false;
        });
    }
}

// --- 3. SameSite cookie: CSRF'e karşı ek katman ---
public static class SameSiteCookieSetup
{
    public static void Kaydet(WebApplicationBuilder builder)
    {
        builder.Services.ConfigureApplicationCookie(opt =>
        {
            // ne yapar: cookie sadece aynı site isteklerinde gönderilir
            // bunu yazmasaydık: cross-site POST isteğinde cookie otomatik eklenirdi → CSRF açığı
            opt.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
            opt.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            opt.Cookie.HttpOnly = true; // ne yapar: JavaScript cookie'ye erişemez → XSS koruması
        });
    }
}

// --- 4. Minimal API'da CSRF doğrulama ---
public static class CsrfEndpoints
{
    public static void Map(WebApplication app)
    {
        // ne yapar: CSRF token endpoint'i — SPA başlarken bu token'ı alır
        app.MapGet("/antiforgery/token",
            (Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext ctx) =>
            {
                // ne yapar: istemciye CSRF token döner
                // bunu yazmasaydık: SPA token'ı nereden alacağını bilemezdi
                var token = antiforgery.GetAndStoreTokens(ctx);
                return Results.Ok(new { token = token.RequestToken });
            });

        app.MapPost("/siparis",
            // ne yapar: bu endpoint'te CSRF token doğrulaması zorunlu
            // bunu yazmasaydık: başka siteden POST → sahte sipariş
            [ValidateAntiForgeryToken] async (SiparisIstegi istek) =>
            {
                return Results.Ok("Sipariş oluşturuldu");
            });
    }
}

public record SiparisIstegi(int KitapId, int Adet);
