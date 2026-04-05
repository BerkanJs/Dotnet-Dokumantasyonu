# Gün 17 — Routing: Convention vs Attribute

---

## 1. Routing Nedir?

Tarayıcıdan `GET /kitaplar/detay/42` isteği geldiğinde ASP.NET Core'un bunu `KitapController.Detay(42)` metoduna götürmesi gerekiyor. Bu eşleme işlemine **routing** denir.

Routing olmadan framework hangi koda gidileceğini bilemez.

```
Tarayıcı  →  GET /kitaplar/detay/42
ASP.NET   →  KitapController → Detay(id: 42)
```

---

## 2. Endpoint Routing — Modern Mimari

ASP.NET Core 3+ ile routing artık middleware pipeline'ından bağımsız. İki adımda çalışır:

```
UseRouting()       → URL hangi endpoint'e gidiyor? Belirle.
UseAuthorization() → Hangi endpoint'e kim girebilir? Kontrol et.
MapControllerRoute() → Endpoint'leri kaydet.
```

Neden bu ayrım önemli? Eski modelde authorization routing'den önce çalışıyordu — yani henüz URL kime ait bilinmeden "girebilir mi?" sorusu soruluyordu. Şimdi önce endpoint belirleniyor, sonra yetkiye bakılıyor.

---

## 3. Convention-Based Routing

"URL şu kalıba uymak zorunda" kuralıdır. `Program.cs`'te tek bir yerde tanımlanır, tüm controller'lar bu kurala göre çalışır.

```csharp
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

Bu kalıp şunu söylüyor:
- `{controller}` → hangi controller? (yoksa `Home`)
- `{action}` → hangi metot? (yoksa `Index`)
- `{id?}` → opsiyonel parametre

Kitabevi örnekleri:

| URL | Controller | Action | id |
|---|---|---|---|
| `/` | Home | Index | — |
| `/kitaplar` | Kitap | Index | — |
| `/kitaplar/detay/42` | Kitap | Detay | 42 |
| `/kitaplar/sil/5` | Kitap | Sil | 5 |

Convention routing MVC görünüm (View döndüren) uygulamalarda tercih edilir — tüm URL'leri tek yerden görürsün, controller içi kirlilik olmaz.

---

## 4. Attribute Routing

Routing kuralını doğrudan controller veya action metoduna yazarsın. Her endpoint kendi URL'ini bilir.

```csharp
[Route("kitaplar")]
public class KitapController : Controller
{
    // GET /kitaplar
    [HttpGet("")]
    public IActionResult Liste() { ... }

    // GET /kitaplar/detay/42
    [HttpGet("detay/{id}")]
    public IActionResult Detay(int id) { ... }

    // POST /kitaplar/ekle
    [HttpPost("ekle")]
    public IActionResult Ekle(KitapViewModel model) { ... }
}
```

Convention routing'den farkı: URL ile metot aynı dosyada — controller'ı açınca hangi URL'ler var hemen görürsün.

**Ne zaman hangisi?**

| Durum | Tercih |
|---|---|
| MVC (View döndürüyor, `{controller}/{action}` yeterli) | Convention |
| API (URL'ler özelleştirilmiş, REST standartları) | Attribute |
| MVC + API karışık projede API kısmı | Attribute |

Bir projede ikisi bir arada kullanılabilir. `[Route]` attribute'u olan controller convention routing'i yok sayar.

---

## 5. Route Constraints — Kısıtlar

URL parametresinin tipini veya formatını zorlamak için kullanılır.

```csharp
// id mutlaka int olmak zorunda
[HttpGet("detay/{id:int}")]
public IActionResult Detay(int id) { ... }

// slug sadece küçük harf ve tire içerebilir
[HttpGet("kitap/{slug:regex(^[a-z-]+$)}")]
public IActionResult KitapSayfasi(string slug) { ... }

// min/max değer kısıtı
[HttpGet("sayfa/{sayfa:int:min(1)}")]
public IActionResult Listele(int sayfa) { ... }
```

Kısıtsız durumda ne olur?

```csharp
// id:int kısıtı olmadan:
// /kitaplar/detay/abc  → framework action'ı bulmaya çalışır
//                        int'e çeviremeyince 400 Bad Request döner
// id:int kısıtıyla:
// /kitaplar/detay/abc  → bu route eşleşmez, bir sonraki route denenir
//                        hiçbiri eşleşmezse 404 döner
```

Yaygın kısıtlar:

| Kısıt | Anlam |
|---|---|
| `{id:int}` | int olmak zorunda |
| `{id:guid}` | GUID formatı |
| `{id:min(1)}` | 1 veya üzeri |
| `{id:maxlength(10)}` | en fazla 10 karakter |
| `{slug:alpha}` | sadece harf |
| `{tarih:datetime}` | tarih formatı |

---

## 6. Route Sırası ve Belirsizlik

Birden fazla route aynı URL'e uymaya çalışabilir. Framework hangisini seçer?

Attribute routing'de daha spesifik olan önce eşleşir:

```csharp
// /kitaplar/populer → hangisi çalışır?

