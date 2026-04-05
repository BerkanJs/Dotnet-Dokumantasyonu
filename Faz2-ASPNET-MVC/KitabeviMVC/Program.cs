using Asp.Versioning;
using Hangfire;
using KitabeviMVC.Authorization;
using KitabeviMVC.BackgroundServices;
using KitabeviMVC.Endpoints;
using Scalar.AspNetCore;
using KitabeviMVC.Configuration;
using KitabeviMVC.Data;
using KitabeviMVC.Filters;
using KitabeviMVC.Middleware;
using KitabeviMVC.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────────
// Gün 19: Global Filter kayıtları
//
// options.Filters.Add<T>() → tüm controller ve action'lara uygulanır.
// Filter'lar burada kayıt sırasıyla çalışır: ActionLogFilter önce,
// GlobalHataFilter sonra — ama exception filter pipeline'ın sonunda
// zaten çalışır, sıra önemli değil.
//
// Not: ValidationFilter burada yok — sadece KitapController'da
// [TypeFilter] ile controller seviyesinde eklendi (KitapController.cs).
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews(options =>
{
    // Kim, ne zaman, hangi kaynağa, ne yaptı — yasal zorunluluk
    options.Filters.Add<AuditFilter>();

    // Yavaş action tespiti — 500ms eşiğini aşan action'lar uyarı üretir
    options.Filters.Add<PerformansFilter>();

    // Yakalanmamış hataları ProblemDetails formatında döndürür
    options.Filters.Add<GlobalHataFilter>();
});

// ─────────────────────────────────────────────────────────────────────
// ProblemDetails — RFC 9457 standardı.
// 4xx ve 5xx yanıtlarını otomatik olarak standart JSON formatına çevirir:
//   { "type": "...", "title": "...", "status": 404, "instance": "/..." }
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();

// ─────────────────────────────────────────────────────────────────────
// Gün 20: Authentication — Cookie tabanlı kimlik doğrulama.
//
// AddAuthentication() → hangi scheme varsayılan olarak kullanılacak?
// AddCookie()         → cookie'nin davranışını yapılandır.
//
// Spring Security karşılığı: httpSecurity.formLogin() + sessionManagement()
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Giriş yapılmamışken [Authorize] sayfasına gidilirse buraya yönlendir.
        // ASP.NET Core ReturnUrl'i query string'e otomatik ekler:
        //   /hesap/giris?ReturnUrl=%2Fkitaplar%2Fekle
        options.LoginPath = "/hesap/giris";

        // Giriş yapmış ama yetkisi yoksa (Forbid) buraya yönlendir.
        options.AccessDeniedPath = "/hesap/erisim-reddedildi";

        // Cookie ne kadar geçerli? 8 saat hareketsizlikte sona erer.
        options.ExpireTimeSpan = TimeSpan.FromHours(8);

        // true → her istekte süre uzatılır (sliding expiration).
        // false → ilk açılıştan 8 saat sonra kesinlikle sona erer.
        options.SlidingExpiration = true;
    });

// ─────────────────────────────────────────────────────────────────────
// Gün 20: Authorization — policy tanımları ve resource handler kaydı.
//
// Policy → "KitapEkleme" gibi bir isimle erişim kuralı tanımla,
//           [Authorize(Policy = "KitapEkleme")] ile kullan.
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    // Admin veya Editor rolünden biri yeterliyse kitap eklenebilir.
    options.AddPolicy("KitapEkleme", policy =>
        policy.RequireRole("Admin", "Editor"));

    // Sadece Admin silebilir.
    options.AddPolicy("KitapSilme", policy =>
        policy.RequireRole("Admin"));

    // Giriş yapılmış + email onaylı olmalı.
    // "emailOnaylandi" claim'ini HesapController.Giris() yazıyor.
    options.AddPolicy("EmailOnaylandi", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("emailOnaylandi", "true");
    });

    // Resource-based policy — KitapDuzenlemeHandler bu policy'yi işler.
    // Requirement tipini belirtiriz, hangi handler'ın devreye gireceğini
    // DI container çözümler (IAuthorizationHandler kaydına bakarak).
    options.AddPolicy("KitapDuzenleme", policy =>
        policy.Requirements.Add(new KitapDuzenlemeRequirement()));
});

// ─────────────────────────────────────────────────────────────────────
// Gün 25: Hangfire — job storage + server kaydı.
//
// InMemory → geliştirme için yeterli, uygulama kapanınca joblar kaybolur.
// Production'da: Hangfire.SqlServer veya Hangfire.Redis kullanılır.
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddHangfire(config =>
    config.UseInMemoryStorage());

// Hangfire server → jobları çalıştıran worker process (aynı uygulamada)
builder.Services.AddHangfireServer();

// ─────────────────────────────────────────────────────────────────────
// Gün 25: BackgroundService kayıtları.
//
// AddHostedService → host başlayınca otomatik başlatır, kapanınca durdurur.
// Her biri ayrı arka plan thread'inde çalışır.
//
// SiparisOnayKanali → Singleton: producer ve consumer aynı instance'ı paylaşır.
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<SiparisOnayKanali>();
builder.Services.AddHostedService<StokKontrolServisi>();
builder.Services.AddHostedService<SiparisOnayServisi>();

// ─────────────────────────────────────────────────────────────────────
// Gün 24: OpenAPI — .NET 9 built-in spec üretimi.
//
// "AddOpenApi()" → /openapi/v1.json endpoint'ini aktive eder.
// UI sunmaz — Scalar ayrıca eklenir (pipeline'da).
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi();

