# Gün 101 — Structured Logging: Production-Grade

---

## Loglama Neden Önemli?

Production'da bir şeyler ters gidiyor. Kullanıcı "sipariş veremiyorum" dedi. Hata nerede? Hangi serviste? Hangi kullanıcıda? Hangi istek sırasında?

Logların yoksa — karanlıkta yürüyorsun. Logların varsa ama düz metin olarak yazılmışsa — binlerce satır arasında iğne arıyorsun.

**Structured logging** ise loglara yapı kazandırır. Her log satırında hangi kullanıcı, hangi istek, hangi sunucu, ne oldu — hepsi ayrı ayrı aranabilir, filtrelenebilir alanlar olarak saklanır.

---

## Structured Logging vs Düz String — Fark Nedir?

İki farklı loglama şekli var. İkisi de aynı metni üretiyor — ama arka planda çok farklı çalışıyor.

### Düz String (Yanlış Yol)

```csharp
_logger.LogInformation($"User {userId} logged in from {ipAddress}");
```

Bu satır çıktıda şunu üretir:
```
User 42 logged in from 192.168.1.1
```

Sorun ne? Bu sadece bir cümle. Düz metin. Log analiz aracında "userId'si 42 olan tüm logları göster" diyemezsin. Çünkü 42 sayısı cümlenin içinde kaybolmuş — araç 42'nin userId olduğunu bilmiyor.

### Structured (Doğru Yol)

```csharp
_logger.LogInformation("User {UserId} logged in from {IpAddress}", userId, ipAddress);
```

Çıktıda aynı cümleyi üretir:
```
User 42 logged in from 192.168.1.1
```

Ama arka planda veriler ayrı ayrı saklanır:
```json
{
  "message": "User 42 logged in from 192.168.1.1",
  "UserId": 42,
  "IpAddress": "192.168.1.1",
  "Timestamp": "2026-05-10T14:30:00Z"
}
```

Artık log analiz aracında (Seq, Kibana, Application Insights) şunları yapabilirsin:
- `WHERE UserId = 42` → bu kullanıcının tüm hareketleri
- `WHERE IpAddress = "192.168.1.1"` → bu IP'den gelen tüm istekler
- `GROUP BY UserId` → en aktif kullanıcılar
- `WHERE UserId = 42 AND Level = "Error"` → bu kullanıcının hataları

Düz string ile bunların hiçbiri mümkün değil. Elinde sadece bir metin var, onun içinden veri çıkarmak regex ile uğraşmak demek.

**Kural:** `$"..."` ile asla loglama. Her zaman `"... {Property} ...", value` formatını kullan.

---

## Serilog Enricher'ları — Her Loga Otomatik Bilgi Ekleme

### Sorun

Her log satırında hangi sunucu, hangi thread, hangi ortam (development/production) olduğunu bilmek istiyorsun. Ama her seferinde elle yazmak pratik değil:

```csharp
// ✗ Her log satırında elle bilgi eklemek:
_logger.LogInformation("Sipariş oluşturuldu. Server={Server}, Thread={Thread}, Env={Env}",
    Environment.MachineName, Thread.CurrentThread.ManagedThreadId, "Production");
// Bu saçmalık — 50 farklı yerde bunu yazmak zorundasın
```

### Çözüm: Enricher

Enricher, her log satırına otomatik olarak ek bilgi ekleyen bileşen. Sen sadece `"Sipariş oluşturuldu"` yaz — enricher gerisini halleder.

**Analoji:** Postaneye mektup bırakıyorsun. Sen sadece "Kime: Ahmet" ve mektup içeriğini yazdın. Ama postane otomatik olarak tarih damgası basıyor, şube kodunu ekliyor, posta bölgesini yazıyor. Sen bunları düşünmüyorsun — sistem her mektuba otomatik ekliyor.

