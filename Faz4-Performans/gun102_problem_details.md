# Gün 102 — Problem Details RFC 9457 ve API Hata Standartları

---

## Sorun: Her API Hataları Farklı Formatta Dönüyor

Bir API'den hata aldığında yanıt ne şekilde geliyor? Her API kendi kafasına göre yapıyor:

```json
// API 1:
{ "error": "Kitap bulunamadı" }

// API 2:
{ "code": 404, "msg": "not found", "success": false }

// API 3:
{ "errors": ["Fiyat negatif olamaz"], "status": "fail" }
```

Client (frontend, mobil app) her API için farklı hata parse mantığı yazmak zorunda. Yeni bir API entegre ettiğinde "bu sefer hata hangi alanda?" diye tahmin ediyor.

**Çözüm:** Herkesin uyduğu bir standart — **RFC 9457 (Problem Details).** Hata yanıtının formatı standartlaştırılmış, tüm API'lar aynı yapıda hata dönüyor.

---

## RFC 7807 → RFC 9457 — Ne Değişti?

RFC 7807 (2016) ilk standart. RFC 9457 (Temmuz 2023) bunun yerini aldı. İçerik neredeyse aynı ama:
- Resmi standart oldu (artık "proposed" değil "Internet Standard")
- Açıklamalar netleşti
- `type` alanının `about:blank` varsayılan değeri vurgulandı

Pratikte: ikisi arasında breaking change yok, RFC 9457 güncel referans.

---

## ProblemDetails Anatomisi — 5 Temel Alan

Bir hata yanıtında şu alanlar standart:

```json
{
  "type": "https://api.kitapapp.com/errors/out-of-stock",
  "title": "Ürün stokta yok",
  "status": 409,
  "detail": "Kitap 'Clean Code' (id: 42) şu anda stokta bulunmamaktadır. Tahmini stok tarihi: 2026-05-15.",
  "instance": "/api/siparisler/99"
}
```

Her alanın ne anlama geldiği:

| Alan | Ne anlatıyor | Örnek |
|------|-------------|-------|
| **type** | Hatanın türünü tanımlayan URI. Client buna bakarak "ne tip hata?" sorusuna cevap verir | `https://api.kitapapp.com/errors/out-of-stock` |
| **title** | Hatanın kısa, insan tarafından okunabilir başlığı. Her zaman aynı cümle (değişken veri koyma) | `"Ürün stokta yok"` |
| **status** | HTTP status code (yanıtın header'ındaki ile aynı olmalı) | `409` |
| **detail** | Bu spesifik hatanın detayı. Değişken veri burada olur | `"Kitap 'Clean Code' (id: 42) stokta yok..."` |
| **instance** | Hatanın oluştuğu spesifik kaynak (hangi request, hangi endpoint) | `"/api/siparisler/99"` |

### title vs detail — Fark Ne?

- **title** sabit bir cümle — hata türünün genel açıklaması. Her "stok yok" hatasında aynı title.
- **detail** değişken — bu spesifik durumun detayı. Hangi kitap, hangi id, ne zaman gelecek.

Client kodu title'a göre karar verir (if/switch), detail'i kullanıcıya gösterir.

### type URI — Hata Kataloğu

`type` alanı bir URL. Bu URL'nin çalışan bir sayfa olması zorunlu değil ama best practice olarak hata açıklaması sayfasına yönlendirebilirsin. Client bu URI'yi string olarak karşılaştırır:

```javascript
// Frontend'de:
if (error.type === "https://api.kitapapp.com/errors/out-of-stock") {
    showStockNotification(error.detail);
} else if (error.type === "https://api.kitapapp.com/errors/insufficient-balance") {
    redirectToPaymentPage();
}
// Client hata tipine göre farklı davranır — string'e bakarak, status code'a değil
// Neden status code yetmiyor → 409 "conflict" birçok farklı hata olabilir
// type ile hangi conflict olduğunu kesin bilirsin
```

`type` belirtilmezse varsayılan: `"about:blank"` — "genel HTTP hatası, özel bir tip yok" demek.

---

## ASP.NET Core'da ProblemDetails — Built-in Destek

### Temel Kurulum

```csharp
// Program.cs:
builder.Services.AddProblemDetails();
// ne yapar → tüm hata yanıtlarını otomatik ProblemDetails formatına çevirir
// bunu yazmasaydık → 500 hatası düz "Internal Server Error" metni döner
// bununla → JSON ProblemDetails objesi döner

app.UseExceptionHandler();
// ne yapar → yakalanmamış exception'ları ProblemDetails olarak döner
app.UseStatusCodePages();
// ne yapar → 404, 405 gibi framework hatalarını da ProblemDetails formatında döner
// bunu yazmasaydık → boş body ile 404 döner, client ne olduğunu anlamaz
```

Artık herhangi bir yakalanmamış exception veya framework hatası şöyle döner:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500
}
```

### Controller/Minimal API'da Manuel ProblemDetails

```csharp
// Controller'da:
[HttpGet("kitaplar/{id}")]
public async Task<IActionResult> Get(int id)
{
    var kitap = await _repo.GetAsync(id);
    if (kitap is null)
    {
        return Problem(
            type: "https://api.kitapapp.com/errors/not-found",
            title: "Kaynak bulunamadı",
            detail: $"Id={id} olan kitap bulunamadı.",
            statusCode: 404,
            instance: HttpContext.Request.Path);
        // ne yapar → standart ProblemDetails formatında 404 döner
        // bunu yazmasaydık → return NotFound() sadece boş 404 döner, client detay alamaz
    }
    return Ok(kitap);
}

