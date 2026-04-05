# Gün 19 — Filters: Cross-Cutting Concerns

---

## 1. Filter Nedir? Neden Var?

Bir web uygulamasında bazı işlemler her action'dan önce veya sonra tekrar eder:

- Her değişikliği logla — kim, ne zaman, hangi kayda ne yaptı
- Kullanıcı giriş yapmış mı kontrol et
- Gelen model geçerli değilse action'ı hiç çalıştırma
- Beklenmedik hata oluşursa düzgün bir JSON yanıt döndür

Bu kodu her action'a yazsaydın yüzlerce kopyası olurdu ve bir şeyi değiştirmen gerektiğinde hepsini değiştirirdin. **Filter**, bu tekrar eden mantığı tek bir yere toplar ve belirli noktalarda otomatik çalıştırır.

Spring'de bunu AOP `@Around`, `@Before`, `@After` ile yapıyordun. ASP.NET'te bunun karşılığı **Filter pipeline**.

---

## 2. Filter Pipeline — Nerede Çalışır?

Bir istek geldiğinde şu sırayla geçer:

```
İstek
  ↓
[ Authorization Filter ]   → Kullanıcı bu kaynağa girebilir mi?
  ↓
[ Resource Filter ]        → Cache'den dön, binding'i atla (nadir kullanılır)
  ↓
  Model Binding            → URL/body → C# nesnesi
  ↓
[ Action Filter — Before ] → Action çalışmadan hemen önce
  ↓
  Action metod çalışır
  ↓
[ Action Filter — After ]  → Action çalıştıktan sonra
  ↓
[ Result Filter ]          → IActionResult execute edilmeden önce/sonra
  ↓
  Response kullanıcıya gönderilir
  ↓
[ Exception Filter ]       → Yukarıdaki adımlardan herhangi birinde
                             yakalanmamış hata varsa devreye girer
```

**Middleware ile farkı:**

Middleware tüm HTTP pipeline'ını kapsar — statik dosya istekleri, authentication, routing dahil her şey. Filter ise sadece controller/action seviyesinde çalışır ve `ActionContext`'e (model, route değerleri, validation sonucu) erişebilir.

Middleware → tüm HTTP trafiği için genel kesitler (rate limit, CORS, authentication)
Filter → controller dünyasına özgü kesitler (audit, validation, hata yönetimi)

---

## 3. Gerçek Hayatta Filter Nerede Kullanılır?

Filter'ın production projelerinde **kesinlikle** kullanıldığı 4 senaryo var. Bunların dışındaki kullanımlar çoğunlukla middleware ile daha doğru çözülür.

### Senaryo 1: Audit Log — Kim Ne Yaptı?

Bankacılık, sağlık, e-ticaret gibi sektörlerde yasal zorunluluk: hangi kullanıcı, hangi kaynağa, ne zaman, ne işlem yaptı — hepsinin kaydı tutulmalı.

Bunu her action'a elle yazmak yerine filter otomatik halleder.

