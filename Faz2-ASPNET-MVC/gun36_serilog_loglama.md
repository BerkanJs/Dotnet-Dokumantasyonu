# Gün 36 — Serilog ile Yapılandırılmış Loglama

---

## 1. Neden Serilog?

ASP.NET Core'un yerleşik `ILogger` arayüzü metin tabanlı log üretir. Binlerce log satırı arasında "fiyatı 100'ün üzerinde kitap satışları bu ay kaç tane?" gibi bir soruyu cevaplayamazsın.

```
Klasik loglama — metin:
  _logger.LogInformation("Kitap 42 satıldı, fiyat: 89");
  → Log dosyasında düz metin satırı
  → Filtrelemek için regex veya gözle tarama

Structured logging — nesne:
  _logger.LogInformation("Kitap {KitapId} satıldı, fiyat: {Fiyat}", 42, 89);
  → JSON: { "KitapId": 42, "Fiyat": 89, "Level": "Information", "@t": "..." }
  → Seq/Elasticsearch'te: KitapId = 42 AND Fiyat > 50 sorgusu çalışır
```

Serilog, yapılandırılmış loglama için .NET ekosisteminin standart kütüphanesidir.

---

## 2. Kurulum

```bash
dotnet add package Serilog.AspNetCore
# ASP.NET Core pipeline entegrasyonu (request loglama dahil)

dotnet add package Serilog.Sinks.Console
# Konsola renkli, yapılandırılmış çıktı

dotnet add package Serilog.Sinks.File
# Dosyaya yazma (rolling file desteğiyle)

dotnet add package Serilog.Settings.Configuration
# appsettings.json'dan Serilog yapılandırması okuma
```

---

## 3. Temel Yapılandırma

```csharp
// Program.cs

using Serilog;

// ─── 1. Logger'ı uygulamadan ÖNCE yapılandır ───────────────────────────────
// Startup sırasında oluşan hataları da yakalamak için Program.cs başında tanımlanır.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()             // Information ve üstü loglanır (Debug/Verbose atlanır)
                                            // MinimumLevel.Debug() yazsaydık: EF Core sorguları
                                            // dahil her şey loglanır → dosya hızla büyür

    .MinimumLevel.Override("Microsoft",        Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    // Override: Microsoft namespace'i için seviyeyi Warning'e çek
    // Override yazmasaydık: EF Core her SQL sorgusunu Information olarak loglar
    // → üretim ortamında log dosyası çok hızlı büyür, gerçek hataları gömülü kalır

    .Enrich.FromLogContext()               // using (LogContext.PushProperty(...)) ile anlık özellik ekle
    .Enrich.WithMachineName()             // hangi sunucu: çoklu instance'da hangi pod'dan geldiği belli olur
    .Enrich.WithEnvironmentName()         // Development / Production ayrımı JSON'da görünür

    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    // outputTemplate: konsol çıktı formatı
    // {Level:u3}: INF, WRN, ERR gibi 3 karakter — bilgi yoğunluğu yüksek terminal için

    .WriteTo.File(
        path:           "Logs/log-.txt",   // - işareti: tarih eklenecek (log-20240411.txt)
        rollingInterval: RollingInterval.Day,   // her gün yeni dosya — tek dev dosya büyümez
        retainedFileCountLimit: 7,              // 7 günden eski dosyalar silinir
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    // bunu yazmadan sadece Console sink kullansaydık:
    // uygulama kapanınca tüm loglar kaybolur — hata analizi imkansız

    .CreateLogger();

// ─── 2. Host builder'a ekle ────────────────────────────────────────────────
builder.Host.UseSerilog();
// ASP.NET Core'un ILogger'ını Serilog ile değiştirir
// bunu yazmasaydık: appsettings.json'daki Logging konfigürasyonu kullanılırdı
// ve _logger.LogInformation() Serilog'a değil yerleşik provder'a giderdi

var app = builder.Build();
```

---

## 4. appsettings.json ile Yapılandırma

Kod içinde `Log.Logger = new LoggerConfiguration()...` yazmak yerine appsettings.json kullanmak daha esnek: deploy sonrası log seviyesini binary değiştirmeden ayarlarsın.

```json
// appsettings.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft":                        "Warning",
        "Microsoft.EntityFrameworkCore":    "Warning",
        "System":                           "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path":                  "Logs/log-.txt",
          "rollingInterval":       "Day",
          "retainedFileCountLimit": 7
        }
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName" ]
  }
}
```

```csharp
// Program.cs — appsettings.json'dan oku

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    // ReadFrom.Configuration: yukarıdaki JSON bloğunu okur
    // artık tek satır — JSON'da değişiklik yeterli, binary değişmez
    .CreateLogger();
```

---

## 5. Request Loglama — UseSerilogRequestLogging

Her HTTP isteği için otomatik log üretir: method, path, status code, süre.