// ─────────────────────────────────────────────────────────────────────
// Gün 22: API Versioning — Asp.Versioning paketi.
//
// İstemci versiyon belirtmezse v1.0 varsayılan.
// URL path stratejisi: /api/v1/kitaplar, /api/v2/kitaplar
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddApiVersioning(options =>
{
    // Versiyon belirtilmezse bu versiyon geçerli
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;

    // Yanıt header'larında desteklenen versiyonları bildir:
    //   api-supported-versions: 1.0, 2.0
    //   api-deprecated-versions: (varsa)
    options.ReportApiVersions = true;
})
.AddMvc(); // MVC controller'larında [ApiVersion] attribute'unu etkinleştir

// Resource-based authorization handler — DI container'a kaydet.
// Scoped: her request'te ayrı instance — ileride DbContext kullanırsa Scoped olmalı.
builder.Services.AddScoped<IAuthorizationHandler, KitapDuzenlemeHandler>();

// EF Core — bağlantı dizesi appsettings.json'dan gelir
builder.Services.AddDbContext<KitabeviDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─────────────────────────────────────────────────────────
// Gün 16: Konfigürasyon — appsettings.json → C# sınıfına bağlama
//
// builder.Configuration, şu sırayla üst üste bindirir:
//   1. appsettings.json
//   2. appsettings.{Environment}.json   (Development ise ExpiryMinutes=5 kazanır)
//   3. Ortam değişkenleri               (Jwt__SecretKey=... ile ezilir)
//   4. Komut satırı argümanları
// ─────────────────────────────────────────────────────────

// "Jwt" bölümünü JwtAyarlari sınıfına bağla.
// Artık servisler IOptions<JwtAyarlari> ile bu ayarlara ulaşabilir.
builder.Services.Configure<JwtAyarlari>(
    builder.Configuration.GetSection("Jwt"));

// Gün 18: KitapServisi — in-memory veri, Singleton (uygulama boyunca tek liste)
// Gerçek projede AddScoped<IKitapServisi, KitapServisi>() olur — her request ayrı DB context alır
builder.Services.AddSingleton<IKitapServisi, KitapServisi>();

// TokenServisi — IOptions<T> kullanır (ayarlar uygulama boyunca sabit)
builder.Services.AddSingleton<TokenServisi>();

// TokenServisiCanlı — IOptionsMonitor<T> kullanır (hot reload)
builder.Services.AddSingleton<TokenServisiCanlı>();

// TokenServisiScoped — IOptionsSnapshot<T> kullanır (request bazlı)
// Singleton olamaz; her request yeni instance alır.
builder.Services.AddScoped<TokenServisiScoped>();

var app = builder.Build();

// ──────────────────────────────────────────────
// Middleware pipeline — sıra kritik!
// ──────────────────────────────────────────────

// ──────────────────────��──────────────────────────────────────────────
// Gün 24: OpenAPI + Scalar — sadece development'ta aç.
//
// Production'da /openapi/v1.json açık kalırsa API yapısı dışarıya görünür.
// MapOpenApi()           → /openapi/v1.json (ham JSON spec)
// MapScalarApiReference() → /scalar/v1     (interaktif UI)
// ───────────────────────────────────────��──────────────────────────��──
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Kitabevi API";
        // Scalar'ın varsayılan teması — "Default", "Moon", "Saturn" vb.
        options.Theme = ScalarTheme.Moon;
    });
}

// 1. Hata yakalama — en dışta olmalı, içteki tüm hataları yakalar
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 2. İstek loglama — her isteği loglar (Gün 15)
app.UseIstekLoglama();

// 3. HTTP → HTTPS yönlendirme
app.UseHttpsRedirection();

// 4. Static dosyalar (CSS/JS) — auth gerekmeden erişilir
app.MapStaticAssets();

// 5. Routing — hangi controller/action? belirle
app.UseRouting();

// 6. Authentication — cookie okunur, User nesnesi doldurulur.
//    UseAuthorization'dan ÖNCE gelmeli — kim olduğu bilinmeden
//    neye izin verileceği bilinemez.
app.UseAuthentication();

// 7. Authorization — [Authorize] attribute'larını işler.
//    UseAuthentication'dan SONRA gelmeli.
app.UseAuthorization();

// 7. Controller route'ları
//
// Gün 17: Convention routing — iki route tanımlandı.
//
// Kural: Daha spesifik route ÖNCE yazılmalı.
// İlk eşleşen kazanır; "default" her şeyi yuttuğu için en sona kalır.
//
// KitapController [Route("kitaplar")] attribute'u taşıdığı için
// convention routing'e girmez — kendi attribute route'unu kullanır.
// HomeController'da [Route] yok → "default" convention'ı geçerli.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// ─────────────────────────────────────────────────────────────────────
// Gün 25: Hangfire dashboard — /hangfire adresinde job yönetimi.
// Sadece development'ta açık — production'da auth ile korunmalı.
// ─────────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}

// Recurring job kaydı — her gece 02:00'de stok raporu.
// "AddOrUpdate" → aynı isimle tekrar çağrılırsa günceller, oluşturmaz.
RecurringJob.AddOrUpdate<KitabeviJoblar>(
    "gunluk-stok-raporu",
    j => j.GunlukStokRaporu(),
    "0 2 * * *"); // cron: dakika=0, saat=2, her gün

// ─────────────────────────────────────────────────────────────────────
// Gün 23: Minimal API endpoint'leri — extension metod ile kayıt.
// Program.cs sadece bu satırı görür; detaylar KitapEndpoints.cs'te.
// ─────────────────────────────────────────────────────────────────────
app.MapKitapEndpoints();

app.Run();