```csharp
// "IAsyncActionFilter" → implement ettiğimiz interface.
// "Async" versiyonunu seçtik çünkü hem before hem after
// tek metodda yazılıyor — await next() öncesi before, sonrası after.
public class AuditFilter : IAsyncActionFilter
{
    private readonly ILogger<AuditFilter> _logger;

    // Constructor injection — DI container bu constructor'ı görür ve
    // ILogger<AuditFilter>'ı otomatik oluşturup inject eder.
    public AuditFilter(ILogger<AuditFilter> logger)
    {
        _logger = logger;
    }

    // "OnActionExecutionAsync" → IAsyncActionFilter'ın tek metodu, implement etmek zorundayız.
    // "ActionExecutingContext context" → action çalışmadan önceki durumu taşır:
    //     hangi controller, hangi action, route değerleri, gelen model, kullanıcı kimliği
    // "ActionExecutionDelegate next" → "bir sonraki adıma geç" fonksiyonu.
    //     await next() yazmadan action hiç çalışmaz — dikkat.
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // ── Action öncesi: kim, ne istiyor? ────────────────────────

        // "context.HttpContext" → ASP.NET Core'un tüm request/response bilgisi buradan.
        // ".User" → giriş yapmış kullanıcı (ClaimsPrincipal nesnesi).
        // ".Identity" → kimlik bilgisi. Giriş yapılmamışsa null olabilir — "?" ile güvenli eriş.
        // ".Name" → kullanıcı adı. Cookie veya JWT token'dan gelir.
        // "?? "Anonim"" → sol taraf null ise sağ taraftaki değeri kullan (null coalescing operator).
        var kullanici = context.HttpContext.User.Identity?.Name ?? "Anonim";

        // "context.RouteData.Values" → URL'den parse edilmiş route segmentleri.
        // /kitaplar/sil/42 isteği için:
        //   ["controller"] → "Kitap"   (KitapController'dan suffix kaldırıldı)
        //   ["action"]     → "Sil"     (metod adı)
        //   ["id"]         → "42"      (URL segmenti)
        // object tipi döner — string değil. Log'a yazarken sorun olmaz.
        var controller = context.RouteData.Values["controller"];
        var action     = context.RouteData.Values["action"];
        var id         = context.RouteData.Values["id"]; // yoksa null — sorun değil

        // Structured logging: "{Kullanici}" gibi placeholder'lar değişkenlere sırayla bağlanır.
        // "Audit" prefix'i → log aracında filtre yapabilmek için.
        _logger.LogInformation(
            "[Audit] {Kullanici} → {Controller}/{Action} id={Id}",
            kullanici, controller, action, id);

        // ── Action çalışır ──────────────────────────────────────────

        // "await next()" → pipeline'daki bir sonraki adımı çalıştır ve bekle.
        // Başka filter sırada varsa onu çalıştırır, yoksa action metodunu çalıştırır.
        // "var executed =" → action bittikten sonraki durumu yakala.
        // Bu satır olmadan action hiç çalışmaz!
        var executed = await next();

        // ── Action sonrası: sonuç ne oldu? ─────────────────────────

        // "executed.Result" → action'ın döndürdüğü IActionResult nesnesi.
        // "?.GetType().Name" → nesne null değilse tip adını al: "RedirectToActionResult", "ViewResult" vs.
        // "?? "null"" → executed.Result null ise "null" string'i kullan.
        var sonucTipi = executed.Result?.GetType().Name ?? "null";

        // "executed.Exception" → action içinde fırlatılan ve henüz kimse tarafından
        // yakalanmamış hata. Yoksa null.
        // "is not null" → null kontrolü (C# 9 pattern matching). "!= null" ile aynı.
        if (executed.Exception is not null)
        {
            _logger.LogWarning(
                "[Audit] {Kullanici} → {Controller}/{Action} HATA: {Mesaj}",
                kullanici, controller, action, executed.Exception.Message);
        }
        else
        {
            _logger.LogInformation(
                "[Audit] {Kullanici} ← {Controller}/{Action} sonuç: {Sonuc}",
                kullanici, controller, action, sonucTipi);
        }
    }
}
```

Gerçek projede log satırları bir veritabanı tablosuna veya Elasticsearch/Seq gibi merkezi log sistemine gider. Ama mantık aynı — filter yazdın, her action otomatik audit altına girdi.

---

### Senaryo 2: Performans İzleme — Yavaş Action'ları Tespit Et

Production'da bir action aniden 3 saniye sürmeye başladıysa bunu fark etmen için bir mekanizma gerekir. Her action'a stopwatch kodu yazmak yerine tek bir filter halleder.

```csharp
// Yine IAsyncActionFilter — await next() etrafına süre ölçümü sarıyoruz.
public class PerformansFilter : IAsyncActionFilter
{
    // "const" → derleme zamanında sabit, değiştirilemez.
    // "int" → tam sayı tipi.
    // Gerçek projede bu değer appsettings.json'dan IOptions<T> ile okunur (Gün 16).
    private const int UyariEsigiMs = 500;

    private readonly ILogger<PerformansFilter> _logger;

    public PerformansFilter(ILogger<PerformansFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // "System.Diagnostics.Stopwatch" → .NET'in yerleşik kronometre sınıfı.
        // "System.Diagnostics" namespace'i — using ile eklemek yerine tam yol yazdık.
        // ".StartNew()" → static factory metod: hem oluşturur hem başlatır.
        // DateTime.UtcNow farkından daha hassas: CPU tick'i sayar (nanosaniye hassasiyeti).
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await next(); // action çalışır — süreyi burada ölçüyoruz

        // ".Stop()" → kronometreyi durdur.
        sw.Stop();

        // ".ElapsedMilliseconds" → başlangıçtan durdurmaya kadar geçen süre, milisaniye cinsinden.
        // "long" tipi döner ama int ile karşılaştırma sorunsuz çalışır.
        var sure = sw.ElapsedMilliseconds;

        var controller = context.RouteData.Values["controller"];
        var action     = context.RouteData.Values["action"];

        if (sure > UyariEsigiMs)
        {
            // "LogWarning" → izleme araçlarında (Application Insights, Seq, Grafana) alert üretir.
            // "+" operatörü string birleştirme — uzun mesajı iki satıra böldük.
            _logger.LogWarning(
                "[Performans] YAVAŞ: {Controller}/{Action} {Sure}ms " +
                "(eşik: {Esik}ms) | Path: {Path}",
                controller, action,
                sure, UyariEsigiMs,
                context.HttpContext.Request.Path); // "/kitaplar/detay/42" gibi
        }
        else
        {
            // "LogDebug" → varsayılan log seviyesi Information olduğu için
            // production'da bu satır hiç yazılmaz. Geliştirme ortamında görünür.
            _logger.LogDebug(
                "[Performans] {Controller}/{Action} {Sure}ms",
                controller, action, sure);
        }
    }
}
```

