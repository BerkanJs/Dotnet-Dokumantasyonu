# Gün 28 — IHttpClientFactory ve Typed HttpClient

---

## 1. Problem: `new HttpClient()` Neden Tehlikeli?

Bir ASP.NET Core uygulamasında dış bir API'ye istek atmak gerektiğinde akla ilk gelen şey `new HttpClient()` yazmaktır. Ancak bu yaklaşım prodüksiyon ortamında ciddi sorunlara yol açar.

### Sorun 1: Socket Exhaustion (Port Tükenmesi)

```
Senaryo: E-ticaret sitemizde her ödeme işleminde ödeme API'sine HTTP isteği atılıyor.

[Normal saatte]
  Kullanıcı ödeme yapar → new HttpClient() → istek → yanıt → HttpClient.Dispose()
  Socket: TIME_WAIT durumunda → OS bunu 2-4 dakika tutar
  Sorun yok gibi görünüyor...

[Black Friday — 1000 eş zamanlı ödeme]
  1000 x new HttpClient() → 1000 yeni TCP bağlantısı
  Dispose edilse de: 1000 socket TIME_WAIT modunda bekliyor
  OS'un maksimum port sayısı: ~65.000 (kullanılabilir: ~28.000)
  2 dakika içinde 28.000 port doldu → "Unable to connect: Address in use"
  Uygulama çöktü, ödeme yapılamıyor, müşteriler bekliyor.

Gerçek olay: Microsoft, .NET 2.0 döneminde bu hatayı resmi dokümantasyona
"HttpClient guidelines and best practices" başlığıyla ekledi.
```

```csharp
// YANLIŞ — Her istek için yeni HttpClient:
public class OdemeServisi
{
    public async Task<bool> OdemeDenetle(decimal tutar)
    {
        using var client = new HttpClient();
        // "using" ile dispose etmek aslında problemi çözmüyor!
        // Socket TIME_WAIT: OS seviyesinde, .NET'in kontrolü dışında.
        // Dispose: sadece managed kaynakları temizler; OS soketi hemen bırakmaz.

        var yanit = await client.GetAsync("https://api.odeme.com/dogrula");
        return yanit.IsSuccessStatusCode;
    }
}
```

### Sorun 2: DNS Staleness (Eski DNS Cache)

```
Senaryo: Mikro servis mimarisinde load balancer IP'si değişti.

[Yanlış yaklaşım — static singleton HttpClient]
  private static readonly HttpClient _client = new HttpClient();
  // Uygulama başlarken: api.kitabevi.com → 10.0.1.5 çözümlendi
  // Load balancer 3 gün sonra: api.kitabevi.com → 10.0.1.8 oldu
  // Static client hâlâ 10.0.1.5'e gidiyor → "Connection refused"
  // Uygulama yeniden başlatılana kadar düzelmiyor.

[Doğru yaklaşım — IHttpClientFactory]
  HttpMessageHandler havuzu: 2 dakikada bir DNS yeniden çözümlenir
  10.0.1.8 otomatik algılanır, yeniden başlatma gerekmez.
```

---

## 2. IHttpClientFactory Nasıl Çalışır?

```
[IHttpClientFactory Mimarisi]

  Uygulama kodu               DI Container              HttpMessageHandler Havuzu
  ─────────────────           ───────────────           ──────────────────────────
  factory.CreateClient()  →   Handler Pool Yöneticisi → Handler A (oluşturuldu: 10:00)
                                                     → Handler B (oluşturuldu: 10:02)
                                                     → Handler C (oluşturuldu: 10:04)

  Handler yaşam döngüsü:
    Oluştur → 2 dakika kullan → "süresi doldu" işaretle → yeni handler oluştur → eski dispose et
    ↑______________________________________________________________↑

  CreateClient() her çağrısında:
    Yeni HttpClient instance → ama içindeki HttpMessageHandler havuzdan alınır
    Yani: HttpClient yeni, TCP bağlantısı paylaşımlı → iki sorun da çözülüyor
```

```
Fayda özeti:

  Socket Exhaustion çözümü:
    Handler havuzu → aynı TCP bağlantısı farklı HttpClient'lar tarafından yeniden kullanılır
    1000 eş zamanlı istek → 1000 HttpClient, ama 5-10 TCP bağlantısı

  DNS Stale çözümü:
    Her 2 dakikada handler yenilenir → yeni handler yeni DNS çözümlemesi yapar
    Yapılandırılabilir: HandlerLifetime = TimeSpan.FromMinutes(5) gibi
```