[HttpGet("{id:int}")]      // "populer" int değil → geçmez
public IActionResult Detay(int id) { ... }

[HttpGet("populer")]       // tam eşleşme → bu çalışır
public IActionResult Populer() { ... }
```

Convention routing'de sıra önemlidir — ilk eşleşen kazanır:

```csharp
// Önce spesifik, sonra genel
app.MapControllerRoute(
    name: "kitap-detay",
    pattern: "kitaplar/detay/{id:int}",
    defaults: new { controller = "Kitap", action = "Detay" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

---

## 7. Minimal API Routing

.NET 6 ile gelen yaklaşım: controller sınıfı yok, doğrudan `Program.cs`'te lambda ile endpoint tanımlanır.

```csharp
// Convention veya attribute routing yok — endpoint lambda olarak yazılır
app.MapGet("/kitaplar", () => new[] { "Suç ve Ceza", "1984" });

app.MapGet("/kitaplar/{id:int}", (int id) => $"Kitap ID: {id}");

app.MapPost("/kitaplar", (KitapDto dto) =>
{
    // Kaydet...
    return Results.Created($"/kitaplar/{dto.Id}", dto);
});
```

Ne zaman kullanılır?
- Küçük mikroservisler, basit API'ler
- Controller şişkinliği istemiyorsan
- Çok az endpoint varsa

Kitabevi gibi orta-büyük uygulamalarda Minimal API yerine Controller + Attribute routing daha okunabilir.

---

## 8. LinkGenerator — URL Üretimi

Kodun içinde URL'i elle yazmak tehlikeli — route değişince broken link olur. `LinkGenerator` route isminden URL üretir:

```csharp
public class KitapController : Controller
{
    private readonly LinkGenerator _linkGenerator;

    public KitapController(LinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
    }

    public IActionResult Ekle(KitapViewModel model)
    {
        // Kaydet...

        // URL'i elle yazmak yerine route adıyla üret
        var url = _linkGenerator.GetPathByAction(
            HttpContext,
            action: "Detay",
            controller: "Kitap",
            values: new { id = model.Id });

        // url → "/kitaplar/detay/42"
        return Redirect(url!);
    }
}
```

View tarafında benzer iş Tag Helper ile yapılır:

```html
<!-- href="/kitaplar/detay/42" → otomatik üretilir -->
<a asp-controller="Kitap" asp-action="Detay" asp-route-id="42">Detaya Git</a>
```

---

## 9. Dikkat Edilmesi Gerekenler

**Convention + Attribute karışımı:** Bir controller'a `[Route]` yazarsan o controller artık convention routing'den çıkar. Tutarsız davranış istenmiyorsa API controller'larına her zaman attribute routing kullan.

**Route constraint vs model validation farkı:** Constraint eşleşme aşamasında çalışır (404 üretir), validation action içinde çalışır (400 üretir). İkisi farklı katman.

**URL büyük/küçük harf:** ASP.NET Core varsayılan olarak URL'lerde büyük/küçük harf ayrımı yapmaz. `/Kitaplar` ile `/kitaplar` aynı endpoint'e gider.

**Trailing slash:** `/kitaplar/` ile `/kitaplar` varsayılan olarak farklı URL'dir. Gerekiyorsa middleware ile normalize edilebilir.

---

## 10. Kontrol Soruları

1. Convention routing ile attribute routing arasındaki fark nedir? Hangisini ne zaman tercih edersin?

2. `{id:int}` kısıtı olduğunda `/kitaplar/detay/abc` isteği gelirse ne olur? Kısıt olmasaydı ne olurdu?

3. `UseRouting()` ve `MapControllerRoute()` neden ayrı middleware çağrısı? Aynı şey değil mi?

4. Aynı URL'e uyan iki attribute route varsa hangisi çalışır?

5. Tag Helper'da `asp-controller` ve `asp-action` kullanmak URL'i elle yazmaktan neden daha güvenli?
