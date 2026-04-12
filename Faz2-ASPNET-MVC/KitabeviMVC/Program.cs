using Asp.Versioning;
using Hangfire;
using KitabeviMVC.Authorization;
using KitabeviMVC.BackgroundServices;
using KitabeviMVC.Behaviours;
using KitabeviMVC.Endpoints;
using KitabeviMVC.Repositories;
using MediatR;
using Scalar.AspNetCore;
using Serilog;
using KitabeviMVC.Configuration;
using KitabeviMVC.Data;
using KitabeviMVC.Filters;
using KitabeviMVC.Middleware;
using KitabeviMVC.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

// ─────────────────────────────────────────────────────────────────────
// Gün 36: Serilog — uygulama başlamadan önce yapılandırılır.
// Startup sırasında oluşan hataları da yakalamak için builder'dan önce tanımlanır.
// ─────────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    // Override: Microsoft/EF Core namespace'ini Warning'e çek
    // Override yazmasaydık: EF Core her SQL sorgusunu Information olarak loglar → log gürültüsü
    .MinimumLevel.Override("Microsoft",                        Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore",    Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()        // LogContext.PushProperty() ile anlık özellik eklemeye izin ver
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path:            "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,       // her gün yeni dosya
        retainedFileCountLimit: 7,                  // 7 günden eski dosyalar silinir
        outputTemplate:  "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    // sadece Console kullansaydık: uygulama kapanınca loglar kaybolur
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────────
// Gün 36: ASP.NET Core'un ILogger'ını Serilog ile değiştir.
// bunu yazmasaydık: appsettings.json Logging bölümü kullanılırdı ve
// yapılandırılmış loglama (structured logging) devre dışı kalırdı.
// ─────────────────────────────────────────────────────────────────────
builder.Host.UseSerilog();

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

// ─────────────────────────────────────────────────────────────────────
// Gün 29: DbContext kaydı.
// Gün 32: SQL Server geçiş yolu gösterildi — şimdilik InMemory devam ediyor.
//
// AddDbContext → varsayılan olarak SCOPED kaydeder.
// Her HTTP request için yeni DbContext → Unit of Work pattern.
//
// InMemory → SQL Server geçişi için bu bloğu değiştir:
//   options.UseSqlServer(
//       builder.Configuration.GetConnectionString("DefaultConnection"))
//   ardından: dotnet ef migrations add InitialCreate
//             dotnet ef database update
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<KitabeviDbContext>(options =>
    options.UseInMemoryDatabase("KitabeviDb"));

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

// ─────────────────────────────────────────────────────────────────────
// Gün 26: IMemoryCache — süreç içi bellek cache'i.
//
// SizeLimit = 1024: toplam cache kapasitesi (birim sen tanımlarsın).
// Her cache girdisi .SetSize(n) ile kaç birim kapladığını bildirir.
// SizeLimit olmadan belleği sınırsız tüketir — production'da mutlaka belirle.
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024;
});

// ─────────────────────────────────────────────────────────────────────
// Gün 29: EF Core tabanlı servis kaydı.
//
// EfKitapServisi → Scoped (DbContext Scoped olduğu için zorunlu).
// CachedKitapServisi → Scoped (içteki EfKitapServisi Scoped olduğu için).
//
// DI çözümleme:
//   IKitapServisi istendi → CachedKitapServisi verilir (Scoped)
//   CachedKitapServisi içindeki IKitapServisi → EfKitapServisi verilir (Scoped)
//   EfKitapServisi içindeki KitabeviDbContext → aynı request'in DbContext'i (Scoped)
//
// Gün 26 farkı: Gün 26'da hem KitapServisi hem CachedKitapServisi Singleton'dı.
// EF Core'a geçince Scoped zorunlu oldu — "cannot consume scoped from singleton" kuralı.
//
// Cache katmanını devre dışı bırakmak → sadece bu satırı kullan:
//   builder.Services.AddScoped<IKitapServisi, EfKitapServisi>();
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<EfKitapServisi>();
// Gün 32: DbSeeder — Scoped: DbContext Scoped olduğu için zorunlu.
// Singleton yapılsaydı: "cannot consume scoped service from singleton" hatası.
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddScoped<IKitapServisi>(sp =>
    new CachedKitapServisi(
        sp.GetRequiredService<EfKitapServisi>(),
        sp.GetRequiredService<IMemoryCache>(),
        sp.GetRequiredService<ILogger<CachedKitapServisi>>()));