// Minimal API'da:
app.MapGet("/kitaplar/{id}", async (int id, IKitapRepo repo) =>
{
    var kitap = await repo.GetAsync(id);
    if (kitap is null)
        return Results.Problem(
            type: "https://api.kitapapp.com/errors/not-found",
            title: "Kaynak bulunamadı",
            detail: $"Id={id} olan kitap bulunamadı.",
            statusCode: 404);

    return Results.Ok(kitap);
});
```

---

## Extension Fields — Domain'e Özgü Bilgi Ekleme

Standart 5 alan yetmeyebilir. Kendi alanlarını ekleyebilirsin:

```csharp
app.MapPost("/siparisler", async (SiparisDto dto, ISiparisService svc) =>
{
    var stok = await svc.StokKontrolAsync(dto.KitapId);
    if (stok <= 0)
    {
        return Results.Problem(new ProblemDetails
        {
            Type = "https://api.kitapapp.com/errors/out-of-stock",
            Title = "Ürün stokta yok",
            Status = 409,
            Detail = $"Kitap (id: {dto.KitapId}) stokta bulunmamaktadır.",
            Extensions =
            {
                ["kitapId"] = dto.KitapId,
                ["tapiminStokTarihi"] = "2026-05-15",
                ["alternativeKitaplar"] = new[] { 101, 102, 103 }
            }
            // ne yapar → standart alanların yanına domain'e özel bilgiler ekler
            // client: "stok yok, ama alternatif kitaplar var" bilgisini alır
            // bunu yazmasaydık → client sadece "stokta yok" bilir, alternatif sunamaz
        });
    }
    // ...
});
```

Yanıt:
```json
{
  "type": "https://api.kitapapp.com/errors/out-of-stock",
  "title": "Ürün stokta yok",
  "status": 409,
  "detail": "Kitap (id: 42) stokta bulunmamaktadır.",
  "kitapId": 42,
  "tapiminStokTarihi": "2026-05-15",
  "alternativeKitaplar": [101, 102, 103]
}
```

---

## ValidationProblemDetails — Validation Hatalarını Standartlaştırma

Validation hataları birden fazla alana ait olabilir. Bunun için özel bir ProblemDetails alt tipi var:

```csharp
// Controller'da [ApiController] attribute'u otomatik ValidationProblemDetails döner:
[ApiController]
public class KitaplarController : ControllerBase
{
    [HttpPost]
    public IActionResult Create(KitapEkleDto dto)
    {
        // ModelState geçersizse [ApiController] otomatik 400 döner:
        // elle yapmana gerek yok
    }
}
```

Otomatik yanıt:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Ad": ["Kitap adı boş olamaz."],
    "Fiyat": ["Fiyat 0'dan büyük olmalıdır.", "Fiyat 10000'den küçük olmalıdır."]
  }
}
```