```csharp
// Program.cs — Serilog kurulumu:
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)

        .Enrich.FromLogContext()
        // ne yapar → middleware'de eklediğin dinamik property'leri (CorrelationId gibi) loga dahil eder
        // bunu yazmasaydık → LogContext.PushProperty ile eklediğin şeyler loglarda görünmez

        .Enrich.WithMachineName()
        // ne yapar → her loga "MachineName: web-server-02" gibi sunucu bilgisi ekler
        // neden önemli → 3 sunucu var, hata hangisinde? bu property'den anlarsın
        // bunu yazmasaydık → "bu hata hangi sunucuda?" sorusuna cevap veremezsin

        .Enrich.WithThreadId()
        // ne yapar → logun hangi thread'den yazıldığını ekler
        // neden önemli → deadlock veya concurrency bug araştırırken hangi thread ne yapıyor görmek için
        // bunu yazmasaydık → thread bazlı debug yapamaz, sorunun kaynağını bulamazsın

        .Enrich.WithEnvironmentName()
        // ne yapar → "Development", "Staging", "Production" bilgisini ekler
        // neden önemli → tüm loglar aynı yere gidiyorsa (merkezi log sistemi), hangi ortamdan geldiğini bilirsin
        // bunu yazmasaydık → test logları ile production logları karışır, yanlış alarma neden olur

        .WriteTo.Console()
        .WriteTo.Seq("http://localhost:5341");
        // Seq → structured logları görselleştiren araç, development'ta çok faydalı
        // production'da Elasticsearch+Kibana, Application Insights veya Datadog kullanılır
});
```

Artık sen sadece şunu yazıyorsun:
```csharp
_logger.LogInformation("Sipariş oluşturuldu");
```

Ama logda şu görünüyor:
```json
{
  "message": "Sipariş oluşturuldu",
  "MachineName": "web-server-02",
  "ThreadId": 14,
  "Environment": "Production",
  "Timestamp": "2026-05-10T14:30:00Z"
}
```

Hiçbirini elle yazmadın — enricher'lar otomatik ekledi.

### Custom Enricher — Kendi Bilgini Ekle

Built-in enricher'lar yetmiyorsa kendi enricher'ını yazabilirsin. Mesela multi-tenant uygulamada her loga TenantId eklemek istiyorsun:

```csharp
public class TenantEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _accessor;

    public TenantEnricher(IHttpContextAccessor accessor) => _accessor = accessor;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        var tenantId = _accessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (tenantId is not null)
        {
            logEvent.AddPropertyIfAbsent(factory.CreateProperty("TenantId", tenantId));
        }
        // ne yapar → her log satırına TenantId eklenir
        // artık "TenantId == acme olan tüm hataları göster" diyebilirsin
        // bunu yazmasaydık → hangi tenant'ta hata olduğunu anlamak için log metnini okumak zorunda kalırdın
    }
}

// Kayıt:
config.Enrich.With(new TenantEnricher(serviceProvider.GetRequiredService<IHttpContextAccessor>()));
```

---

## İstek Takibi: Correlation ID vs TraceId vs Activity.Id

Production'da bir hata oldu. Ama uygulaman 3 servisten oluşuyor: API Gateway → Sipariş Servisi → Ödeme Servisi. Hata hangisinde? Hangi istek sırasında? Bu soruyu cevaplamak için isteklere takip numarası veriyorsun.

Ama 3 farklı takip numarası konsepti var — her biri farklı amaca hizmet ediyor:

### 1. HttpContext.TraceIdentifier — "Bu uygulama içindeki istek numarası"

```csharp
var traceId = HttpContext.TraceIdentifier;  // "0HN4T..." gibi bir string
```

ASP.NET Core her gelen HTTP isteğine otomatik bir ID verir. Ama bu ID sadece **bu uygulama** içinde geçerli. İstek Ödeme Servisi'ne gittiğinde orada farklı bir TraceIdentifier oluşur — ikisi arasında bağlantı yok.

**Ne zaman kullan:** Tek servis, basit uygulama. Birden fazla servis varsa yetersiz.

### 2. Activity.Current?.TraceId — "Servisler arası otomatik takip numarası"

```csharp
var traceId = Activity.Current?.TraceId.ToString();
```

W3C standardına uygun bir ID. En büyük farkı: HttpClient ile başka servise istek attığında bu ID otomatik olarak karşı servise de gönderilir (`traceparent` header ile). Yani 3 servis de aynı TraceId'yi bilir — logları birleştirebilirsin.

**Analoji:** Kargo takip numarası. Paket depodan çıktı, araca bindi, şubeye geldi — her aşamada aynı takip numarası. Neredeyse hangi aşamada olduğunu bilirsin.

**Ne zaman kullan:** Microservice ortamı, distributed tracing (Jaeger, Zipkin, Application Insights).