Bu filter global kaydedilince hiçbir action'a dokunmadan tüm uygulamada performans izleme başlar. Bir action 500ms'yi aşınca log sistemine uyarı düşer, alarm kurulur.

---

### Senaryo 3: Merkezi Hata Yönetimi — Her Exception ProblemDetails Olsun

50 controller'ın her action'ına try/catch yazmak yerine tek bir exception filter tüm hataları yakalayıp standart formatta döndürür.

Bu özellikle API endpoint'leri için kritik: her hata aynı formatta gelmezse frontend takımı veya API kullanan servisler her endpoint için farklı hata parse etmek zorunda kalır.

```csharp
// "IExceptionFilter" → yakalanmamış exception'lar için özel interface.
// IActionFilter'dan farklı: action öncesi/sonrası değil, sadece hata anında tetiklenir.
public class GlobalHataFilter : IExceptionFilter
{
    private readonly ILogger<GlobalHataFilter> _logger;

    // "IWebHostEnvironment" → uygulamanın ortam bilgisi (Development/Production/Staging).
    // DI container bunu otomatik sağlar — kaydetmene gerek yok, built-in servis.
    private readonly IWebHostEnvironment _env;

    public GlobalHataFilter(ILogger<GlobalHataFilter> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env    = env;
    }

    // "OnException" → IExceptionFilter'ın tek metodu.
    // "ExceptionContext context" → hata anındaki durumu taşır:
    //     context.Exception     → fırlatılan hata nesnesi
    //     context.Result        → buraya set edilirse bu yanıt gönderilir
    //     context.ExceptionHandled → true yapılırsa hata "çözüldü" sayılır
    public void OnException(ExceptionContext context)
    {
        // "context.Exception" → yakalanmamış Exception nesnesi.
        // "var hata =" → kısa isim ver, aşağıda tekrar kullanacağız.
        var hata = context.Exception;

        // "hata" (Exception nesnesi) doğrudan LogError'a geçilebilir —
        // .NET loglama altyapısı stack trace'i otomatik ekler.
        _logger.LogError(
            hata,
            "[HataFilter] {Tip}: {Mesaj} | Path: {Path}",
            hata.GetType().Name, // "KeyNotFoundException", "ArgumentException" vs.
            hata.Message,
            context.HttpContext.Request.Path);

        // "switch expression" (C# 8+) — normal switch'in kısa yazımı.
        // Söz dizimi: "değişken switch { tip => sonuç, tip => sonuç, _ => varsayılan }"
        // Her satır: "hata bu tipse → şu tuple'ı döndür"
        // Tuple: "(int, string)" — iki değeri birlikte döndürme.
        // "var (statusKod, baslik) =" → tuple'ı iki ayrı değişkene aç (destructuring).
        var (statusKod, baslik) = hata switch
        {
            // "KeyNotFoundException" tipindeyse:
            KeyNotFoundException        => (StatusCodes.Status404NotFound,
                                            "İstenen kaynak bulunamadı"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden,
                                            "Bu işlem için yetkiniz yok"),
            ArgumentException           => (StatusCodes.Status400BadRequest,
                                            "Geçersiz istek parametresi"),
            // "_" (discard) → diğer tüm tipler için varsayılan — Java'daki default gibi.
            _                           => (StatusCodes.Status500InternalServerError,
                                            "Sunucu tarafında beklenmedik bir hata oluştu")
        };

        // "new ProblemDetails { ... }" → nesne başlatıcı (object initializer).
        // Constructor çağırmak yerine property'leri süslü parantez içinde set ediyoruz.
        // Java'daki builder pattern'e benzer ama daha kısa.
        var problem = new ProblemDetails
        {
            Status   = statusKod,    // HTTP status kodu (int): 404, 500 vs.
            Title    = baslik,       // İnsan okuyabilir hata başlığı
            Instance = context.HttpContext.Request.Path  // Hatanın hangi URL'de olduğu
        };

        // "_env.IsDevelopment()" → ASPNETCORE_ENVIRONMENT değişkeni "Development" ise true.
        // Development'ta detayı göster — Production'da stack trace kullanıcıya görünmemeli.
        if (_env.IsDevelopment())
        {
            // "hata.ToString()" → hata mesajı + tüm stack trace.
            // "problem.Detail" → ProblemDetails'in opsiyonel ek açıklama alanı.
            problem.Detail = hata.ToString();
        }

        // "new ObjectResult(problem)" → herhangi bir nesneyi HTTP yanıtına sarar.
        // "{ StatusCode = statusKod }" → nesne başlatıcı ile StatusCode property'sini set et.
        // NOT: ProblemDetails.Status sadece JSON'a yazılır, HTTP durum kodunu set ETMEZ.
        //      HTTP durum kodunu ObjectResult.StatusCode set eder — ikisi ayrı şey.
        context.Result = new ObjectResult(problem) { StatusCode = statusKod };

        // "context.ExceptionHandled = true" → "bu hatayı hallettim" işareti.
        // false bırakılırsa hata middleware'e iletilir, UseExceptionHandler da devreye girer.
        // İkisi birden çalışmasın diye true yapıyoruz.
        context.ExceptionHandled = true;
    }
}
```