---

## 3. Kurulum

```csharp
// Program.cs

builder.Services.AddHttpClient();
// Bu tek satır: IHttpClientFactory'yi DI container'a kaydeder.
// Bunu yazmadan constructor'da IHttpClientFactory inject etmeye kalkarsak:
//   InvalidOperationException: No service for type 'IHttpClientFactory' registered.
```

---

## 4. Kullanım Biçimi 1: Basic Client

En basit kullanım. Geçici veya tek seferlik çağrılar için.

```csharp
// Controllers/KitapController.cs — kötü örnek (direkt DI yerine factory)

public class KitapController : Controller
{
    private readonly IHttpClientFactory _factory;

    public KitapController(IHttpClientFactory factory)
        => _factory = factory;

    public async Task<IActionResult> DisKaynakDenetle()
    {
        var client = _factory.CreateClient();
        // CreateClient(): her çağrıda yeni HttpClient instance oluşturur.
        // Arka planda handler havuzlanmış — socket exhaustion riski yok.
        // "Basic" kullanım: BaseAddress yok, Header yok, timeout varsayılan (100 saniye).

        var yanit = await client.GetAsync("https://api.example.com/status");
        return Ok(new { StatusCode = (int)yanit.StatusCode });
    }
}
```

```
Basic Client ne zaman tercih edilir?
  ✓ Farklı BaseAddress'lere gidiyorsun (tek URL sabit değil)
  ✓ Test/debug için geçici çağrı
  ✓ Birden fazla farklı API ile iletişim (her biri farklı yapılandırma gerektirir)

Basic Client ne zaman tercih edilmez?
  ✗ Aynı servisi birden fazla yerden çağırıyorsun → Named Client kullan
  ✗ HTTP çağrısı iş mantığından ayrılmalı → Typed Client kullan
```

---

## 5. Kullanım Biçimi 2: Named Client

Aynı dış servis birden fazla yerden çağrılıyorsa, yapılandırmayı merkeze alır.

```
Gerçek hayat örneği:
  Kitabevimizdeki fiyat analiz ekibi:
  - PriceController → /api/fiyatlar → https://api.tedarikci.com/v1/
  - StokService     → /api/stok    → https://api.tedarikci.com/v1/
  - RaporServisi    → /api/rapor   → https://api.tedarikci.com/v1/

  Üç farklı yerde aynı BaseAddress ve aynı API Key header'ı yazıyorlar.
  Değişince: 3 yeri güncelle → biri unutulursa bug.
  Named Client: tek yerden yönet.
```

```csharp
// Program.cs — Named Client kaydı

builder.Services.AddHttpClient("TedarikciApi", client =>
{
    client.BaseAddress = new Uri("https://api.tedarikci.com/v1/");
    // BaseAddress: tüm göreli URL'ler buradan türetilir.
    // client.GetAsync("fiyatlar/1234") → https://api.tedarikci.com/v1/fiyatlar/1234

    client.DefaultRequestHeaders.Add("X-Api-Key", "gizli-anahtar-buraya");
    // Her isteğe otomatik eklenen header — API kimlik doğrulaması.
    // Bunu her çağrı noktasına yazmak: bir yer unutulursa 401 Unauthorized.

    client.DefaultRequestHeaders.Add("Accept", "application/json");
    // JSON yanıt bekliyoruz — content negotiation.

    client.Timeout = TimeSpan.FromSeconds(15);
    // 15 saniye timeout: varsayılan 100 saniye çok uzun — dış servis yavaşlarsa
    // thread 100 saniye bloke kalır. 15 saniye: daha hızlı hata tespiti.
    // Bunu set etmeseydin: tek yavaş dış servis tüm iş parçacıklarını 100s bloke eder.
});
```

```csharp
// Kullanım — farklı controller'lar aynı ismi kullanır

public class FiyatController : Controller
{
    private readonly IHttpClientFactory _factory;
    public FiyatController(IHttpClientFactory factory) => _factory = factory;

    public async Task<IActionResult> KarsilastirFiyat(string isbn)
    {
        var client = _factory.CreateClient("TedarikciApi");
        // "TedarikciApi": Program.cs'teki kayıt adıyla eşleşmeli.
        // Yanlış isim: factory yeni boş client oluşturur (exception değil!) — dikkat.

        var fiyatlar = await client.GetFromJsonAsync<List<TedarikFiyati>>($"fiyatlar/{isbn}");
        return Ok(fiyatlar);
    }
}
```