### 3. X-Correlation-ID — "İş süreci takip numarası"

```csharp
var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault();
```

TraceId her HTTP isteğinde farklıdır. Ama bir sipariş süreci birden fazla HTTP isteği içerebilir: sepete ekle → ödeme yap → kargo bilgisi al. Her biri farklı TraceId alır. **Correlation ID** ise tüm bu istekleri tek bir iş süreci altında birleştirir.

**Analoji:** Bir dava numarası. Dava kapsamında onlarca duruşma, dilekçe, karar var. Her biri ayrı belge ama hepsi aynı dava numarasına bağlı. Correlation ID = dava numarası, TraceId = tek bir duruşmanın numarası.

**Ne zaman kullan:** "Bu siparişle ilgili tüm logları göster" demen gereken senaryolar.

### Karşılaştırma Tablosu

| ID | Kapsam | Kim üretir | Otomatik yayılır mı? | Ne zaman kullan |
|----|--------|-----------|---------------------|-----------------|
| TraceIdentifier | Tek uygulama | ASP.NET Core | Hayır | Basit tek servis |
| Activity.TraceId | Servisler arası | Framework/OpenTelemetry | Evet (traceparent header) | Distributed tracing |
| X-Correlation-ID | İş süreci | Client veya Gateway | Elle yaymalısın | Business flow takibi |

### Correlation ID Middleware — Her İsteğe Otomatik Ekle

```csharp
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // İstekte header varsa onu kullan, yoksa yeni üret:
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
        // neden header'dan al → API Gateway veya client zaten göndermişse aynı ID'yi kullan
        // neden yoksa üret → ilk servis ise kimse göndermemiş, sen başlat

        // Yanıta da ekle — client log'larında eşleştirebilsin:
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        // Serilog LogContext'e ekle:
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
        // ne yapar → bu istek boyunca yazılan TÜM loglara CorrelationId property'si eklenir
        // controller'da, service'te, repository'de — nerede log yazarsan yaz, CorrelationId otomatik görünür
        // bunu yazmasaydık → her log satırında elle correlationId parametresi geçmek zorunda kalırdın
    }
}

app.UseMiddleware<CorrelationIdMiddleware>();
```

`LogContext.PushProperty` ne yapıyor? Bir "scope" oluşturuyor. Bu scope içinde yazılan tüm loglara belirttiğin property otomatik ekleniyor. Scope bitince (using bloğu kapanınca) property kalkıyor. Böylece bir isteğin tüm logları aynı CorrelationId'yi taşıyor — farklı istekler farklı ID alıyor.

---

## W3C TraceContext — Servisler Arası Otomatik Takip

Correlation ID'yi elle yönetiyorsun. Ama TraceId otomatik yayılır — hiçbir şey yapmana gerek yok.

### Nasıl Çalışır?

```
Kullanıcı isteği
  → API Gateway
      traceparent: 00-abc123def456...-span01-01
      │
      → Sipariş Servisi (aynı traceparent header'ı alır)
          TraceId: abc123def456...  ← aynı!
          │
          → Ödeme Servisi'ne HttpClient ile istek
              traceparent header otomatik eklenir
              TraceId: abc123def456...  ← yine aynı!
```

3 servis de aynı TraceId'yi biliyor. Log aracında `TraceId = abc123def456` diye filtrele → 3 servisteki tüm loglar bir arada.

```csharp
// ASP.NET Core + HttpClient bunu varsayılan olarak yapar (.NET 6+)
// Ek config gerekmez — sadece HttpClient kullanıyorsan traceparent otomatik eklenir.

// Tam distributed tracing istiyorsan — OpenTelemetry ekle:
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()    // gelen istekleri yakala
        .AddHttpClientInstrumentation()     // giden HTTP çağrılarını yakala
        .AddSqlClientInstrumentation()      // DB sorgularını yakala
        .AddOtlpExporter());                // Jaeger veya Zipkin'e gönder
// ne yapar → tüm servisler arası çağrıları tek trace altında birleştirir
// Jaeger UI'da "waterfall" görünümü: hangi servis, ne kadar sürdü, sıralama ne
// bunu yazmasaydık → her servisin loglarını ayrı ayrı okuyup elle eşleştirmek zorunda kalırdın
```