`errors` alanı alan adına göre gruplanmış hata mesajları içerir. Frontend bu yapıyı direkt form alanlarıyla eşleştirebilir.

```javascript
// Frontend'de:
if (error.status === 400 && error.errors) {
    Object.entries(error.errors).forEach(([field, messages]) => {
        showFieldError(field, messages[0]); // "Ad" input'unun altına "Kitap adı boş olamaz" yaz
    });
}
```

---

## IExceptionHandler — Exception → ProblemDetails Mapping

ASP.NET Core 8+ ile gelen yeni yaklaşım. Tüm exception'ları merkezi bir yerde ProblemDetails'e çevir:

```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        var problem = exception switch
        {
            NotFoundException ex => new ProblemDetails
            {
                Type = "https://api.kitapapp.com/errors/not-found",
                Title = "Kaynak bulunamadı",
                Detail = ex.Message,
                Status = 404
            },
            BusinessRuleException ex => new ProblemDetails
            {
                Type = "https://api.kitapapp.com/errors/business-rule-violation",
                Title = "İş kuralı ihlali",
                Detail = ex.Message,
                Status = 422
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Type = "https://api.kitapapp.com/errors/unauthorized",
                Title = "Yetkisiz erişim",
                Status = 403
            },
            _ => new ProblemDetails
            {
                Type = "https://api.kitapapp.com/errors/internal",
                Title = "Sunucu hatası",
                Detail = "Beklenmeyen bir hata oluştu.",
                Status = 500
            }
        };
        // ne yapar → her exception tipini uygun ProblemDetails'e çevirir
        // bunu yazmasaydık → tüm hatalar generic 500 döner, client ne olduğunu bilemez

        _logger.LogError(exception, "Hata: {Type}", problem.Type);

        context.Response.StatusCode = problem.Status ?? 500;
        await context.Response.WriteAsJsonAsync(problem, ct);

        return true;  // true → exception handle edildi, pipeline'a devam etme
    }
}

// Kayıt:
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
app.UseExceptionHandler();
```

**Neden IExceptionHandler?**
- Eski yol: `app.UseExceptionHandler("/error")` + error controller → karmaşık
- Yeni yol: DI-friendly, test edilebilir, birden fazla handler zincirlenebilir
- Her exception tipi kendi ProblemDetails'ini alır — client hata tipine göre davranır

---

## Custom IProblemDetailsWriter

ProblemDetails JSON çıktısını özelleştirmek istiyorsan:

```csharp
builder.Services.AddProblemDetails(opt =>
{
    opt.CustomizeProblemDetails = context =>
    {
        // Her ProblemDetails'e ek bilgi ekle:
        context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id;
        context.ProblemDetails.Extensions["timestamp"] = DateTime.UtcNow;
        // ne yapar → tüm hata yanıtlarına trace ID ve zaman damgası eklenir
        // destek ekibi: "hata trace ID'nizi paylaşır mısınız?" → logu anında bulur
        // bunu yazmasaydık → kullanıcı hata aldığında hangi log kaydına ait olduğu belli olmaz

        // Production'da stack trace gizle:
        if (!context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            context.ProblemDetails.Extensions.Remove("exception");
        }
        // ne yapar → development'ta detaylı hata, production'da güvenli hata
    };
});
```

---

## API Deprecation — Endpoint Kullanımdan Kaldırma

API versiyonlama yapıyorsun, eski endpoint'i kaldırmak istiyorsun. Ama client'lar hâlâ kullanıyor — birden kapatırsan kırılırlar. Standart yaklaşım: önce "bu endpoint kalkacak" uyarısı ver, süre tanı, sonra kapat.

### Deprecation ve Sunset Header'ları