---

## 6. Kullanım Biçimi 3: Typed Client (En İyi Pratik)

HTTP mantığını ayrı bir servis sınıfına taşır. Controller veya uygulama katmanı `HttpClient`'ı hiç görmez.

```
Gerçek hayat örneği:
  Aynı tedarikçi API'si 5 controller'dan çağrılıyor.
  Bir gün API URL'si değişiyor: v1 → v2.
  
  Named Client: Program.cs'te tek satır değişir → iyi.
  Typed Client: Ayrıca işlem detayları (hata yönetimi, retry log) da tek yerde.
  Controller kodu: hiçbir şey değişmez → mükemmel.
```

```csharp
// Services/KitapApiIstemcisi.cs — interface tanımı (test edilebilirlik için)

public interface IKitapApiIstemcisi
{
    Task<List<DisFiyatDto>?> FiyatKarsilastirAsync(string isbn);
    Task<bool>               StokSorgulaAsync(string isbn);
}

// record: immutable, value equality — response DTO'lar için ideal.
public record DisFiyatDto(string Tedarikci, decimal Fiyat, bool Stokta);
```

```csharp
// Services/KitapApiIstemcisi.cs — implementasyon

public class KitapApiIstemcisi : IKitapApiIstemcisi
{
    private readonly HttpClient _client;
    private readonly ILogger<KitapApiIstemcisi> _logger;

    public KitapApiIstemcisi(HttpClient client, ILogger<KitapApiIstemcisi> logger)
    {
        _client = client;
        _logger = logger;
        // HttpClient doğrudan inject edilir — factory değil.
        // DI altyapısı arka planda AddHttpClient<KitapApiIstemcisi>'i kullanır.
        // Biz sadece hazır HttpClient'ı alırız.
    }

    public async Task<List<DisFiyatDto>?> FiyatKarsilastirAsync(string isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            throw new ArgumentException("ISBN boş olamaz.", nameof(isbn));
        // Guard clause: geçersiz giriş API'ye ulaşmadan erken reddedilir.

        try
        {
            var yanit = await _client.GetAsync($"fiyatlar/{isbn}");
            // BaseAddress Program.cs'te set edilmiş — göreli URL yeterli.

            if (yanit.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("ISBN {Isbn} için dış fiyat bulunamadı.", isbn);
                return null;
                // 404: iş kuralı (kitap yok), exception değil null dön.
                // EnsureSuccessStatusCode() yazmak: 404'ü de exception yapardı — yanlış.
            }

            yanit.EnsureSuccessStatusCode();
            // 4xx/5xx: HttpRequestException fırlatır.
            // Bunu yazmassaydık: hatalı yanıtı başarılı sanıp deserialize etmeye çalışırız.

            return await yanit.Content.ReadFromJsonAsync<List<DisFiyatDto>>();
            // ReadFromJsonAsync: JSON'ı otomatik deserialize eder.
            // Manuel yol 3 satır: ReadAsStringAsync + JsonSerializer.Deserialize.
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Dış fiyat API erişilemedi. ISBN: {Isbn}", isbn);
            return null;
            // Ağ hatası: null dön — çağıran kod "API erişilemiyor" durumunu yönetir.
            // Exception fırlatmak: kullanıcıya 500 dönebilir — kötü UX.
        }
    }

    public async Task<bool> StokSorgulaAsync(string isbn)
    {
        try
        {
            var yanit = await _client.GetAsync($"stok/{isbn}");
            if (!yanit.IsSuccessStatusCode) return false;

            var sonuc = await yanit.Content.ReadFromJsonAsync<StokYaniti>();
            return sonuc?.Stokta ?? false;
            // null-coalescing: API null dönerse → false (stok yok kabul et — güvenli taraf).
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stok sorgulama başarısız. ISBN: {Isbn}", isbn);
            return false;
        }
    }
}

// file modifier: sadece bu dosyada görünür — dışa açılmasına gerek yok.
file record StokYaniti(bool Stokta);
```

