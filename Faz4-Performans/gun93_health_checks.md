# Gün 93 — Health Checks ve Readiness/Liveness

---

## Health Check Nedir?

Uygulamanın "sağlıklı mı?" sorusuna cevap veren özel endpoint. Load balancer, Kubernetes veya monitoring aracı bu endpoint'i düzenli aralıklarla çağırır. Yanıt "Healthy" ise trafik gönderilir, "Unhealthy" ise uygulama devre dışı bırakılır.

**Analoji:** Hastanede doktora gittin. Doktor nabzını, tansiyonunu, ateşini ölçtü — hepsi normal ise "sağlıklı." Biri anormalse "tedavi lazım." Health check de uygulamanın nabzını ölçer: DB'ye bağlanabiliyor mu? Redis erişilebilir mi? Disk dolu mu?

**Neden önemli?**
- Uygulama çalışıyor ama DB bağlantısı kopmuş → istekler hata veriyor → health check "Unhealthy" der → load balancer bu instance'a trafik göndermez
- Container çökmedi ama deadlock'a girmiş → liveness probe "Unhealthy" → Kubernetes container'ı yeniden başlatır
- Yeni deploy edilen instance henüz warm-up bitirmemiş → readiness probe "NotReady" → trafik gelmez

---

## Liveness vs Readiness — Fark Ne?

| | Liveness | Readiness |
|---|---|---|
| **Soru** | "Uygulama yaşıyor mu?" | "Uygulama trafik almaya hazır mı?" |
| **Başarısız olursa** | Container yeniden başlatılır (kill + restart) | Trafik gönderilmez ama container öldürülmez |
| **Ne kontrol eder** | Deadlock, infinite loop, process cevap veriyor mu | DB bağlantısı, Redis, dış servisler hazır mı |
| **Ne kadar hafif** | Çok hafif — sadece "yaşıyor musun?" | Daha kapsamlı — bağımlılıkları kontrol eder |

**Analoji:**
- **Liveness** = "Kalbin atıyor mu?" → Hayırsa reanimasyon (restart)
- **Readiness** = "Çalışabilir durumda mısın?" → Hayırsa rapor al, evde kal (trafik gitmez)

**Gerçek senaryo:** Uygulama başladı ama Redis henüz bağlanmadı. Liveness: healthy (uygulama yaşıyor). Readiness: unhealthy (henüz hazır değil). Kubernetes trafik göndermez ama container'ı öldürmez — Redis bağlanınca readiness healthy olur, trafik gelmeye başlar.

---

## ASP.NET Core'da Temel Kurulum

```csharp
// Program.cs
builder.Services.AddHealthChecks();
// ne yapar → health check altyapısını kaydeder
// bunu yazmasaydık → MapHealthChecks çalışmaz

app.MapHealthChecks("/health");
// ne yapar → GET /health endpoint'i oluşturur
// yanıt: 200 OK + "Healthy" veya 503 + "Unhealthy"
// bunu yazmasaydık → monitoring araçlarının kontrol edecek endpoint'i yok
```

Tarayıcıda `/health` → `Healthy` yazısı görürsün. Ama bu sadece "uygulama ayakta" der — bağımlılıkları kontrol etmez.

---

## Veritabanı Health Check

```csharp
// NuGet: Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "readiness" });
// ne yapar → DbContext üzerinden SELECT 1 çalıştırır
// DB erişilebilirse Healthy, değilse Unhealthy
// tags: "readiness" → bu check sadece readiness probe'da çalışsın diye etiketledik
// bunu yazmasaydık → DB çökse bile /health "Healthy" der — yanlış bilgi
```

---

## Custom Health Check — Kendi Kontrolünü Yaz

```csharp
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var latency = await db.PingAsync();
            // ne yapar → Redis'e PING gönderir, yanıt süresini ölçer

            if (latency > TimeSpan.FromSeconds(2))
                return HealthCheckResult.Degraded($"Redis yavaş: {latency.TotalMs}ms");
                // Degraded → çalışıyor ama performans düşük (uyarı seviyesi)

            return HealthCheckResult.Healthy($"Redis OK: {latency.TotalMs}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis erişilemiyor", ex);
            // Unhealthy → tamamen bağlanamıyor
        }
    }
}

// Kayıt:
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "database", tags: new[] { "readiness" })
    .AddCheck<RedisHealthCheck>("redis", tags: new[] { "readiness" });
// ne yapar → /health çağrıldığında hem DB hem Redis kontrol edilir
// biri bile Unhealthy ise genel sonuç Unhealthy olur
```

---

## Liveness ve Readiness İçin Ayrı Endpoint'ler

```csharp
// Liveness — sadece "uygulama yaşıyor mu?" (hiçbir bağımlılık kontrol etme)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
    // ne yapar → hiçbir registered check'i çalıştırma
    // sadece endpoint'in cevap vermesi = uygulama yaşıyor
    // neden → liveness çok hafif olmalı, DB/Redis'e bağımlı olmamalı
    // DB çöktü diye uygulamayı restart etmek yanlış — belki DB geçici koptu
});

// Readiness — bağımlılıklar hazır mı?
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("readiness")
    // ne yapar → sadece "readiness" tag'li check'leri çalıştırır
    // DB ve Redis kontrol edilir — ikisi de OK ise trafik alınabilir
});
```

