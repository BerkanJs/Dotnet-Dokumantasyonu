# Gün 90 — Minimal API Performance

---

## Minimal API Nedir, Neden Performans Avantajı Var?

ASP.NET Core'da iki endpoint tanımlama yolu var:
- **Controller-based:** `[ApiController]`, `ControllerBase`, attribute routing — klasik MVC yapısı
- **Minimal API:** `app.MapGet("/kitaplar", handler)` — controller sınıfı yok, doğrudan lambda/metot

Minimal API neden daha hızlı?
- Controller overhead yok (instantiation, action filter pipeline, model binding reflection)
- Daha az middleware katmanı geçer
- Source generator desteği ile compile-time optimizasyon
- Düşük bellek ayak izi (allocation azalır)

**Ne kadar fark?** Basit endpoint'lerde %10-20 daha düşük latency ve allocation. Karmaşık iş mantığında fark azalır çünkü darboğaz zaten DB/network.

**Ne zaman Minimal API?**
- Yeni proje başlıyorsan → varsayılan olarak Minimal API ile başla
- Yüksek throughput endpoint'ler (health check, config, basit CRUD)
- Microservice'ler — az endpoint, hızlı boot

**Ne zaman Controller?**
- Büyük ekip, çok endpoint, karmaşık filter zinciri
- Mevcut controller-based proje (geçiş zorunlu değil)

---

## Temel Performans Teknikleri

### 1. Asenkron Her Yerde — Ama Gereksiz Yerde Değil

```csharp
// ✓ I/O var → async kullan
app.MapGet("/kitaplar", async (IKitapRepo repo) =>
{
    return await repo.ListAsync();
    // ne yapar → thread I/O sırasında serbest kalır, başka isteklere hizmet eder
    // bunu sync yapsaydık → thread bloklanır, thread pool tükenir, throughput düşer
});

// ✓ I/O yok → async KULLANMA
app.MapGet("/sabit-config", () =>
{
    return Results.Ok(new { version = "1.0", env = "prod" });
    // ne yapar → senkron döner, async overhead yok (state machine oluşmaz)
    // bunu async yapsaydık → gereksiz Task allocation + state machine maliyeti
});
```

### 2. TypedResults ile Compile-Time Doğrulama

```csharp
// ✗ Results.Ok → runtime'da IResult boxing
app.MapGet("/kitaplar/{id}", async (int id, IKitapRepo repo) =>
{
    var kitap = await repo.GetAsync(id);
    return kitap is null ? Results.NotFound() : Results.Ok(kitap);
});

// ✓ TypedResults → compile-time, daha az allocation
app.MapGet("/kitaplar/{id}", async Task<Results<Ok<Kitap>, NotFound>> (int id, IKitapRepo repo) =>
{
    var kitap = await repo.GetAsync(id);
    return kitap is null ? TypedResults.NotFound() : TypedResults.Ok(kitap);
    // ne yapar → dönüş tipi derleme zamanında belli, boxing azalır
    // ek avantaj → OpenAPI/Swagger otomatik doğru response type üretir
});
```

### 3. AsParameters — Binding Allocation'ı Azalt

```csharp
// Her parametre ayrı ayrı bind edilir — çok parametrede dağınık
app.MapGet("/kitaplar", async (int sayfa, int boyut, string? arama, string? kategori, IKitapRepo repo) =>
{ /* ... */ });

// ✓ AsParameters ile tek struct'a bind et
app.MapGet("/kitaplar", async ([AsParameters] KitapSorguDto sorgu, IKitapRepo repo) =>
{
    return await repo.ListAsync(sorgu);
});

public record struct KitapSorguDto(int Sayfa = 1, int Boyut = 20, string? Arama = null, string? Kategori = null);
// ne yapar → parametreler tek struct'ta toplanır
// neden struct → heap allocation yok (record class olsa her istekte new object)
// bunu yazmasaydık → 5+ parametre olunca okunabilirlik düşer
```

### 4. Response Streaming — Büyük Listeler