---

## Hassas Veri Maskeleme — Loga Ne Yazılmamalı?

Şifre, kredi kartı numarası, TC kimlik, API token — bunlar asla loga yazılmamalı. Çünkü:
- Log dosyasına erişen herkes bu verileri görür
- Log'lar 3. parti servislere gönderiliyorsa (Datadog, Kibana) — hassas veri dışarı çıkar
- KVKK/GDPR ihlali → yasal sorumluluk

### Sorun: Geliştirici Yanlışlıkla Loglarsa?

```csharp
// ✗ Geliştirici farkında olmadan nesneyi logladı:
_logger.LogInformation("Ödeme işlemi: {@OdemeDetay}", odemeDto);
// @ operatörü nesneyi "destructure" eder — tüm property'leri yazar
// Log'da: { KartNo: "4532-1234-5678-9012", CVV: "123", Tutar: 150 }
// → kredi kartı ve CVV açıkça logda!
```

`{@OdemeDetay}` yazınca Serilog nesnenin tüm property'lerini ayrı ayrı loga yazar. Geliştirici "tutarı loglamak istiyordum" diye yazdı ama kredi kartı da gitti.

### Çözüm 1: Destructuring Policy — Belirli Tipleri Maskele

```csharp
// Serilog konfigürasyonunda:
config.Destructure.ByTransforming<OdemeDto>(dto => new
{
    KartNo = MaskCard(dto.KartNo),   // "4532-****-****-9012"
    Tutar = dto.Tutar,                // bu görünebilir, sorun yok
    CVV = "***"                       // asla gösterme
});
// ne yapar → OdemeDto {@...} ile loglandığında otomatik maskelenir
// geliştirici {@OdemeDto} yazsa bile kart numarası gizli kalır
// bunu yazmasaydık → her geliştirici "kart numarasını loglama" kuralını bilmek zorunda

private static string MaskCard(string card)
    => card.Length > 8 ? $"{card[..4]}-****-****-{card[^4..]}" : "****";
// "4532-1234-5678-9012" → "4532-****-****-9012"
```

### Çözüm 2: [LogMasked] Attribute — DTO'da İşaretle

Hangi alanların hassas olduğunu DTO'nun kendisinde belirt — policy elle yazmak yerine attribute ile işaretle:

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class LogMaskedAttribute : Attribute { }

public class KullaniciDto
{
    public string Email { get; set; } = null!;        // bu loglanabilir

    [LogMasked]
    public string Sifre { get; set; } = null!;        // bu maskelenmeli

    [LogMasked]
    public string TcKimlik { get; set; } = null!;     // bu da maskelenmeli
}
```

```csharp
// Serilog konfigürasyonunda genel bir policy yaz:
config.Destructure.ByTransformingWhere<object>(
    type => type.GetProperties().Any(p => p.GetCustomAttribute<LogMaskedAttribute>() != null),
    obj =>
    {
        var props = obj.GetType().GetProperties();
        var result = new Dictionary<string, object?>();
        foreach (var prop in props)
        {
            result[prop.Name] = prop.GetCustomAttribute<LogMaskedAttribute>() != null
                ? "***MASKED***"
                : prop.GetValue(obj);
        }
        return result;
    });
// ne yapar → [LogMasked] attribute'u olan herhangi bir property logda "***MASKED***" görünür
// avantaj → yeni bir DTO eklediğinde sadece [LogMasked] koyarsın, policy'yi değiştirmene gerek yok
```

Artık geliştirici `{@KullaniciDto}` yazsa bile:
```json
{ "Email": "berkan@mail.com", "Sifre": "***MASKED***", "TcKimlik": "***MASKED***" }
```

---

## Log Seviyesi Dinamik Değiştirme

Production'da bir hata araştırıyorsun. Normal seviyede (Information) yeterli detay yok — Debug logları lazım. Ama Debug açmak için uygulamayı yeniden deploy etmek istemiyorsun (downtime, risk).

### Log Seviyeleri — Hatırlatma

| Seviye | Ne zaman yaz | Örnek |
|--------|-------------|-------|
| **Verbose/Trace** | Her şey, en detaylı | "Metot X'e girildi, parametre: Y" |
| **Debug** | Geliştirme detayı | "Cache miss, DB'ye gidiliyor" |
| **Information** | Normal akış | "Sipariş oluşturuldu, id: 42" |
| **Warning** | Potansiyel sorun | "Rate limit'e yaklaşıldı: %90" |
| **Error** | Hata ama uygulama çalışıyor | "Ödeme servisi timeout, retry edilecek" |
| **Fatal** | Uygulama çöküyor | "DB bağlantısı kurulamıyor, uygulama durduruluyor" |

Production'da genelde Information ve üstü loglanır. Debug çok fazla log üretir — performans etkisi var.

### Yöntem 1: appsettings.json Değiştir (Dosya Bazlı)

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.EntityFrameworkCore": "Warning",
        "MyApp.Services.OdemeService": "Debug"
      }
    }
  }
}
// Override → belirli namespace'lerin seviyesini ayrı ayarla
// EF Core logları çok gürültülü → Warning'e çek
// OdemeService'te bug var → Debug'a aç
```

