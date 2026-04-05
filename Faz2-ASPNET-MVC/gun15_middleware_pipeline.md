# Gün 15 — Middleware Pipeline

---

## 1. Middleware Nedir? Somut Örnek

Bir kafe düşün. Müşteri sipariş verdiğinde şu adımlar oluyor:

1. Kapıda güvenlik kontrol eder — üye misin?
2. Kasa siparişi alır, fişi yazar
3. Barista kahveyi yapar
4. Kasa teslim eder
5. Güvenlik çıkışta teşekkür eder

Her adım bir öncekinin çıktısını alıyor, işleyip bir sonrakine veriyor. Birini atlarsan sistem bozuluyor — kasa olmadan barista ne yapacağını bilemiyor.

ASP.NET Core'da bu zincire **middleware pipeline** deniyor. Tarayıcıdan gelen her HTTP isteği bu zincirden geçiyor:

```
İstek →  [Loglama] → [Auth] → [Routing] → [Controller]
Yanıt ← [Loglama] ← [Auth] ← [Routing] ← [Controller]
```

İstek içe doğru ilerliyor, controller yanıt üretince aynı zincir dışa doğru açılıyor. Yani her middleware isteği hem gidişte hem dönüşte görebiliyor.

---

## 2. Program.cs — İki Aşama

ASP.NET Core uygulaması başlarken iki aşama var:

```csharp
// AŞAMA 1 — Hazırlık (builder)
var builder = WebApplication.CreateBuilder(args);

// "Hangi servisleri kullanacağım?" buraya yazılır
builder.Services.AddControllers();
builder.Services.AddScoped<IKitapRepository, KitapRepository>();

// AŞAMA 2 — Kurulum (app)
var app = builder.Build();  // ← DI container burada kapanır

// "İstekler hangi sırayla hangi middleware'den geçecek?" buraya yazılır
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();  // ← sunucu başlar, uygulama çalışmaya başlar
```

`builder` → servisler için. `app` → pipeline için. İkisini karıştırma.

`builder.Build()` çağrıldıktan sonra artık servis ekleyemezsin — DI container kapandı.

---

## 3. Use, Run, Map

Her birinin tek bir amacı var:

---

### `Use` — geçir, ama önce bir şeyler yap

```csharp
app.Use(async (context, next) =>
{
    // İstek geldi — burada bir şeyler yap
    Console.WriteLine("İstek geliyor: " + context.Request.Path);

    await next();  // ← bir sonraki middleware'e gönder

    // Yanıt döndü — burada da bir şeyler yapabilirsin
    Console.WriteLine("Yanıt gönderildi: " + context.Response.StatusCode);
});
```

`next()` çağrılırsa zincir devam eder.  
`next()` çağrılmazsa istek orada durur, controller'a ulaşmaz.

---

### `Run` — bitir, yanıt yaz

```csharp
app.Run(async context =>
{
    await context.Response.WriteAsync("Merhaba Kitabevi");
    // next() yok — zincir burada bitti
});
```

`Run` her zaman zincirin **son halkasıdır**. Sonrasına ne yazarsan yaz çalışmaz.

---

### `Map` — farklı yola farklı davran

```csharp
// /saglik adresine gelen istekler bu kola gider
app.Map("/saglik", saglikApp =>
{
    saglikApp.Run(async context =>
        await context.Response.WriteAsync("Uygulama çalışıyor"));
});

// Diğer tüm adresler normal pipeline'dan devam eder
app.MapControllers();
```

Health check endpoint'leri için sık kullanılır — authentication gerekmeden "uygulama ayakta mı?" sorusunu yanıtlar.

---

## 4. Sıra Her Şeydir

Middleware'lerin sırası yanlış olursa uygulama çalışır ama **yanlış** çalışır. Hata mesajı almayabilirsin bile.

**Doğru sıra:**