```csharp
// Program.cs — middleware sırası önemli

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "{RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0.0}ms)";
    // {Elapsed}: istek süresi ms cinsinden — yavaş endpoint'leri tespit edersin

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserId",    httpContext.User.Identity?.Name ?? "anonim");
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent);
        // bunu yazmadan sadece path/status loglansaydık:
        // hangi kullanıcı hangi endpointi çağırdı bilemezdik
    };
});
// Bu middleware'i UseRouting'den ÖNCE koyarsan routing süresi dahil olur
// SONRA koyarsan sadece controller işlem süresi dahil olur — ikisi de kabul edilebilir
```

---

## 6. Uygulama İçinde Loglama

```csharp
// Services/EfKitapServisi.cs (veya herhangi bir servis/handler)

public class EfKitapServisi
{
    private readonly ILogger<EfKitapServisi> _logger;
    private readonly KitabeviDbContext _context;

    public EfKitapServisi(ILogger<EfKitapServisi> logger, KitabeviDbContext context)
    {
        _logger  = logger;
        _context = context;
    }

    public async Task<Kitap?> GetByIdAsync(int id)
    {
        _logger.LogDebug("Kitap aranıyor: {KitapId}", id);
        // {KitapId}: structured — JSON'da "KitapId": 42 şeklinde kaydedilir
        // "Kitap aranıyor: " + id.ToString() yazmak YANLIŞ —
        // string concat: yapısal veri kaybolur, Seq'de KitapId'ye göre filtreleyemezsin

        var kitap = await _context.Kitaplar.FindAsync(id);

        if (kitap is null)
            _logger.LogWarning("Kitap bulunamadı: {KitapId}", id);
            // Warning: "bu normal değil ama çökmüyor" — alarm seviyesi değil

        return kitap;
    }

    public async Task SilAsync(int id)
    {
        var kitap = await _context.Kitaplar.FindAsync(id);
        if (kitap is null)
        {
            _logger.LogWarning("Silme isteği — kitap yok: {KitapId}", id);
            return;
        }

        _context.Kitaplar.Remove(kitap);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Kitap silindi: {KitapId} {@KitapBilgi}",   // {@...}: nesneyi JSON olarak serialize et
            id,
            new { kitap.Baslik, kitap.Fiyat });
        // {@KitapBilgi}: Seq'de tıklayınca tüm nesneyi görürsün
        // {KitapBilgi} yazsaydın (@ olmadan): ToString() çağrılır, anlamsız çıktı
    }
}
```

---

## 7. Exception Loglama

```csharp
// Program.cs — global hata yakalama

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature  = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error;

        if (exception is not null)
        {
            Log.Error(exception,
                "İşlenmemiş hata: {Path} {Method}",
                context.Request.Path,
                context.Request.Method);
            // Log.Error(exception, ...): stack trace otomatik eklenir
            // _logger.LogError(exception.Message) yazsaydın: stack trace kaybolur
        }

        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { hata = "Beklenmeyen bir hata oluştu." });
    });
});
```

---

## 8. Log Seviyeleri — Ne Zaman Ne Kullanılır

```
Verbose     → en ayrıntılı, döngü içi izleme — prod'da asla açma
Debug       → geliştirme sırasında değer kontrolü, sorgu parametreleri
Information → normal iş akışı olayları: "Kitap eklendi", "Kullanıcı giriş yaptı"
Warning     → beklenmedik ama düzeltilebilir: "Kitap bulunamadı", "Retry denemesi 2/3"
Error       → işlem başarısız: exception yakalandı, kayıt silinemedi
Fatal       → uygulama çalışamaz durumda: DB bağlantısı hiç kurulamadı
```

```csharp
// Log seviyesi kararları:
_logger.LogInformation("Sipariş oluşturuldu: {SiparisId}", siparisId);  // iş akışı
_logger.LogWarning("Stok azaldı: {KitapId} kalan={Stok}", id, stok);    // dikkat
_logger.LogError(ex, "Ödeme başarısız: {SiparisId}", siparisId);        // hata
_logger.LogDebug("Cache miss: {Anahtar}", anahtar);                      // geliştirme
```

---

## 9. Özet

```
Serilog
  Structured logging: {PropertyName} → JSON'da filtrelenebilir alan
  Sink: Console (geliştirme), File (prod), Seq/Elasticsearch (üretim)
  Override: Microsoft/EF Core namespace'ini Warning'e çek — gürültüyü engelle

Konfigürasyon
  appsettings.json + ReadFrom.Configuration → binary değiştirmeden log seviyesi ayarla

Request Loglama
  UseSerilogRequestLogging → her HTTP isteği otomatik loglanır
  EnrichDiagnosticContext → UserId, UserAgent gibi bilgiler eklenir

Hata Loglama
  Log.Error(exception, template, args) → stack trace otomatik dahil
  UseExceptionHandler → yakalanmayan tüm hatalar tek noktada loglanır

Pratik Kurallar
  String concat değil structured: _logger.LogInformation("{Id}", id)
  {@Nesne}: nesneyi serialize et, sadece {Nesne} ToString() çağırır
  Debug log prod'da kapalı olsun — appsettings.Production.json'da Warning veya Information
```

---

## Sonraki Gün

Gün 37'de Docker ile containerization: Dockerfile yazımı, multi-stage build, docker-compose ile SQL Server + uygulama birlikte ayağa kaldırma.