```csharp
// Middleware veya filter ile:
app.MapGet("/api/v1/kitaplar", async (IKitapRepo repo) =>
{
    return await repo.ListAsync();
})
.AddEndpointFilter(async (context, next) =>
{
    var response = context.HttpContext.Response;

    response.Headers["Deprecation"] = "true";
    // ne yapar → RFC 8594 — "bu endpoint kullanımdan kalkıyor" sinyali
    // client bu header'ı görünce yeni versiyona geçmeyi planlamalı

    response.Headers["Sunset"] = "Sat, 01 Nov 2026 00:00:00 GMT";
    // ne yapar → "bu tarihten sonra bu endpoint çalışmayacak"
    // client kesin deadline biliyor — o tarihe kadar geçiş yapmalı

    response.Headers["Link"] = "</api/v2/kitaplar>; rel=\"successor-version\"";
    // ne yapar → yeni versiyonun adresini gösterir
    // client otomatik olarak yeni endpoint'e geçebilir

    return await next(context);
});
```

Client yanıtta şunları görür:
```http
HTTP/1.1 200 OK
Deprecation: true
Sunset: Sat, 01 Nov 2026 00:00:00 GMT
Link: </api/v2/kitaplar>; rel="successor-version"
```

### ASP.NET API Versioning ile Deprecated İşaretleme

```csharp
// NuGet: Asp.Versioning.Http
builder.Services.AddApiVersioning(opt =>
{
    opt.DefaultApiVersion = new ApiVersion(2, 0);
    opt.ReportApiVersions = true;
    // ne yapar → yanıt header'ında desteklenen versiyonları listeler
    // api-supported-versions: 2.0
    // api-deprecated-versions: 1.0
});

// Controller'da:
[ApiVersion("1.0", Deprecated = true)]   // v1 deprecated
[ApiVersion("2.0")]                       // v2 aktif
[ApiController]
[Route("api/v{version:apiVersion}/kitaplar")]
public class KitaplarController : ControllerBase
{
    [HttpGet]
    [MapToApiVersion("1.0")]
    public IActionResult GetV1()
    {
        // eski format — hâlâ çalışıyor ama deprecated
        return Ok(legacyFormat);
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public IActionResult GetV2()
    {
        // yeni format
        return Ok(modernFormat);
    }
}
// ne yapar → v1 çalışır ama yanıtta "deprecated" bilgisi gider
// client SDK'ları bu bilgiyi okuyup uyarı loglar
```

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de:
- Hata yanıtları standart değil — her endpoint farklı format
- Validation hataları ModelState'ten düz string olarak dönüyor
- Exception handler yok — yakalanmamış hata generic 500 sayfası
- API versioning yok — değişiklik breaking change

50K kullanıcıda: frontend ekibi, mobil ekibi, 3. parti entegrasyonlar — hepsinin aynı hata formatını beklemesi lazım.

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| ProblemDetails | İyi alışkanlık — client geliştirme kolaylaşır | Zorunlu — tüm client'lar standart format bekler |
| IExceptionHandler | Merkezi hata yönetimi — her zaman iyi | Zorunlu — farklı exception'lar farklı yanıt |
| Extension fields | Basit senaryoda gereksiz | Domain bilgisi döndürmek için faydalı |
| ValidationProblemDetails | [ApiController] otomatik yapıyor | Form validation UX'i için kritik |
| API deprecation headers | Gereksiz (tek versiyon) | Zorunlu — client'ları kırmadan geçiş |

---

## Kontrol Soruları

1. ProblemDetails'in 5 temel alanı nedir? title ile detail arasındaki fark ne?
2. `type` URI'si neden önemli? Client bu alanı nasıl kullanır?
3. ValidationProblemDetails'in errors alanı nasıl yapılandırılmış? Frontend bunu nasıl kullanır?
4. IExceptionHandler ile eski UseExceptionHandler yaklaşımı arasındaki fark nedir?
5. Extension fields ne zaman eklenir? Örnek bir senaryo ver.
6. Deprecation ve Sunset header'ları ne anlama gelir? Client ne yapmalı?
7. Production'da ProblemDetails'te stack trace neden gizlenmeli?