```csharp
// Program.cs — Typed Client kaydı

builder.Services.AddHttpClient<KitapApiIstemcisi>(client =>
{
    client.BaseAddress = new Uri("https://api.tedarikci.com/v1/");
    client.Timeout     = TimeSpan.FromSeconds(15);
});

// Typed Client: interface üzerinden DI'a da kaydet (test edilebilirlik)
builder.Services.AddScoped<IKitapApiIstemcisi, KitapApiIstemcisi>();
// Bu kayıt AddHttpClient<KitapApiIstemcisi> ile birlikte çalışır:
// IKitapApiIstemcisi inject edildiğinde → KitapApiIstemcisi verilir
// KitapApiIstemcisi'ne HttpClient inject edildiğinde → factory'den configured client gelir
```

```csharp
// Controller — HttpClient'ı hiç görmüyor

public class PazarController : Controller
{
    private readonly IKitapApiIstemcisi _kitapApi;

    public PazarController(IKitapApiIstemcisi kitapApi)
        => _kitapApi = kitapApi;
    // Typed Client sayesinde: HTTP detayı soyutlandı.
    // Test ortamında: IKitapApiIstemcisi mock'lanır, gerçek HTTP çıkmaz.

    public async Task<IActionResult> FiyatKarsilastir(string isbn)
    {
        var fiyatlar = await _kitapApi.FiyatKarsilastirAsync(isbn);

        if (fiyatlar is null)
            return NotFound("Bu ISBN için fiyat bilgisi bulunamadı.");

        return Ok(fiyatlar);
    }
}
```

---

## 7. DelegatingHandler — Request Pipeline (Middleware Benzeri)

Tüm HTTP isteklerini kesen ve işleyen ara katman. Cross-cutting concern'leri (loglama, auth, retry) her servise ayrı ayrı yazmak yerine bir kez tanımlanır.

```
Handler Zinciri:

  client.GetAsync("fiyatlar/123")
          ↓
  [CorrelationIdHandler]  → X-Correlation-Id header ekle
          ↓
  [LoggingHandler]        → istek gidecek, süre ölçülüyor
          ↓
  [AuthHandler]           → Bearer token header ekle
          ↓
  [Gerçek HTTP Ağı]       → https://api.tedarikci.com/v1/fiyatlar/123
          ↑
  [LoggingHandler]        → yanıt geldi, süre loglandı
          ↑
  Yanıt çağıran koda döner
```

```csharp
// Services/HttpHandlers/LoggingHandler.cs

public class LoggingHandler : DelegatingHandler
{
    private readonly ILogger<LoggingHandler> _logger;
    public LoggingHandler(ILogger<LoggingHandler> logger) => _logger = logger;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        // Kısa UUID: eş zamanlı istekleri log'da ayırt etmek için.
        // "N": kısa format (tireler yok), [..8]: ilk 8 karakter yeterince benzersiz.

        _logger.LogInformation("[{Id}] → {Method} {Uri}", requestId, request.Method, request.RequestUri);

        var sw = Stopwatch.StartNew();
        // Stopwatch: DateTime.Now'dan çok daha hassas; yüksek çözünürlüklü zamanlayıcı.

        HttpResponseMessage yanit;
        try
        {
            yanit = await base.SendAsync(request, ct);
            // base.SendAsync: MUTLAKA çağrılmalı — zincirdeki bir sonraki handler.
            // Bu çağrı olmadan istek ağa çıkmaz, yanıt dönmez.
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[{Id}] ✗ {ElapsedMs}ms — ağ hatası", requestId, sw.ElapsedMilliseconds);
            throw;
            // throw: exception'ı yutmuyoruz, sadece logluyoruz.
            // Bunu yazmasaydık exception kaybolur, çağıran kod başarılı sanırdı.
        }

        sw.Stop();
        var level = (int)yanit.StatusCode >= 500 ? LogLevel.Error
                  : (int)yanit.StatusCode >= 400 ? LogLevel.Warning
                  : LogLevel.Information;
        _logger.Log(level, "[{Id}] ← {Status} {ElapsedMs}ms", requestId, (int)yanit.StatusCode, sw.ElapsedMilliseconds);

        return yanit;
    }
}
```

```csharp
// Services/HttpHandlers/AuthHandler.cs

public class AuthHandler : DelegatingHandler
{
    private readonly ITokenServisi _tokenServisi;
    public AuthHandler(ITokenServisi tokenServisi) => _tokenServisi = tokenServisi;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _tokenServisi.TokenGetirAsync();
        // Token servisi: cache'den geçerli token verir, süresi dolduysa yeniler.

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        // Bearer token: OAuth 2.0 standardı.
        // Bunu her servis metoduna yazmak yerine tek handler'da merkezi.

        return await base.SendAsync(request, ct);
    }
}
```

