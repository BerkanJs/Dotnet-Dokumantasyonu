# Gün 23 — Minimal API vs Controller-Based API

---

## 1. Minimal API Nedir?

.NET 6 ile gelen Minimal API, endpoint'leri bir controller sınıfına gerek kalmadan doğrudan `Program.cs` veya küçük dosyalarda lambda ile tanımlamayı sağlar.

Klasik yaklaşımda bir endpoint için şunlar gerekiyordu:
- Bir controller sınıfı
- Constructor injection
- Action metodu
- Route attribute

Minimal API'de aynı iş tek satırda yapılır:

```csharp
app.MapGet("/kitaplar/{id}", (int id, IKitapServisi servis) =>
    servis.BulById(id) is { } kitap ? Results.Ok(kitap) : Results.NotFound());
```

Spring Boot'daki `@RestController` → Controller yaklaşımı.
Minimal API'nin Spring karşılığı yok — en yakın Spring WebFlux'un functional endpoint'leri (`RouterFunction`).

---

## 2. Controller vs Minimal API — Trade-off'lar

| | Controller | Minimal API |
|---|---|---|
| Kod miktarı | Fazla (sınıf, attribute) | Az (lambda) |
| Filter desteği | Tam (Action, Result, Exception) | Sınırlı (EndpointFilter) |
| Model Binding | Otomatik, zengin | Temel — complex senaryolarda manuel |
| Swagger / OpenAPI | `[ProducesResponseType]` ile kolay | `TypedResults` ile desteklenir |
| Test edilebilirlik | Kolay (DI + mock) | Kolay ama lambda izolasyonu zor |
| Büyük ekip uyumu | Yüksek — standart yapı | Düşük — her geliştirici farklı yazar |
| Performans | İyi | Biraz daha iyi (daha az overhead) |

**Kural:**

```
Minimal API  → microservice, basit CRUD, az bağımlılık, küçük ekip
Controller   → karmaşık domain, büyük ekip, filter-heavy logic
```

Kitabevi gibi bir proje büyüdükçe Controller tercih edilir. Basit bir fiyat sorgulama microservice'i için Minimal API daha uygun.

---

## 3. Minimal API Temel Syntax

```csharp
// Program.cs — app.MapXxx ile endpoint tanımlanır
var app = builder.Build();

// GET — basit okuma
// "int id" → route'dan, "IKitapServisi servis" → DI container'dan gelir
app.MapGet("/api/kitaplar/{id:int}", (int id, IKitapServisi servis) =>
{
    var kitap = servis.BulById(id);
    return kitap is null ? Results.NotFound() : Results.Ok(kitap);
});

// POST — yeni kayıt
// "[FromBody]" attribute'una gerek yok — complex tip body'den otomatik okunur
app.MapPost("/api/kitaplar", (KitapOlusturRequest request, IKitapServisi servis) =>
{
    var id = servis.Ekle(/* ... */);
    return Results.Created($"/api/kitaplar/{id}", new { id });
});

// DELETE
app.MapDelete("/api/kitaplar/{id:int}", (int id, IKitapServisi servis) =>
{
    servis.Sil(id);
    return Results.NoContent();
});
```

Lambda'lar async da olabilir:

```csharp
app.MapGet("/api/kitaplar", async (IKitapServisi servis) =>
{
    var liste = await servis.HepsiniGetirAsync();
    return Results.Ok(liste);
});
```

---

## 4. IEndpointRouteBuilder Extension Pattern

`Program.cs`'i şişirmemek için endpoint'ler extension metoda taşınır. Bu, Minimal API'yi organize etmenin en yaygın yolu:

```csharp
// Endpoints/KitapEndpoints.cs
namespace KitabeviMVC.Endpoints;

public static class KitapEndpoints
{
    // "this IEndpointRouteBuilder" → extension metod.
    // "app.MapKitapEndpoints()" şeklinde çağrılır.
    public static IEndpointRouteBuilder MapKitapEndpoints(this IEndpointRouteBuilder app)
    {
        // "RouteGroupBuilder" → ortak prefix + ortak ayarlar bir kez yazılır.
        // Tüm route'lara "/api/v1/kitaplar" prefix'i eklenir.
        var group = app.MapGroup("/api/v1/kitaplar")
            .WithTags("Kitaplar"); // Swagger'da grup altında görünür

        group.MapGet("/", Liste);
        group.MapGet("/{id:int}", Detay);
        group.MapPost("/", Olustur);
        group.MapDelete("/{id:int}", Sil);

        return app;
    }

    // Lambda yerine private static metod — test edilebilirliği artırır,
    // uzun iş mantığı lambda içinde okunaksız olur.
    private static IResult Liste(IKitapServisi servis)
    {
        var kitaplar = servis.HepsiniGetir();
        return Results.Ok(kitaplar);
    }

    private static IResult Detay(int id, IKitapServisi servis)
    {
        var kitap = servis.BulById(id);
        return kitap is null ? Results.NotFound() : Results.Ok(kitap);
    }

    private static IResult Olustur(KitapOlusturRequest request, IKitapServisi servis)
    {
        // Minimal API'de [ApiController] yok — validation manuel yapılır
        // veya bir filter eklenir (bkz. Bölüm 6)
        var id = servis.Ekle(new KitapFormViewModel
        {
            Baslik    = request.Baslik,
            Yazar     = request.Yazar,
            Fiyat     = request.Fiyat,
            Kategori  = request.Kategori,
            StokAdedi = request.StokAdedi
        });

        return Results.Created($"/api/v1/kitaplar/{id}", new { id });
    }

    private static IResult Sil(int id, IKitapServisi servis)
    {
        var kitap = servis.BulById(id);
        if (kitap is null) return Results.NotFound();

        servis.Sil(id);
        return Results.NoContent();
    }
}
```

```csharp
// Program.cs — tek satır
app.MapKitapEndpoints();
```

---

## 5. TypedResults — Compile-Time OpenAPI Desteği

`Results.Ok(...)` çalışır ama dönüş tipi `IResult` — Swagger hangi HTTP kodlarının döneceğini bilemez.

`TypedResults` bunu çözer: dönüş tipi `Results<T1, T2, T3>` şeklinde yazılınca Swagger otomatik üretir:

```csharp
// "Results<Ok<KitapResponse>, NotFound>" → bu metod ya 200 ya 404 döner.
// Swagger bunu görerek dokümanı otomatik oluşturur.
// "static" → delegate yerine metod referansı — test edilebilir.
private static Results<Ok<KitapResponse>, NotFound> Detay(
    int id,
    IKitapServisi servis)
{
    var kitap = servis.BulById(id);

    if (kitap is null)
        return TypedResults.NotFound(); // NotFound tipi

    var response = new KitapResponse(
        kitap.Id, kitap.Baslik, kitap.Yazar,
        kitap.Fiyat, kitap.Kategori, kitap.StokAdedi);

    return TypedResults.Ok(response); // Ok<KitapResponse> tipi
}
```

```csharp
// POST — 3 olası dönüş tipi
private static Results<Created<KitapResponse>, Conflict<object>, BadRequest> Olustur(
    KitapOlusturRequest request,
    IKitapServisi servis)
{
    if (servis.BaslikVarMi(request.Baslik))
        return TypedResults.Conflict((object)new { hata = "Başlık zaten mevcut." });

    var id = servis.Ekle(/* ... */);
    var response = new KitapResponse(id, request.Baslik, request.Yazar, request.Fiyat, request.Kategori, request.StokAdedi);

    return TypedResults.Created($"/api/v1/kitaplar/{id}", response);
}
```

---

## 6. EndpointFilter vs Action Filter

Controller'da `IActionFilter` kullanıyorduk. Minimal API'de bunun karşılığı `IEndpointFilter`:

```csharp
// Controller Action Filter → Minimal API'de çalışmaz.
// Minimal API için IEndpointFilter kullanılır.
public class ValidationEndpointFilter : IEndpointFilter
{
    // "EndpointFilterInvocationContext context" → endpoint'e gelen istek
    // "EndpointFilterDelegate next" → "bir sonraki adıma geç"
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Endpoint'e gelen argümanları tara — IValidatableObject olanları doğrula
        foreach (var arg in context.Arguments)
        {
            if (arg is null) continue;

            // "DataAnnotations" ile manuel validation
            var validationContext = new ValidationContext(arg);
            var validationResults = new List<ValidationResult>();

            if (!Validator.TryValidateObject(arg, validationContext, validationResults, true))
            {
                // 400 Bad Request — validation hataları ile
                var hatalar = validationResults.ToDictionary(
                    v => v.MemberNames.FirstOrDefault() ?? "genel",
                    v => v.ErrorMessage ?? "Geçersiz değer");

                return Results.ValidationProblem(hatalar);
            }
        }

        return await next(context); // validation geçti, devam et
    }
}
```