**Neden middleware `UseExceptionHandler` yetmez?**

`UseExceptionHandler` hataları yakalar ama `ActionContext`'e (ModelState, route değerleri, action adı) erişemez. Exception filter bu bilgilere erişerek daha zengin hata yanıtı döndürebilir. Production'da genellikle ikisi birlikte kullanılır:

- `UseExceptionHandler` → routing öncesi veya middleware hatalarını yakalar
- Exception Filter → controller/action hatalarını yakalar, daha detaylı bilgiyle

---

### Senaryo 4: Validation Boilerplate'i Ortadan Kaldır

Projeye 20 controller eklendiğinde her POST action'da şunu yazıyorsun:

```csharp
if (!ModelState.IsValid)
    return View(model);
```

Bu 40+ yerde aynı kod. Bir gün "validation hatasında BadRequest dön" kararı alınırsa 40+ yerde değiştirirsin. Validation filter bu kodu tek yerden yönetir.

```csharp
// "IActionFilter" → Sync versiyon. Async bekleme yapmıyoruz — DB veya HTTP çağrısı yok.
// İki metod implement etmek zorundayız: OnActionExecuting ve OnActionExecuted.
public class ValidationFilter : IActionFilter
{
    // "OnActionExecuting" → action çalışmadan ÖNCE tetiklenir.
    // "ActionExecutingContext context" → action öncesi durum:
    //     context.ModelState      → binding + validation sonucu
    //     context.ActionArguments → action'a gelen parametreler (name:value)
    //     context.Result          → buraya set edilirse action çalışmaz (kısa devre)
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // "context.HttpContext.Request.Method" → "GET", "POST", "PUT", "DELETE" string'i.
        // "HttpMethods.IsGet(method)" → string karşılaştırmasını güvenli yapan static metod.
        // GET ve DELETE'de body olmaz, model binding olmaz, validation anlamsız — atla.
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsDelete(method))
            return; // return → metoddan çık, aşağıdaki kod çalışmaz

        // "context.ModelState.IsValid" → tüm DataAnnotation attribute'larının
        // ve binding'in sonucu. Hepsi geçerliyse true, biri ihlal edildiyse false.
        if (context.ModelState.IsValid)
            return; // Model geçerliyse dokunma, action normal çalışsın

        // ── Buraya geldik: ModelState geçersiz ────────────────────

        // "context.ActionArguments" → Dictionary<string, object?>
        // Action metodunun parametrelerini isim:değer olarak tutar.
        // Örnek: Ekle(KitapFormViewModel model) için:
        //   { "model" → KitapFormViewModel { Baslik="" } }
        // ".Values" → sadece değerleri al (isimleri değil).
        // ".FirstOrDefault(...)" → koşulu sağlayan ilk elemanı döndür, yoksa null.
        // "v is not null" → null değil mi?
        // "!v.GetType().IsPrimitive" → int, bool gibi primitif değil mi? (sınıf mı?)
        // "v is not string" → string de primitif gibi davranır, onu da dışla.
        // Sonuç: action'a gelen ilk "model" sınıfını bul.
        var model = context.ActionArguments.Values
            .FirstOrDefault(v => v is not null && !v.GetType().IsPrimitive && v is not string);

        // "context.Controller" → o anki controller instance'ı, object tipinde gelir.
        // "is Controller controller" → pattern matching + cast birlikte.
        //   "context.Controller, Controller tipindeyse" VE "controller değişkenine ata"
        //   Cast başarısızsa if bloğuna girmez (null kontrolü yapmaya gerek yok).
        if (context.Controller is Controller controller)
        {
            // "controller.View(model)" → View() helper metodu.
            // Model ile birlikte ilgili .cshtml dosyasını render eder.
            // Bu result set edilince action çalışmaz — pipeline kısa devre yapar.
            // ModelState hataları view'da asp-validation-for ile görünür.
            context.Result = controller.View(model);
        }
    }

    // "OnActionExecuted" → action çalıştıktan SONRA tetiklenir.
    // Kısa devre olduysa (context.Result set edildiyse) burası çalışmaz zaten.
    // Yapacağımız bir şey yok — boş implement ediyoruz ama yazmak zorundayız (interface şartı).
    public void OnActionExecuted(ActionExecutedContext context) { }
}
```