```csharp
// Services/HttpHandlers/CorrelationIdHandler.cs

public class CorrelationIdHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (!request.Headers.Contains("X-Correlation-Id"))
            request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
        // Mevcut ID varsa koru — zincirin ortasındayız, ID oluşturulmuş olabilir.
        // Yok ise yeni oluştur: bu ilk servis çağrısı.

        return await base.SendAsync(request, ct);
    }
}
```

```csharp
// Program.cs — handler'ları kaydet ve zincire ekle

builder.Services.AddTransient<LoggingHandler>();
builder.Services.AddTransient<AuthHandler>();
builder.Services.AddTransient<CorrelationIdHandler>();
// Transient: her kullanımda yeni handler — DelegatingHandler için uygun lifetime.
// Singleton yapmak: state paylaşımı riski (thread-safety sorunu).

builder.Services.AddHttpClient<KitapApiIstemcisi>(client =>
{
    client.BaseAddress = new Uri("https://api.tedarikci.com/v1/");
})
.AddHttpMessageHandler<CorrelationIdHandler>()  // ← önce ekle: ilk çalışacak
.AddHttpMessageHandler<LoggingHandler>()         // ← sonra: correlation ID logda görünür
.AddHttpMessageHandler<AuthHandler>();           // ← son: auth token eklenir
// Zincirleme sırası: ekleme sırasının tersidir (LIFO — son eklenen ilk çalışır).
// Daha doğrusu: AddHttpMessageHandler sırası = istek gidişindeki sıra.
```

---

## 8. .NET 8 — AddStandardResilienceHandler

```csharp
// Program.cs — .NET 8+ tek satır resilience

builder.Services.AddHttpClient<KitapApiIstemcisi>(client =>
{
    client.BaseAddress = new Uri("https://api.tedarikci.com/v1/");
})
.AddStandardResilienceHandler();
// Tek satır → şunları içerir:
//   ✓ Retry: geçici hatalarda 3 kez dene (exponential backoff)
//   ✓ Circuit Breaker: 5 ardışık hata → 30 saniye devre aç
//   ✓ Timeout: istek başına 30 saniye
//   ✓ Rate Limiter: eş zamanlı istek sınırı

// Özelleştirmek:
.AddStandardResilienceHandler(opts =>
{
    opts.Retry.MaxRetryAttempts = 5;
    opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
});
```

```
Neden Retry gerekli?
  Gerçek senaryo: Ödeme API'si Türkiye'deki altyapı sorununda 2-3 saniye yanıt vermiyor.
  Retry olmadan: müşteri hata alır, işlem başarısız.
  Retry ile: 1. deneme başarısız → 2 saniye bekle → 2. deneme başarılı.
  Kullanıcı fark etmez bile.

Neden Circuit Breaker gerekli?
  Gerçek senaryo: Stok API'si çöktü, 500 dönüyor.
  Retry olmadan: her istek direkt 500 alır → kullanıcı hata görür.
  Retry + Circuit Breaker:
    5 ardışık hata → devre açılır (30 saniye)
    Bu 30 saniyede: istek stok API'sine hiç gitmiyor → hızlı hata → daha az kaynak harcanır
    30 saniye sonra: devre kapanır, 1 test isteği → başarısız? → devre tekrar açılır
    Başarılı? → normal operasyon devam eder.
```

---

## 9. Kullanım Biçimi Seçim Rehberi

```
┌─────────────────────────────────────────────────────────────────────────┐
│                   Hangi Pattern Ne Zaman?                                │
├──────────────────┬──────────────────────────────────────────────────────┤
│ Basic Client     │ Farklı URL'lere geçici istek                          │
│                  │ Test/debug amaçlı                                     │
│                  │ Yapılandırma gerektirmeyen tek seferlik çağrı         │
├──────────────────┼──────────────────────────────────────────────────────┤
│ Named Client     │ Aynı BaseAddress birden fazla yerde kullanılıyor      │
│                  │ Merkezi header/timeout yapılandırması lazım            │
│                  │ Ama HTTP mantığı controller'da kalabilir               │
├──────────────────┼──────────────────────────────────────────────────────┤
│ Typed Client ◄── │ HTTP mantığını ayrı serviste saklamak istiyorsun      │
│ GENEL TERCİH     │ Mock edilebilmeli (integration test / unit test)       │
│                  │ Consumer HttpClient'ı hiç görmemeli                   │
│                  │ DelegatingHandler ile zenginleştirilecek               │
└──────────────────┴──────────────────────────────────────────────────────┘
```