Kullanımı — tek endpoint veya gruba eklenebilir:

```csharp
// Tek endpoint'e:
group.MapPost("/", Olustur)
     .AddEndpointFilter<ValidationEndpointFilter>();

// Tüm gruba:
group.MapGroup("/api/v1/kitaplar")
     .AddEndpointFilter<ValidationEndpointFilter>() // her endpoint için çalışır
     .WithTags("Kitaplar");
```

**Action Filter vs EndpointFilter farkı:**

| | Action Filter | EndpointFilter |
|---|---|---|
| Nerede çalışır | Controller pipeline | Minimal API pipeline |
| Erişim | ActionContext (ModelState, route) | EndpointFilterInvocationContext (argümanlar) |
| Kayıt yeri | `[TypeFilter]`, global, controller | `.AddEndpointFilter<T>()` |
| DI desteği | Tam | Tam |

---

## 7. Ne Zaman Minimal API, Ne Zaman Controller?

**Minimal API tercih et:**

- Microservice — tek sorumluluk, az endpoint
- Basit proxy veya gateway endpoint'leri
- Prototip veya hızlı geliştirme
- Performans kritik, overhead minimuma indirilmeli

**Controller tercih et:**

- 10+ endpoint, karmaşık domain mantığı
- Büyük ekip — standart yapı okunabilirliği artırır
- Filter-heavy: audit, validation, performance izleme zaten var
- MVC ile birlikte çalışıyorsa (Kitabevi gibi)

**Aynı projede ikisi bir arada olabilir** — ASP.NET Core buna izin verir. Controller'lar domain CRUD için, Minimal API basit utility endpoint'leri için kullanılabilir.

---

## 8. Dikkat Edilmesi Gerekenler

**Validation farkı:** `[ApiController]` olan controller'larda ModelState otomatik 400 döndürür. Minimal API'de bu yoktur — ya `ValidationEndpointFilter` eklersin ya da her endpoint'te manuel kontrol yaparsın.

**`Results` vs `TypedResults`:** `Results.Ok(...)` çalışır ama Swagger dönüş tipini bilemez. Production API'lerinde `TypedResults` kullan — hem dokümantasyon hem compile-time güvenliği.

**Program.cs şişmesi:** Tüm endpoint'leri `Program.cs`'e yazmak hızlı kötü görünür. `IEndpointRouteBuilder` extension pattern ile her kaynak kendi dosyasında yaşar.

**Route group'ları**: `MapGroup` ortak prefix ve middleware'i bir kez yazmanı sağlar. Her endpoint'e tekrar tekrar `.RequireAuthorization()` yazmak yerine gruba bir kez yaz.

```csharp
// Tüm gruptaki endpoint'ler authentication gerektirir
var korunanGrup = app.MapGroup("/api/v1/admin")
    .RequireAuthorization("AdminOnly");
```

---

## 9. Kontrol Soruları

1. Minimal API'de `[ApiController]`'daki otomatik 400 validation yoktur. Bunu nasıl sağlarsın?

2. `Results.Ok(kitap)` ile `TypedResults.Ok(kitap)` arasındaki fark nedir? Swagger açısından ne değişir?

3. `IEndpointRouteBuilder` extension pattern neden kullanılır? Alternatifi ne, dezavantajı nedir?

4. Aynı projede hem Controller hem Minimal API kullanılabilir mi? Ne zaman bu tercih edilir?

5. `EndpointFilter` ile `ActionFilter` arasındaki temel fark nedir? Birinin diğerinin yerine geçebilir mi?

6. Büyüyen bir projede neden Minimal API yerine Controller tercih edilir? Trade-off'ı somutlaştır.