```csharp
// ✗ Tüm listeyi belleğe yükleyip serialize et
app.MapGet("/kitaplar/hepsi", async (IKitapRepo repo) =>
{
    var hepsi = await repo.GetAllAsync();   // 100K kayıt → RAM'e yüklenir
    return Results.Ok(hepsi);                // serialize edilir → ek bellek
});

// ✓ IAsyncEnumerable ile stream et
app.MapGet("/kitaplar/hepsi", (IKitapRepo repo) =>
{
    return repo.StreamAllAsync();   // IAsyncEnumerable<Kitap> döner
    // ne yapar → veriler chunk chunk serialize edilir, tamamı belleğe yüklenmez
    // bunu yazmasaydık → 100K kayıt = ~200 MB RAM spike
    // ne zaman kullan → büyük veri setleri, export endpoint'leri
});

// Repository'de:
public async IAsyncEnumerable<Kitap> StreamAllAsync()
{
    await foreach (var kitap in _context.Kitaplar.AsAsyncEnumerable())
    {
        yield return kitap;
        // ne yapar → her satır DB'den okundukça serialize edilip gönderilir
    }
}
```

### 5. Output Cache + Minimal API (Gün 88 tekrarı değil — entegrasyon notu)

```csharp
// Minimal API'da cache output doğal entegre:
app.MapGet("/kategoriler", async (IKategoriRepo repo) =>
{
    return await repo.ListAsync();
})
.CacheOutput(p => p.Expire(TimeSpan.FromMinutes(30)).Tag("kategoriler"));
// ne yapar → 30 dk boyunca handler çalışmaz, cache'ten döner
// neden kategoriler için uygun → nadiren değişir, sık istenir
// controller'da aynı şeyi yapmak → attribute + policy tanımı gerekir (daha verbose)
```

---

## Source Generator ile AOT Desteği

.NET 8+ ile Minimal API JSON serialization'da source generator kullanabilir — reflection yerine compile-time kod üretir.

```csharp
// Program.cs
builder.Services.ConfigureHttpJsonOptions(opt =>
{
    opt.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
    // ne yapar → JSON serialize/deserialize reflection yerine generated kod kullanır
    // bunu yazmasaydık → her istekte reflection maliyeti (küçük ama ölçekte birikir)
    // AOT publish yapacaksan → ZORUNLU (reflection AOT'de çalışmaz)
});

[JsonSerializable(typeof(List<Kitap>))]
[JsonSerializable(typeof(Kitap))]
[JsonSerializable(typeof(PaginatedResult<Kitap>))]
internal partial class AppJsonContext : JsonSerializerContext { }
// ne yapar → bu tipler için derleme zamanında serializer kodu üretilir
// bunu yazmasaydık → runtime'da reflection ile serialize eder (daha yavaş)
// her yeni DTO eklediğinde buraya da eklemen lazım
```

**Performans etkisi:**
- İlk istek: reflection'da ~5ms warm-up, source generator'da 0
- Bellek: her serialize'da ~%30 daha az allocation
- AOT publish: uygulama 10-50ms'de ayağa kalkar (normal: 200-500ms)

---

## Endpoint Filter ve Short-Circuit

Controller'daki action filter'ların Minimal API karşılığı. Ama daha hafif — reflection yok.

```csharp
// Validation filter — her endpoint'e tekrar tekrar yazmak yerine
app.MapPost("/kitaplar", async (KitapEkleDto dto, IKitapRepo repo) =>
{
    var kitap = await repo.CreateAsync(dto);
    return TypedResults.Created($"/kitaplar/{kitap.Id}", kitap);
})
.AddEndpointFilter(async (context, next) =>
{
    var dto = context.GetArgument<KitapEkleDto>(0);
    if (string.IsNullOrWhiteSpace(dto.Ad))
        return TypedResults.ValidationProblem(
            new Dictionary<string, string[]> { ["ad"] = ["Kitap adı boş olamaz"] });
    // ne yapar → handler çalışmadan önce validation yapar
    // bunu yazmasaydık → validation mantığı handler içinde karışır
    // controller'daki fark → [ApiController] otomatik ModelState validation yapar,
    // minimal API'da kendin yazarsın veya FluentValidation filter eklersin

    return await next(context);   // validation geçtiyse handler'a devam
});

// Reusable filter — IEndpointFilter interface'i ile:
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var arg = ctx.GetArgument<T>(0);
        // FluentValidation veya manual kontrol...
        return await next(ctx);
        // ne yapar → next çağrılırsa pipeline devam eder (sonraki filter veya handler)
        // next çağrılMAZSA → short-circuit: handler'a hiç ulaşılmaz
    }
}
```