```csharp
app.UseExceptionHandler("/hata");  // 1. En dışta — her hatayı yakalar
app.UseHttpsRedirection();         // 2. HTTP gelirse HTTPS'e yönlendir
app.UseStaticFiles();              // 3. CSS/JS/resim — giriş yapmadan erişilir
app.UseAuthentication();           // 4. "Bu kullanıcı kim?" — JWT token'ı oku
app.UseAuthorization();            // 5. "Bu kullanıcının yetkisi var mı?"
app.MapControllers();              // 6. Controller'a gönder
```

**Neden bu sıra?**

`UseAuthentication` önce gelir çünkü kullanıcının kim olduğunu bilmeden yetki kontrolü yapılamaz. Sırayı tersine çevirirsen authorization her zaman başarısız olur — çünkü "kim olduğunu" henüz bilmiyorsun.

`UseStaticFiles` auth'tan önce gelir çünkü sitenin CSS dosyasını indirmek için login olmak gerekmez.

`UseExceptionHandler` en dışta olur çünkü içteki herhangi bir middleware veya controller hata fırlatırsa dışarıdaki yakalasın.

**Yanlış sıra — sessiz hata:**

```csharp
app.UseAuthorization();   // ← kim olduğunu bilmeden yetki kontrolü?
app.UseAuthentication();  // ← artık çok geç, yukarısı zaten çalıştı
```

Kod derlenir, uygulama başlar, ama `[Authorize]` attribute'u hiç çalışmaz.

---

## 5. HttpContext — İsteğin Tüm Bilgisi

Her HTTP isteği için ASP.NET Core bir `HttpContext` nesnesi oluşturur. Bu nesne o isteğe ait her şeyi taşır:

```csharp
app.Use(async (context, next) =>
{
    // Gelen istek hakkında bilgiler
    string metod   = context.Request.Method;          // "GET", "POST" ...
    string adres   = context.Request.Path;            // "/api/kitaplar"
    string? sayfa  = context.Request.Query["sayfa"];  // ?sayfa=2
    string? token  = context.Request.Headers["Authorization"];

    // Middleware'ler arası veri taşıma
    // Bir middleware buraya yazar, sonraki okur
    context.Items["istekBaslangic"] = DateTime.UtcNow;

    await next();

    // Yanıt hakkında bilgiler (next() sonrası)
    int statusKod = context.Response.StatusCode;  // 200, 404, 500 ...
});
```

`HttpContext` o isteğe özeldir — farklı kullanıcıların istekleri farklı `HttpContext` nesneleri. İstek bitince nesne yok edilir.

---

## 6. Short-Circuit — İsteği Durdurmak

Bazen isteği controller'a kadar götürmek istemezsin. Örneğin API key yoksa neden controller'a kadar gidelim?

```csharp
app.Use(async (context, next) =>
{
    bool apiKeyVar = context.Request.Headers.ContainsKey("X-Api-Key");

    if (!apiKeyVar)
    {
        // Yanıtı biz yazıyoruz, pipeline kesildi
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("API anahtarı eksik");
        return;  // next() çağrılmadı — controller'a ulaşılmadı
    }

    await next();  // API key varsa devam et
});
```

`next()` çağrılmadan `return` yapılırsa istek orada durur. Controller çalışmaz, gereksiz iş yapılmaz.

---

## 7. Class Tabanlı Middleware

Küçük bir kontrol için `app.Use(...)` yeterli. Ama gerçek projede middleware'in:
- Birden fazla servise ihtiyacı olabilir (logger, config...)
- Test edilmesi gerekebilir
- Başka dosyada durması daha temiz olabilir

Bunun için class yaz:

```csharp
public class IstekLoglamaMiddleware
{
    private readonly RequestDelegate _next;
    // RequestDelegate = "bir sonraki middleware" demek

    public IstekLoglamaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // ASP.NET Core bu metodu her istekte çağırır
    public async Task InvokeAsync(HttpContext context)
    {
        var baslangic = DateTime.UtcNow;
        var adres = context.Request.Path;

        Console.WriteLine($"→ İstek geldi: {adres}");

        await _next(context);  // bir sonraki middleware'e gönder

        var sure = (DateTime.UtcNow - baslangic).TotalMilliseconds;
        Console.WriteLine($"← Yanıt döndü: {context.Response.StatusCode} ({sure}ms)");
    }
}
```