// ─────────────────────────────────────────────────────────────────────
// Gün 30: IKitapSorguServisi — EfKitapServisi zaten implement ediyor.
// Aynı Scoped instance üzerinden çözümlenir.
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IKitapSorguServisi>(sp =>
    sp.GetRequiredService<EfKitapServisi>());

// ─────────────────────────────────────────────────────────────────────
// Gün 33: IKitapBatchServisi — ExecuteUpdate/Delete için ayrı interface.
// EfKitapServisi zaten implement ediyor; aynı Scoped instance.
//
// Neden ayrı interface? CachedKitapServisi ve KitapServisi bu metodları
// implement etmek zorunda değil — Interface Segregation Principle.
// Controller IKitapBatchServisi'ni inject edince doğrudan EfKitapServisi'ne gider.
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IKitapBatchServisi>(sp =>
    sp.GetRequiredService<EfKitapServisi>());
// bunu yazmadan IKitapBatchServisi inject etmeye kalksaydık:
// "No service for type 'IKitapBatchServisi' has been registered" → runtime exception

// ─────────────────────────────────────────────────────────────────────
// Gün 34: Unit of Work — Repository katmanı DI kaydı.
// Scoped: her HTTP request için bir instance.
// Transient yapmak: her inject noktasında yeni UoW → yeni context → transaction paylaşımı bozulur.
// Singleton yapmak: tüm request'ler aynı context → thread-safety sorunu.
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

// ─────────────────────────────────────────────────────────────────────
// Gün 35: MediatR — handler'ları ve pipeline behaviour'ları kaydet.
// RegisterServicesFromAssembly: aynı projede IRequestHandler implement eden
// tüm sınıfları tarar ve DI'a kaydeder.
// bunu yazmasaydık: handler'lar DI'a kayıtlı olmaz,
// _mediator.Send() "handler bulunamadı" exception fırlatırdı.
// ─────────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    // Pipeline behaviour: her Send() çağrısında LoggingBehaviour otomatik devreye girer
    // Sıra önemli: birden fazla behaviour varsa kayıt sırası = pipeline sırası
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
});

// TokenServisi — IOptions<T> kullanır (ayarlar uygulama boyunca sabit)
builder.Services.AddSingleton<TokenServisi>();

// TokenServisiCanlı — IOptionsMonitor<T> kullanır (hot reload)
builder.Services.AddSingleton<TokenServisiCanlı>();

// TokenServisiScoped — IOptionsSnapshot<T> kullanır (request bazlı)
// Singleton olamaz; her request yeni instance alır.
builder.Services.AddScoped<TokenServisiScoped>();

var app = builder.Build();