**Kubernetes'te kullanım:**
```yaml
# deployment.yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 80
  initialDelaySeconds: 10      # başladıktan 10 sn sonra kontrol et
  periodSeconds: 15             # her 15 sn'de bir

readinessProbe:
  httpGet:
    path: /health/ready
    port: 80
  initialDelaySeconds: 5
  periodSeconds: 10
```

---

## Detaylı JSON Yanıt

Varsayılan yanıt sadece "Healthy" text'i. Monitoring için hangi check'in ne durumda olduğunu görmek istersin:

```csharp
app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        };
        await context.Response.WriteAsJsonAsync(result);
    }
    // ne yapar → her check'in ayrı durumunu JSON olarak döner
    // bunu yazmasaydık → sadece "Healthy" veya "Unhealthy" yazar, hangisi sorunlu bilinmez
});
```

**Örnek yanıt:**
```json
{
  "status": "Unhealthy",
  "duration": 245,
  "checks": [
    { "name": "database", "status": "Healthy", "duration": 12 },
    { "name": "redis", "status": "Unhealthy", "description": "Redis erişilemiyor", "duration": 2003 }
  ]
}
```

---

## Health Check UI

`AspNetCore.HealthChecks.UI` paketi ile tarayıcıda görsel dashboard:

```csharp
// NuGet: AspNetCore.HealthChecks.UI, AspNetCore.HealthChecks.UI.InMemory.Storage
builder.Services.AddHealthChecksUI(opt =>
{
    opt.AddHealthCheckEndpoint("API", "/health/detail");
    opt.SetEvaluationTimeInSeconds(30);
    // ne yapar → 30 saniyede bir /health/detail'i çağırıp durumu günceller
})
.AddInMemoryStorage();
// ne yapar → geçmişi bellekte tutar (production'da SQL/Redis kullanılabilir)

app.MapHealthChecksUI(opt => opt.UIPath = "/health-ui");
// ne yapar → /health-ui adresinde görsel dashboard
// yeşil/kırmızı kutularla hangi servis sağlıklı, hangisi değil gösterir
```

---

## Circuit Breaker ile Health Check Entegrasyonu

Bir dış servise (ödeme API, e-posta servisi) bağımlısın. Servis çöktüğünde her istekte timeout bekliyorsun → kendi uygulamanı da yavaşlatıyorsun. Circuit breaker: "bu servis çökmüş, artık denemeyeceğim" der.

Health check + circuit breaker birlikte çalışır:

```csharp
// Polly ile circuit breaker tanımla (NuGet: Microsoft.Extensions.Http.Polly)
builder.Services.AddHttpClient("OdemeServisi", c =>
{
    c.BaseAddress = new Uri("https://api.odeme.com");
})
.AddTransientHttpErrorPolicy(p =>
    p.CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 3,
        // ne yapar → 3 ardışık hata olursa devre keser
        durationOfBreak: TimeSpan.FromSeconds(30)
        // ne yapar → 30 sn boyunca hiç denemez (istek anında hata döner)
        // 30 sn sonra bir kez dener (half-open) → başarılıysa devre kapanır
    ));

// Health check ile birleştir:
public class OdemeHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpFactory;

    public OdemeHealthCheck(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var client = _httpFactory.CreateClient("OdemeServisi");
            var response = await client.GetAsync("/health", ct);
            // ne yapar → ödeme servisinin kendi health endpoint'ini çağırır
            // circuit breaker açıksa → anında hata döner (timeout beklenmez)

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Ödeme servisi erişilebilir")
                : HealthCheckResult.Unhealthy($"Ödeme servisi: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Ödeme servisi erişilemiyor", ex);
        }
    }
}
```

**Akış:**
```
Ödeme servisi çöktü
  → Circuit breaker açıldı (3 hata sonrası)
  → Health check Unhealthy raporlar
  → Dashboard'da kırmızı görürsün
  → 30 sn sonra circuit half-open → tekrar dener
  → Servis düzeldiyse → Healthy, devre kapanır
```

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de health check yok. Uygulama çöktüğünde:
- Kullanıcı hata sayfası görene kadar kimse bilmiyor
- DB bağlantısı koptuysa tüm istekler 500 dönüyor ama load balancer hâlâ trafik gönderiyor
- Yeni deploy sonrası warm-up bitmeden trafik geliyor → ilk kullanıcılar hata alıyor

50K kullanıcıda bu kabul edilemez — monitoring + health check + otomatik failover şart.

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| Temel /health endpoint | İyi alışkanlık | Zorunlu |
| Liveness + Readiness ayrımı | Gereksiz (Kubernetes yoksa) | Container orchestration varsa zorunlu |
| Custom health check (Redis, dış API) | Opsiyonel | Her bağımlılık için yazılmalı |
| Health Check UI | Güzel ama gereksiz | Operasyon ekibi için faydalı |
| Circuit breaker entegrasyonu | Overengineering | Dış servise bağımlılık varsa şart |

---

## Kontrol Soruları

1. Liveness ve readiness probe arasındaki fark nedir? Yanlış kullanımın sonucu ne olur?
2. DB çöktüğünde liveness unhealthy olmalı mı? Neden?
3. Custom health check yazarken Degraded ne zaman dönersin?
4. Health check endpoint'i public mi olmalı, korumalı mı?
5. Circuit breaker "open" durumdayken health check ne raporlar?