`RequestDelegate` → "bir sonraki middleware" demek. Bunu çağırırsan zincir devam eder.

Program.cs'e eklemek:

```csharp
app.UseMiddleware<IstekLoglamaMiddleware>();
```

Daha temiz yapmak için extension method eklenebilir:

```csharp
public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseIstekLoglama(this IApplicationBuilder app)
        => app.UseMiddleware<IstekLoglamaMiddleware>();
}

// Program.cs'de artık böyle kullanılır:
app.UseIstekLoglama();
```

ASP.NET Core'un kendi `app.UseAuthentication()`, `app.UseRouting()` gibi metodları da böyle yazılmış — hepsi aslında bir extension method.

---

## 8. Middleware mi, Action Filter mı?

Önce action filter'ın ne olduğunu anlayalım.

Action filter, bir controller metodunun **hemen öncesinde veya hemen sonrasında** otomatik çalışan bir kod bloğu. Şöyle düşün: her kitap ekleme isteğinden önce "kullanıcı admin mi?" kontrol etmek istiyorsun. Bunu her action metoduna yazmak yerine bir filter yazıp üstüne `[AdminFilter]` koyarsın — otomatik çalışır.

```csharp
// Bu action çağrılmadan önce ve sonra AdminFilter devreye girer
[AdminFilter]
public IActionResult KitapEkle(KitapRequest request) { ... }
```

Farkı şöyle açıklayabiliriz:

**Middleware** kapının önündeki güvenlik gibi — binaya giren **herkes** ondan geçer. CSS dosyası isteyen de, API çağrısı yapan da, health check yapan da.

**Action Filter** masanın başındaki asistan gibi — sadece **o masaya** oturmak isteyenlerle ilgilenir. Diğer masalar onu ilgilendirmez.

```
İstek → [Middleware] → [Middleware] → MVC → [Action Filter] → Controller Action
         ↑ herkesi görür                      ↑ sadece bu action'ı görür
```

**Somut örnek:**

`GET /api/kitaplar` isteği geldi diyelim.

- Loglama middleware'i çalışır — her isteği loglar, bu da dahil
- Auth middleware'i çalışır — token var mı bakar
- `[AdminFilter]` ise **sadece** `[AdminFilter]` yazılı action'larda çalışır. Başka bir controller'da işi yok.

**Neyi nereye yazarsın:**

| Ne yapmak istiyorsun | Nereye |
|---|---|
| Her isteği logla | Middleware |
| Token'ı doğrula | Middleware |
| CORS header ekle | Middleware |
| Sadece bu action'da admin kontrolü yap | Action Filter |
| Sadece bu action'ın sonucunu cache'le | Action Filter |
| Sadece bu action'da audit log tut | Action Filter |

Kural basit: **tüm istekleri etkileyecekse** → middleware. **Sadece belirli controller/action'ları etkileyecekse** → action filter.

Action filter'ları Gün 19'da detaylı göreceğiz. Şimdilik "middleware'in daha spesifik, MVC'ye özgü hali" olarak düşün.

---

## 9. Kontrol Soruları

1. Bir middleware `next()` çağırmazsa ne olur? Controller çalışır mı?
hayır bir sonraki kısma gitmez 
2. `Use` ile `Run` arasındaki tek fark nedir?
use bir şeyi calıstırır ama rundan sonra use calısmaz 
3. `UseAuthentication` neden `UseAuthorization`'dan önce yazılır?
kisinin auth olup olmadıgını gormek daha mantıklı
4. `HttpContext.Items` ne işe yarar? Hangi durumda kullanırsın?
http isteklerini kontrol etmek için yapılır
5. Loglama işini middleware ile mi yaparsın, action filter ile mi? Neden?
middleware daha iyi tüm istekleri görüyor middleware 