// ─────────────────────────────────────────────────────────────────────
// Gün 32: DbSeeder ile seed — inline seed yerine servis kullanılıyor.
//
// Gün 29'da inline yazılan seed bloğu buradan kaldırıldı.
// Neden? Inline seed:
//   → Yazarlar + Kitaplar sırası yönetilemez (FK bağımlılığı)
//   → AnyAsync idempotency kontrolü yoktu → her uygulama restart'ta patlardı
//   → YazarId set edilmiyordu → Gün 31 eager loading örneği boş kalırdı
//
// Pending migration kontrolü (SQL Server'a geçilince aktif olur):
//   InMemory provider GetPendingMigrations() desteklemez → sadece SQL Server için.
//   Bu yüzden kontrol şimdi yorum satırı; SQL Server'a geçince açılır.
//
// using: scope ömrü bu blokla sınırlı → DbContext burada dispose edilir.
// bunu yazmadan GetRequiredService çağırsaydık scope hiç kapanmazdı → bellek sızıntısı.
// ─────────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KitabeviDbContext>();

    // ── SQL Server'a geçince bu bloğu aç ──────────────────────────────
    // var bekleyenler = db.Database.GetPendingMigrations().ToList();
    // if (bekleyenler.Any())
    // {
    //     var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    //     if (app.Environment.IsDevelopment())
    //     {
    //         // Geliştirme: otomatik uygula
    //         log.LogInformation("Migration uygulanıyor: {Liste}", string.Join(", ", bekleyenler));
    //         db.Database.Migrate();
    //     }
    //     else
    //     {
    //         // Production: uygulamayı başlatma — CI/CD migration bundle kullanılmalı
    //         log.LogCritical("Production'da uygulanmamış migration var: {Liste}", string.Join(", ", bekleyenler));
    //         throw new InvalidOperationException("Production'da migration bekleniyor, uygulama başlatılmıyor.");
    //     }
    // }
    // ─────────────────────────────────────────────────────────────────

    // DbSeeder: Yazarları önce, Kitapları sonra ekler (FK sırası)
    // AnyAsync kontrolüyle idempotent → kaç kez çalışırsa çalışsın, duplicate yok
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
    // bunu async çağırmak için Program.cs'in top-level statement'ı async olmalı
    // .NET 6+ top-level statements zaten async'i destekler — await burada geçerli
}

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

// ─────────────────────────────────────────────────────────────────────
// Gün 36: Serilog request loglama — her HTTP isteği için otomatik log.
// {RequestMethod} GET, {RequestPath} /kitaplar, {StatusCode} 200, {Elapsed} 12.3ms
// UseRouting'den önce koyulursa routing süresi de dahil olur.
// bunu yazmasaydık: istek/yanıt loglarını her action'a elle yazmak gerekir.
// ─────────────────────────────────────────────────────────────────────
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "{RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0.0}ms)";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserId",    httpContext.User.Identity?.Name ?? "anonim");
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        // bunu yazmasaydık: hangi kullanıcı hangi endpointi çağırdı bilemezdik
    };
});

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
//
// Gün 27: Test ortamında atlanır — WebApplicationFactory statik Hangfire
// API'sini başlatmadan önce RecurringJob.AddOrUpdate çağrısı yapar ve
// "JobStorage.Current has not been initialized" hatasına yol açar.
// Çözüm: statik API yerine IRecurringJobManager (DI tabanlı) kullanmak
// doğru yaklaşımdır, ancak bu projede örnek olması için ortam koruması yeterli.
if (!app.Environment.IsEnvironment("Testing"))
{
    RecurringJob.AddOrUpdate<KitabeviJoblar>(
        "gunluk-stok-raporu",
        j => j.GunlukStokRaporu(),
        "0 2 * * *"); // cron: dakika=0, saat=2, her gün
}

// ─────────────────────────────────────────────────────────────────────
// Gün 23: Minimal API endpoint'leri — extension metod ile kayıt.
// Program.cs sadece bu satırı görür; detaylar KitapEndpoints.cs'te.
// ─────────────────────────────────────────────────────────────────────
app.MapKitapEndpoints();

app.Run();

// ─────────────────────────────────────────────────────────────────────
// Gün 27: Integration test için zorunlu.
//
// .NET 6+ top-level program'da "Program" sınıfı otomatik üretilir ama
// internal olarak işaretlenir — test projesi göremez.
// "public partial class Program" → test projesindeki
// WebApplicationFactory<Program>'ın bu sınıfa erişmesini sağlar.
// ─────────────────────────────────────────────────────────────────────
public partial class Program { }