---

## 10. Java / Spring Karşılaştırması

```
Spring RestTemplate (eski, senkron):
  RestTemplate restTemplate = new RestTemplate();
  String sonuc = restTemplate.getForObject("https://api.example.com/data", String.class);

.NET karşılığı (YANLIŞ yaklaşım):
  var client = new HttpClient();
  var sonuc = await client.GetStringAsync("https://api.example.com/data");

─────────────────────────────────────────────────────────────────────────────

Spring WebClient (reaktif, modern):
  WebClient client = WebClient.builder()
      .baseUrl("https://api.tedarikci.com/v1/")
      .defaultHeader("X-Api-Key", "gizli")
      .build();
  Mono<String> sonuc = client.get().uri("fiyatlar/{isbn}", isbn)
      .retrieve().bodyToMono(String.class);

.NET karşılığı (DOĞRU — Typed Client):
  // Program.cs'te:
  builder.Services.AddHttpClient<KitapApiIstemcisi>(c => {
      c.BaseAddress = new Uri("https://api.tedarikci.com/v1/");
      c.DefaultRequestHeaders.Add("X-Api-Key", "gizli");
  });
  // Servis içinde:
  var sonuc = await _client.GetStringAsync($"fiyatlar/{isbn}");

─────────────────────────────────────────────────────────────────────────────

Spring @FeignClient (declarative — en benzer):
  @FeignClient(name = "tedarikci", url = "https://api.tedarikci.com/v1/")
  public interface TedarikciClient {
      @GetMapping("/fiyatlar/{isbn}")
      List<DisFiyat> getFiyatlar(@PathVariable String isbn);
  }

.NET karşılığı (Typed Client + Interface):
  public interface IKitapApiIstemcisi {
      Task<List<DisFiyatDto>?> FiyatKarsilastirAsync(string isbn);
  }
  // FeignClient: interface → oto-implementasyon (framework yazar kodu)
  // .NET: interface + elle yazılmış implementasyon (daha fazla kontrol)

─────────────────────────────────────────────────────────────────────────────

Resilience4j → Polly / AddStandardResilienceHandler:

  @Retry(maxAttempts = 3, backoff = @Backoff(delay = 2000))
  @CircuitBreaker(name = "tedarikci", fallbackMethod = "fallback")
  public List<DisFiyat> getFiyatlar(String isbn) { ... }

  ↓

  builder.Services.AddHttpClient<KitapApiIstemcisi>(...)
      .AddStandardResilienceHandler(); // tek satır — retry + circuit breaker + timeout
```

---

## 11. Özet

```
IHttpClientFactory
  ├── Socket Exhaustion: handler havuzlar → aynı TCP bağlantısı paylaşılır
  ├── DNS Stale: handler 2 dakikada bir yenilenir → yeni DNS çözümlemesi
  └── Her CreateClient(): yeni HttpClient, paylaşımlı handler

Kullanım Biçimleri
  ├── Basic   → factory.CreateClient()
  ├── Named   → factory.CreateClient("isim") + Program.cs'te AddHttpClient("isim", ...)
  └── Typed   → sınıfa HttpClient inject et + AddHttpClient<Sınıf>(...)

DelegatingHandler (Pipeline)
  ├── CorrelationIdHandler → X-Correlation-Id ekle (distributed tracing)
  ├── LoggingHandler       → istek/yanıt logla, süre ölç
  └── AuthHandler          → Bearer token ekle

Resilience (.NET 8+)
  └── .AddStandardResilienceHandler() → retry + circuit breaker + timeout tek satır

Kod Dosyaları (bu projede)
  ├── KitabeviMVC/Services/HttpHandlers/LoggingHandler.cs
  ├── KitabeviMVC/Services/HttpHandlers/AuthHandler.cs
  ├── KitabeviMVC/Services/HttpHandlers/CorrelationIdHandler.cs
  └── KitabeviMVC/Services/KitapApiIstemcisi.cs
```

---

## Sonraki Gün

Gün 29'da EF Core mimarisi: DbContext ve Change Tracker. Entity state'leri (Added, Modified, Deleted, Unchanged), SaveChanges transaction sınırı, AsNoTracking performans etkisi.