### Short-Circuit — Early Return ile Pipeline'ı Kesme

Filter'da `next(context)` çağırmadan değer dönersen → handler hiç çalışmaz. Bu "short-circuit" denir.

```csharp
app.MapPost("/siparis", async (SiparisDto dto, ISiparisService svc) =>
{
    return await svc.OlusturAsync(dto);
})
.AddEndpointFilter(async (context, next) =>
{
    // 1. filter — auth kontrolü
    var user = context.HttpContext.User;
    if (!user.Identity?.IsAuthenticated ?? true)
        return TypedResults.Unauthorized();   // SHORT-CIRCUIT — handler çalışmaz
        // ne yapar → pipeline burada biter, sipariş kodu hiç execute olmaz
        // neden önemli → gereksiz DB çağrısı, validation, iş mantığı atlanır = performans

    return await next(context);   // devam et
})
.AddEndpointFilter(async (context, next) =>
{
    // 2. filter — rate check (filter sırası ekleme sırasıdır)
    var userId = context.HttpContext.User.Identity!.Name!;
    if (await IsRateLimited(userId))
        return TypedResults.StatusCode(429);   // SHORT-CIRCUIT
        // ne yapar → rate limit aşıldıysa handler'a bile ulaşmadan keser

    return await next(context);
});
// Pipeline sırası: Filter1 → Filter2 → Handler
// Filter1 short-circuit yaparsa → Filter2 ve Handler çalışmaz
// Filter2 short-circuit yaparsa → sadece Handler çalışmaz
```

---

## Performans Karşılaştırma Tablosu

| Teknik | Etki | Karmaşıklık | Ne zaman değer? |
|--------|------|-------------|-----------------|
| async I/O | Throughput 2-5x artar | Düşük | Her zaman |
| TypedResults | Az allocation, OpenAPI desteği | Düşük | Her zaman |
| AsParameters (struct) | Heap allocation azalır | Düşük | 3+ parametre varsa |
| IAsyncEnumerable | RAM spike önler | Orta | Büyük veri setleri |
| Source Generator JSON | %30 az allocation, AOT | Orta | Yüksek throughput, AOT |
| Endpoint Filter | Controller filter overhead'i yok | Düşük | Cross-cutting concern |

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC controller-based:
```csharp
// Faz2 — controller overhead + sync risk
public class KitaplarController : Controller
{
    public async Task<IActionResult> Index()
    {
        var kitaplar = await _context.Kitaplar.ToListAsync();  // tamamı RAM'e
        return View(kitaplar);                                  // MVC view render
    }
}

// Faz4 — minimal, hızlı, düşük allocation
app.MapGet("/kitaplar", async (IKitapRepo repo) => await repo.ListAsync())
   .CacheOutput(p => p.Expire(TimeSpan.FromMinutes(5)));
```

Fark: controller instantiation yok, filter pipeline yok, view render yok, cache var. Aynı iş %20 daha az CPU ve bellek ile yapılır.

---

## 500 vs 50K Kullanıcı

| | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---|---|---|
| Minimal API vs Controller | Stil tercihi — performans farkı hissedilmez | Sıcak endpoint'lerde allocation farkı belirgin |
| IAsyncEnumerable | Gereksiz — listeler küçük | Export/rapor endpoint'lerinde zorunlu |
| Source Generator JSON | Opsiyonel güzel alışkanlık | Yüksek RPS endpoint'lerde ölçülebilir fark |
| AOT publish | Gereksiz | Cold start önemliyse (serverless/container) değerli |

---

## Kontrol Soruları

1. Minimal API neden controller-based'den daha az allocation yapar?
2. I/O olmayan endpoint'te async kullanmak neden zararlı olabilir?
3. IAsyncEnumerable ne zaman kullanılır, ToListAsync'den farkı nedir?
4. Source generator JSON serialization'ın AOT ile ilişkisi nedir?
5. TypedResults ile Results arasındaki fark nedir?
6. AsParameters'da neden struct tercih edilir?