```csharp
builder.Configuration.AddJsonFile("appsettings.json", reloadOnChange: true);
// reloadOnChange: true → dosyayı değiştirdiğinde uygulama otomatik yeniden okur
// restart etmene gerek yok — dosyayı kaydet, 1-2 saniye içinde aktif olur
```

### Yöntem 2: API Endpoint ile Anlık Değiştir (Programatik)

```csharp
// LoggingLevelSwitch — runtime'da seviye kontrolü:
var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);

builder.Host.UseSerilog((context, config) =>
{
    config.MinimumLevel.ControlledBy(levelSwitch);
    // ne yapar → levelSwitch değiştiğinde log seviyesi anında değişir
    // restart, redeploy, dosya değiştirme yok
});

// Admin API endpoint:
app.MapPost("/admin/log-level", [Authorize(Roles = "Admin")] (string level) =>
{
    levelSwitch.MinimumLevel = Enum.Parse<LogEventLevel>(level);
    return Results.Ok($"Log seviyesi '{level}' olarak ayarlandı");
});
// Kullanım: POST /admin/log-level?level=Debug
// → anında debug logları akmaya başlar
// Hata bulunduktan sonra: POST /admin/log-level?level=Information
// → debug logları durur, performans normale döner
// neden faydalı → production'da 5 dk debug aç, bul, kapat — downtime yok
```

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de:
- `Console.WriteLine` veya basit `_logger.LogInformation($"...")` — düz string, arama/filtreleme yok
- Correlation ID yok — "bu hata hangi istekle ilgili?" sorusuna cevap veremezsin
- Hassas veri kontrolü yok — geliştirici şifreyi yanlışlıkla loglarsa kimse fark etmez
- Log seviyesi değiştirmek = yeniden deploy

500 kullanıcıda logları terminalden okuyabilirsin. 50K kullanıcıda günde milyonlarca log satırı var — structured logging + analiz aracı olmadan bir şey bulamazsın.

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| Structured logging | Her zaman — temel alışkanlık | Zorunlu — log analiz aracı ile birlikte |
| Enricher'lar (Machine, Thread) | Tek instance'ta az fayda | Multi-instance'ta "hangi sunucu?" sorusunu çözer |
| Correlation ID | İyi alışkanlık | Zorunlu — istek takibi |
| W3C TraceContext / OpenTelemetry | Gereksiz (tek servis) | Distributed system'da zorunlu |
| Hassas veri maskeleme | Güvenlik için her zaman yap | KVKK/GDPR compliance zorunlu |
| Dinamik log seviyesi | Güzel ama nadir lazım olur | Production debug için kritik |

---

## Kontrol Soruları

1. `$"User {userId}"` ile `"User {UserId}", userId` arasındaki fark nedir? Neden ikincisi tercih edilir?
2. Enricher ne yapar? Postane analojisiyle açıkla.
3. Correlation ID, TraceId ve Activity.Id arasındaki fark nedir? Her birini ne zaman kullanırsın?
4. W3C traceparent header'ı servisler arasında nasıl otomatik yayılır?
5. Bir geliştirici `{@OdemeDto}` yazdığında kredi kartı loga yazılmasını nasıl engellersin?
6. Production'da uygulamayı restart etmeden log seviyesini nasıl değiştirirsin?
7. LogContext.PushProperty ne yapar? Neden middleware'de using bloğu içinde kullanılır?