Artık KitapController'daki tüm POST action'lardan `if (!ModelState.IsValid)` satırları kalktı. Yarın "validation hatalarında 400 BadRequest dön" kararı alınırsa sadece bu filter değişir.

---

## 4. Filter'ı Nereye Eklersin? — Üç Seviye

### Global — tüm controller ve action'lara:

```csharp
// "AddControllersWithViews(options => { ... })" →
//   Lambda expression ile MVC'ye seçenekler veriyoruz.
//   "options =>" → parametre adı, MvcOptions tipinde bir nesne.
//   "{ ... }" → lambda gövdesi — burada filter'ları ekliyoruz.
builder.Services.AddControllersWithViews(options =>
{
    // "options.Filters" → global filter koleksiyonu.
    // ".Add<T>()" → T tipini DI container ile oluştur ve ekle.
    // Angle bracket içi "<T>" → hangi filter sınıfı? Generic parametre.
    options.Filters.Add<AuditFilter>();       // her action audit altında
    options.Filters.Add<PerformansFilter>();  // her action süre ölçülür
    options.Filters.Add<GlobalHataFilter>();  // her hata standart formata girer
});
```

### Controller seviyesi — sadece o controller'ın action'larına:

```csharp
// "[ ]" köşeli parantez → attribute bildirimi. Java'daki @Annotation gibi.
// Birden fazla attribute üst üste yazılabilir.
// "[Route("kitaplar")]" → routing için (Gün 17'den geliyor)
// "[TypeFilter(typeof(ValidationFilter))]" → filter ekle
//
// "TypeFilter(typeof(ValidationFilter))" →
//   TypeFilter: bir attribute sınıfı, parantez içine ayarlar alır.
//   "typeof(ValidationFilter)" → ValidationFilter'ın System.Type nesnesi.
//   Java'daki "ValidationFilter.class" ile aynı anlam.
//   Framework bunu DI container ile oluşturur — "new ValidationFilter()" deme.
[Route("kitaplar")]
[TypeFilter(typeof(ValidationFilter))]
public class KitapController : Controller { ... }
```

### Action seviyesi — sadece tek bir action'a:

```csharp
public class KitapController : Controller
{
    // Attribute'u action metodun üstüne taşıdık — sadece bu action etkilenir.
    // Controller'daki diğer action'lar bu filter'ı görmez.
    [HttpPost("ekle")]
    [TypeFilter(typeof(ValidationFilter))]
    public IActionResult Ekle(KitapFormViewModel model) { ... }
}
```

---

## 5. [TypeFilter] vs [ServiceFilter]

Filter'a DI üzerinden servis inject etmek istediğinde:

```csharp
// "[TypeFilter(...)]" → attribute, köşeli parantez içinde.
// "typeof(PerformansFilter)" → hangi filter? Type bilgisi.
// "Arguments = new object[] { 300 }" →
//   named argument: "Arguments" property'sini set et.
//   "new object[] { 300 }" → object dizisi (array).
//   PerformansFilter constructor'ında "int esik" parametresi varsa buradan geçilir.
// Program.cs'de önceden kaydetmene gerek yok — TypeFilter kendisi oluşturur.
[TypeFilter(typeof(PerformansFilter), Arguments = new object[] { 300 })]
public IActionResult AgirIslem() { ... }
```

```csharp
// "[ServiceFilter(...)]" → filter'ı DI container'dan alır.
// Kullanmadan önce Program.cs'de kaydetmiş olman şart:
//   builder.Services.AddScoped<AuditFilter>();
//
// Fark: ServiceFilter filter'ın Scoped/Singleton/Transient olmasına izin verir.
// AuditFilter bir DbContext kullanıyorsa Scoped olması gerekir —
// DbContext Singleton içinde yaşayamaz (captive dependency problemi).
[ServiceFilter(typeof(AuditFilter))]
public IActionResult Detay(int id) { ... }
```

**Özet:** Program.cs'de kayıt gerekmiyorsa `[TypeFilter]`, lifetime kontrolü gerekiyorsa `[ServiceFilter]`.

---

## 6. ProblemDetails — Standart Hata Formatı

API yazan herkes hata yanıtını farklı formatta döndürürdü. RFC 9457 bunu standartlaştırdı:

```json
{
  "type":     "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title":    "İstenen kaynak bulunamadı",
  "status":   404,
  "instance": "/kitaplar/detay/999"
}
```

```csharp
// Program.cs — bu satır 4xx ve 5xx yanıtlarını otomatik
// ProblemDetails JSON formatına çevirir.
// GlobalHataFilter zaten ProblemDetails üretiyor —
// bu satır ek olarak NotFound(), BadRequest() gibi direkt
// döndürülen kodları da aynı formata sokar.
builder.Services.AddProblemDetails();
```

```csharp
// Action içinde manuel ProblemDetails döndürmek:
// "Problem(...)" → Controller base class'tan gelen helper metod.
// Named argument söz dizimi: "paramAdı: değer"
return Problem(
    title:      "Kitap bulunamadı",
    statusCode: 404,
    instance:   $"/kitaplar/detay/{id}"  // "$" → string interpolation, {id} değişken değeri
);
```

---

## 7. Dikkat Edilmesi Gerekenler

**Exception filter her hatayı yakalamaz:** Middleware katmanında oluşan hatalar (routing öncesi, static file hatası) exception filter'a düşmez. Bunlar için `UseExceptionHandler` gerekir. Production'da her ikisi birlikte kullanılır.

**Filter lifetime ve DB context:** Audit filter bir veritabanına yazıyorsa, filter'ın Scoped olması gerekir — DbContext Singleton içinde kullanılamaz. `[ServiceFilter]` ile Scoped kaydedilmeli.

**Filter sırası:** Global kayıtta sıra, ekleme sırasıyla belirlenir. Aynı controller'da hem global hem controller seviyesi filter varsa global önce çalışır.

**Middleware mi, filter mi?** Karar kuralı:

| İhtiyaç | Tercih |
|---|---|
| Tüm HTTP trafiği etkilensin (statik dosyalar dahil) | Middleware |
| Sadece controller action'ları etkilensin | Filter |
| ModelState, route değerleri, action adına erişim gerekli | Filter |
| Authentication, rate limiting, CORS | Middleware |
| Audit log, performans izleme, validation, hata yönetimi | Filter |

---

## 8. Kontrol Soruları

1. Audit log neden middleware yerine filter ile yapılır? Middleware ile yapılsaydı ne eksik kalırdı?

2. Performans filter'ı 500ms eşiğini appsettings.json'dan okumak istesen ne değiştirir? (İpucu: `IOptions<T>`)

3. `GlobalHataFilter`'da `context.ExceptionHandled = true` yapmasaydık ne olurdu?

4. `ValidationFilter` neden Async değil Sync interface kullanıyor? Async ne zaman gerekir?

5. Audit filter bir `DbContext`'e yazacaksa neden Singleton olmamalı? Hangi yöntemle kaydedilmeli?

6. `[TypeFilter]` ile `[ServiceFilter]` arasında Scoped bir filter için hangisi daha doğru? Neden?
