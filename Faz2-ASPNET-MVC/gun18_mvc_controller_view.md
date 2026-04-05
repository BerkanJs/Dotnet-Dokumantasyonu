# Gün 18 — MVC Pattern: Controller, Action, View

---

## 1. MVC Nedir? Neden Var?

Bir web uygulamasında üç farklı sorumluluk var:

- **Veriyi getir/işle** — veritabanına git, hesapla, doğrula
- **Kararı ver** — kullanıcıyı nereye gönder, hangi sayfayı göster
- **Görüntüle** — HTML üret, kullanıcıya sun

Bu üç şeyi tek bir dosyaya koyarsan büyüdükçe okunmaz hale gelir. MVC bu üçünü birbirinden ayırır:

```
Model      → Veri ve iş kuralları (Kitap, KitapFormViewModel)
View       → HTML şablonu (.cshtml dosyası)
Controller → Karar mekanizması (aralarındaki koordinatör)
```

Spring MVC bunu zaten biliyorsun — `@Controller`, `@RequestMapping`, Thymeleaf view. ASP.NET Core MVC aynı fikri farklı API ile uygular.

---

## 2. Request'ten Response'a: Tam Akış

Kullanıcı `GET /kitaplar/detay/42` istediğinde arka planda şunlar olur:

```
1. Kestrel isteği alır
2. Middleware pipeline çalışır (logging, auth, vs.)
3. Router: "Bu istek KitapController.Detay(42)'ye gidiyor" kararını verir
4. Framework KitapController'ın yeni bir instance'ını oluşturur
5. DI container constructor parametrelerini enjekte eder
6. Detay(42) metodu çalışır
7. Action IActionResult döner (View, Json, Redirect, vs.)
8. Framework IActionResult'ı execute eder — HTML veya JSON üretir
9. Response kullanıcıya gönderilir
10. Controller instance çöp toplayıcıya bırakılır
```

**Önemli:** Controller her request'te sıfırdan oluşturulur. Spring'deki `@Controller` default olarak singleton'dır — .NET'te tam tersi. Her isteğe kendi controller instance'ı gelir. Bu yüzden controller'da field olarak state tutmak tehlikeli değil ama gereksiz — zaten bir istek yaşar.

---

## 3. Controller Anatomisi

```csharp
// ": Controller" → Java'daki "extends Controller" ile aynı anlam.
// Controller base class'tan türemek zorunlu değil ama View(), Json(),
// Redirect() gibi helper metodlar oradan geliyor — türemezsen bunlara erişemezsin.
public class KitapController : Controller
{
    // "private readonly" — Java'daki "private final" ile aynı.
    // readonly: constructor dışında atama yapılamaz, yanlışlıkla değiştirilmez.
    // "_" prefix: field olduğunu gösterir (C# konvansiyonu, zorunlu değil).
    private readonly IKitapServisi _kitapServisi;

    // ILogger<KitapController> — generic tip.
    // <KitapController> kısmı log mesajlarına hangi sınıftan geldiğini ekler:
    //   [KitabeviMVC.Controllers.KitapController] Kitap eklendi: ...
    private readonly ILogger<KitapController> _logger;

    // Constructor injection — Java Spring'deki @Autowired constructor ile aynı.
    // Framework bu constructor'ı görür, parametreleri DI container'dan bulup verir.
    // "new KitapController(...)" sen çağırmıyorsun — framework çağırıyor.
    public KitapController(IKitapServisi kitapServisi, ILogger<KitapController> logger)
    {
        _kitapServisi = kitapServisi;
        _logger = logger;
    }

    // IActionResult: tüm dönüş tiplerinin (View, Json, Redirect, NotFound...)
    // implement ettiği interface. Framework ne döndüğünü bu interface üzerinden anlar.
    public IActionResult Index()
    {
        return View();
    }
}
```

**Hangi metodlar action sayılır?**

- `public` olmak zorunda
- `static` olmamalı
- Controller sınıfından miras gelen metodlar (`View()`, `Json()` vs.) action değil

---

## 4. Action Return Types — Ne Dönebilirim?

Action metotların dönüş tipi `IActionResult`. Framework bu interface'i alır ve ne tür bir response üretmesi gerektiğini anlar.

```csharp
// ── View döndür ────────────────────────────────────────────────

// Convention: Views/Kitap/Liste.cshtml aranır (controller adı + action adı)
return View();

// View'a model gönder — .cshtml dosyası bu nesneyi @Model ile alır
return View(kitapListesi);

// İlk argüman view adı, ikinci argüman model.
// Views/Kitap/OzelListe.cshtml aranır.
return View("OzelListe", kitapListesi);


// ── Yönlendirme ────────────────────────────────────────────────

// Tarayıcıya HTTP 302 gönderir.
// Tarayıcı otomatik olarak /kitaplar'a GET isteği yapar.
return RedirectToAction("Liste");

// "new { id = 42 }" → anonim nesne (anonymous object).
// Java'da böyle bir söz dizimi yok — C#'ta tek kullanımlık nesne oluşturur.
// RedirectToAction bu nesnenin property'lerini route değeri olarak kullanır:
//   → /kitaplar/detay/42
return RedirectToAction("Detay", new { id = 42 });

// İkinci argüman controller adı (suffix olmadan: "Home", "Kitap")
return RedirectToAction("Index", "Home");

// URL'i elle yazarak yönlendir — route değişirse kırılır, tercih edilmez
return Redirect("/kitaplar");


// ── HTTP durum kodları ──────────────────────────────────────────
return NotFound();            // 404 — gövdesiz
return NotFound("Kitap yok"); // 404 + string gövde

return BadRequest();                 // 400 — gövdesiz
return BadRequest(ModelState);       // 400 + validation hata detayları (JSON)

return Unauthorized();  // 401 — kimlik doğrulama yok
return Forbid();        // 403 — kimlik var ama yetki yok

return Ok();            // 200 — gövdesiz
return Ok(kitap);       // 200 + kitap nesnesi JSON olarak serialize edilir


// ── JSON ────────────────────────────────────────────────────────
// kitapListesi nesnesini JSON'a çevirip döner.
// MVC'de View() yerine bunu kullanırsan sayfasız, sadece veri döner.
return Json(kitapListesi);


// ── Dosya ───────────────────────────────────────────────────────
// 1. argüman: byte[] — dosya içeriği
// 2. argüman: MIME type — tarayıcı ne tür içerik olduğunu bu bilgiyle anlar
// 3. argüman: indirme adı — "Farklı kaydet" penceresinde görünür
return File(dosyaBaytlari, "application/pdf", "rapor.pdf");
```

**`IActionResult` vs `ActionResult<T>`:**

```csharp
// IActionResult — dönüş tipi belirsiz.
// Ne döneceğini derleyici bilmez, tip güvenliği zayıf.
public IActionResult Detay(int id) { ... }

// ActionResult<T> — generic tip.
// <Kitap> kısmı "başarılı durumda Kitap döner" anlamına gelir.
// Hem IActionResult (NotFound, BadRequest...) hem de Kitap döndürebilirsin.
public ActionResult<Kitap> Detay(int id)
{
    var kitap = _kitapServisi.BulById(id);

    // "kitap is null" → C# 9 pattern matching ile null kontrolü.
    // "kitap == null" ile aynı şeyi yapar ama daha okunabilir.
    if (kitap is null) return NotFound();  // IActionResult döner

    return kitap; // Kitap nesnesi döner — framework otomatik Ok(kitap)'a çevirir
}
```

API yazıyorsan `ActionResult<T>` tercih et — belgeleme araçları tip bilgisini kullanır.

---

## 5. Model Binding — İstek Nasıl C# Nesnesine Dönüşür?

Kullanıcı form doldurup gönderdiğinde veya URL'de parametre geçtiğinde framework bunları otomatik olarak action parametrelerine bağlar. Buna **model binding** denir.

Nereden geleceğini şu attribute'larla belirtirsin:

```csharp
// [FromRoute] → köşeli parantez içindeki kelime bir attribute (Java'daki @Annotation gibi).
// Parametrenin önüne yazılır, framework'e "bu değeri URL'den al" der.
// /kitaplar/detay/42 → URL'deki "42" → id parametresine bağlanır.
public IActionResult Detay([FromRoute] int id) { ... }

// [FromQuery] → "bu değeri query string'den al"
// /kitaplar/ara?q=orwell → URL'deki "orwell" → q parametresine bağlanır.
// "string q" → nullable değil ama q parametresi gelmediyse null olur.
//              "string? q" yazsan derleyici uyarısı bastırılır.
public IActionResult Ara([FromQuery] string q) { ... }

// [FromBody] → "bu değeri HTTP body'den oku ve deserialize et"
// Content-Type: application/json olan POST isteklerinde kullanılır.
// Body'deki JSON → KitapFormViewModel nesnesine otomatik dönüştürülür.
public IActionResult Ekle([FromBody] KitapFormViewModel model) { ... }

// [FromForm] → "bu değeri HTML form gönderisinden oku"
// Content-Type: application/x-www-form-urlencoded veya multipart/form-data.
// Tarayıcı formu submit ettiğinde bu format kullanılır.
public IActionResult Guncelle([FromForm] KitapFormViewModel model) { ... }

// [FromHeader(Name = "Authorization")] → attribute'a parametre geçme.
// "Name = ..." → named argument — hangi header'ı okuyacağını belirtir.
// "Authorization" header'ındaki değer → token parametresine bağlanır.
// Örnek istek: Authorization: Bearer abc123 → token = "Bearer abc123"
public IActionResult Korunan([FromHeader(Name = "Authorization")] string token) { ... }
```

**Attribute yazmasan da olur:**

Framework akıllıca tahmin eder:
- Basit tip (int, string) + route template'de aynı isim → `[FromRoute]`
- Basit tip + route template'de yok → `[FromQuery]`
- Karmaşık tip (sınıf) → `[FromForm]` (MVC) veya `[FromBody]` (API controller)

Ama açıkça yazmak her zaman daha iyi — okuyanda soru işareti bırakmaz.

**Binding sırası:**

Bir parametre için framework şu sırayla bakar, ilk eşleşmede durur:

```
1. [FromBody]   → JSON/XML body
2. [FromForm]   → HTML form alanları
3. [FromRoute]  → URL segmentleri
4. [FromQuery]  → Query string
5. [FromHeader] → HTTP header'ları
```

---

## 6. Model Validation — Gelen Veriyi Doğrulama

Kullanıcıdan gelen veriyi doğrulamanın iki yolu var.

### DataAnnotations — basit kurallar için

Doğrulama kurallarını doğrudan model sınıfına attribute olarak yazarsın:

```csharp
public class KitapFormViewModel
{
    // [Required] → boş geçilemez.
    // (ErrorMessage = "...") → attribute'a named argument ile özel mesaj ver.
    // Named argument söz dizimi: parantez içinde "PropertyAdı = değer"
    [Required(ErrorMessage = "Başlık zorunludur")]

    // [StringLength(200, MinimumLength = 2)] → birden fazla named argument.
    // İlk argüman max uzunluk (pozisyonel), MinimumLength named argument.
    [StringLength(200, MinimumLength = 2, ErrorMessage = "2-200 karakter arası olmalı")]
    // "= string.Empty" → null yerine boş string ile başlat (C# 8+ null safety).
    public string Baslik { get; set; } = string.Empty;

    [Required]
    public string Yazar { get; set; } = string.Empty;

    // [Range(min, max)] → sayısal değer aralığı kısıtı
    [Range(0, 10000, ErrorMessage = "Fiyat 0-10.000 arasında olmalı")]
    public decimal Fiyat { get; set; }

    // "string?" → nullable reference type (C# 8+).
    // "?" eki: bu alan null olabilir, boş bırakılabilir demek.
    // [EmailAddress] → @ ve domain içerip içermediğini kontrol eder.
    [EmailAddress]
    public string? YazarEmail { get; set; }

    // [Url] → "http://" veya "https://" ile başlayıp başlamadığını kontrol eder.
    [Url]
    public string? KapakResmiUrl { get; set; }
}
```

### ModelState — validation sonucu

Framework action çalışmadan önce binding yapıp attribute'lara göre doğrular. Sonuç `ModelState`'te durur:

```csharp
[HttpPost("ekle")]
public IActionResult Ekle(KitapFormViewModel model)
{
    // Herhangi bir validation kuralı ihlal edildiyse false
    if (!ModelState.IsValid)
    {
        // View'ı tekrar göster — ModelState hataları Tag Helper'larla
        // otomatik form alanlarının yanında görünür
        return View(model);
    }

    // Buraya geldi mi veri temiz demektir
    _kitapServisi.Ekle(model);
    return RedirectToAction("Liste");
}
```

**`ModelState`'i elle doldurmak:**

Bazen veritabanı kontrolü gibi attribute ile yapılamayan doğrulamalar gerekir:

```csharp
[HttpPost("ekle")]
public IActionResult Ekle(KitapFormViewModel model)
{
    if (!ModelState.IsValid)
        return View(model);

    // ISBN zaten var mı?
    if (_kitapServisi.IsbnVarMi(model.Isbn))
    {
        // "Isbn" — hangi alana ait hata olduğunu belirt
        ModelState.AddModelError("Isbn", "Bu ISBN zaten kayıtlı");
        return View(model);
    }

    _kitapServisi.Ekle(model);
    return RedirectToAction("Liste");
}
```

---

## 7. View ve Razor Syntax

View dosyaları `.cshtml` uzantılıdır — HTML içinde C# kodu yazabilirsin. Razor bu ikisini birleştiren template engine'dir.

**Dosya konumu kurulumu:**

```
Views/
  Kitap/
    Liste.cshtml    ← KitapController.Liste() için
    Detay.cshtml    ← KitapController.Detay() için
    Ekle.cshtml     ← KitapController.Ekle() için
  Shared/
    _Layout.cshtml  ← Her sayfanın içine yerleştiği ana şablon
    _ValidationScripts.cshtml
```

`return View()` dediğinde framework convention'a göre `Views/{ControllerAdı}/{ActionAdı}.cshtml` dosyasını arar.

**Razor temel syntax:**

```html
@* Bu bir Razor yorum satırı. HTML'e dönüştürülmez, kaynakta görünmez. *@

@* "@model" (küçük harf) — direktif, bu view'ın beklediği tip.
   Bir kez, dosyanın en üstünde yazılır.
   "<>" içi generic tip — "KitapListeViewModel'lardan oluşan liste" demek. *@
@model IEnumerable<KitabeviMVC.Models.ViewModels.KitapListeViewModel>

@* "@{ ... }" — Razor kod bloğu. Birden fazla C# satırı yazılabilir.
   HTML üretmez, sadece C# çalıştırır. *@
@{
    ViewData["Title"] = "Kitap Listesi"; // _Layout.cshtml'deki <title>'a gider
}

@* "@Model" (büyük harf) — controller'ın View(model) ile gönderdiği nesne.
   "@model" direktifi ne tip olduğunu söyledi, "@Model" o tipteki veridir.
   Nokta notasyonuyla property ve metodlara erişilir. *@
<p>Toplam @Model.Count() kitap var.</p>

@* "@if" — @ prefix'i Razor'a "bu satır C#" der. Normal if söz dizimi. *@
@if (!Model.Any())
{
    <p>Henüz kitap eklenmemiş.</p>
}

@* "@foreach" — yine @ prefix ile C# döngüsü.
   Küme parantezleri arasına HTML yazılabilir — Razor HTML ile C#'ı karıştırır. *@
@foreach (var kitap in Model)
{
    <tr>
        @* "@kitap.Baslik" — döngü değişkeninin property'si, HTML'e yazılır *@
        <td>@kitap.Baslik</td>
        <td>@kitap.Yazar</td>
        @* Metod çağrısı içeren ifade: parantez bitene kadar Razor C# okur.
           "C" format kodu: para birimi (currency) — ₺89,00 gibi çıkar *@
        <td>@kitap.Fiyat.ToString("C")</td>
    </tr>
}
```

---

## 8. Tag Helpers — HTML ile C# Arasında Köprü

Tag Helper'lar HTML elementlerine `asp-` prefix'li attribute ekleyerek çalışır. Çalışma anında doğru HTML'e dönüşürler.

**Link üretimi:**

```html
@* "asp-controller", "asp-action" → Tag Helper attribute'ları.
   Normal HTML attribute'lardan farkı "asp-" prefix'i.
   Razor bunları çalışma anında gerçek href'e dönüştürür.
   "Kitap" → KitapController (suffix yok), "Detay" → action metod adı. *@
<a asp-controller="Kitap" asp-action="Detay" asp-route-id="@kitap.Id">
    Detaya Git
</a>

@* "asp-route-id" → "asp-route-{paramAdı}" kalıbı.
   "{}" içindeki isim route template'deki parametre adıyla eşleşmeli.
   Detay action'ında "int id" parametresi var → "asp-route-id" yazıyoruz.
   "@kitap.Id" → Razor ifadesi, döngüdeki kitabın Id'sini yazar.
   Sonuç: <a href="/kitaplar/detay/42">Detaya Git</a> *@
```

**Form:**

```html
@* "asp-controller" + "asp-action" burada form'un action URL'ini üretir.
   method="post" → HTML standardı, Razor'a özgü değil.
   Çıktı: <form action="/kitaplar/ekle" method="post"> *@
<form asp-controller="Kitap" asp-action="Ekle" method="post">

    @* "@Html.AntiForgeryToken()" → Html helper metod çağrısı.
       "@Html" controller'dan gelen bir yardımcı nesne.
       Şunu üretir: <input type="hidden" name="__RequestVerificationToken" value="..." />
       Controller'daki [ValidateAntiForgeryToken] bu değeri doğrular.
       Olmadan: başka bir siteden form submit edilebilir (CSRF saldırısı). *@
    @Html.AntiForgeryToken()

    <div>
        @* "asp-for="Baslik"" → modelin "Baslik" property'sine bağla.
           Label için: <label for="Baslik">Baslik</label> üretir.
           "for" attribute'u label'ı input ile eşleştirir (erişilebilirlik). *@
        <label asp-for="Baslik"></label>

        @* "asp-for" burada input için:
           - name="Baslik"  → form submit edilince bu isimle gönderilir
           - id="Baslik"    → label'ın "for" değeriyle eşleşir
           - value="..."    → model'de değer varsa doldurur (düzenleme formu)
           - type otomatik: string → text, int → number, bool → checkbox *@
        <input asp-for="Baslik" class="form-control" />

        @* "asp-validation-for="Baslik"" → ModelState'te "Baslik" anahtarlı hata varsa
           <span> içine yazar. class="text-danger" Bootstrap kırmızı renk. *@
        <span asp-validation-for="Baslik" class="text-danger"></span>
    </div>

    <div>
        <label asp-for="Fiyat"></label>
        <input asp-for="Fiyat" class="form-control" />
        <span asp-validation-for="Fiyat" class="text-danger"></span>
    </div>

    <button type="submit">Kaydet</button>
</form>
```

**`asp-validation-for` nasıl çalışır?**

POST sonrası `return View(model)` dönüldüğünde `ModelState` hatalarını bilir. `asp-validation-for="Baslik"` o alana ait hatayı bulup `<span>` içine yazar. Client-side validation için `jquery.validate` kütüphanesi devreye girer — sayfayı göndermeden önce tarayıcı tarafında da kontrol eder.

---

## 9. ViewData ve TempData — View'a Ek Veri Göndermek

Model dışında view'a küçük bir şey göndermek gerekirse:

```csharp
// Controller'da:
public IActionResult Liste()
{
    // ViewData — dictionary (anahtar:değer).
    // string key ile her türlü nesne saklanabilir.
    // Sadece bu request boyunca yaşar — sonraki request'te boş olur.
    ViewData["ToplamKitap"] = _kitapServisi.ToplamSay();
    ViewData["Title"] = "Tüm Kitaplar"; // _Layout.cshtml <title>'ına gider

    var model = _kitapServisi.HepsiniGetir();
    return View(model);
}
```

```html
@* View'da:
   ViewData["Title"] string döner ama derleme zamanında tip bilinmez (object).
   Razor otomatik ToString() yapar — cast gerekmez. *@
<h1>@ViewData["Title"]</h1>
<p>@ViewData["ToplamKitap"] kitap bulundu</p>
```

**TempData — redirect sonrası mesaj taşımak için:**

Formdan kaydet → redirect → "Kaydedildi" mesajı göster. Bu senaryoda normal `ViewData` çalışmaz çünkü redirect yeni bir request başlatır. `TempData` session benzeri bir mekanizma kullanır ve bir sonraki request'te okunur, sonra silinir.

```csharp
// Controller'da — POST action:
public IActionResult Ekle(KitapFormViewModel model)
{
    _kitapServisi.Ekle(model);

    // TempData — ViewData gibi dictionary ama cookie/session ile taşınır.
    // RedirectToAction yeni bir request başlatır; ViewData o request'te kaybolur.
    // TempData bir sonraki request'e kadar hayatta kalır, okunduktan sonra silinir.
    TempData["BasariMesaji"] = "Kitap başarıyla eklendi!";

    return RedirectToAction("Liste"); // Yeni GET request → Liste action çalışır
}
// Liste action'ında bir şey yapman gerekmiyor — TempData oraya otomatik taşınır
```

```html
@* Liste.cshtml'de:
   "!= null" kontrolü — TempData okunmadıysa null döner, okunduktan sonra silinir.
   Sayfa yenilenirse TempData artık yok — mesaj bir kez görünür. *@
@if (TempData["BasariMesaji"] != null)
{
    <div class="alert alert-success">
        @TempData["BasariMesaji"]  @* object döner, Razor otomatik string'e çevirir *@
    </div>
}
```

---

## 10. Dikkat Edilmesi Gerekenler

**Controller şişmemeli:** Controller sadece koordinatör — veriyi getir, view'a gönder, yönlendir. İş mantığı controller'da olmamalı. Kitap fiyatı hesaplama, stok kontrolü, mail gönderme → bunlar servis katmanında.

```csharp
// Yanlış — iş mantığı controller'da
public IActionResult Ekle(KitapFormViewModel model)
{
    if (model.Fiyat < 0) model.Fiyat = 0;
    if (model.StokAdedi > 1000) throw new Exception("...");
    // 50 satır iş mantığı...
}

// Doğru — controller sadece koordine eder
public IActionResult Ekle(KitapFormViewModel model)
{
    if (!ModelState.IsValid) return View(model);
    _kitapServisi.Ekle(model);
    return RedirectToAction("Liste");
}
```

**PRG Pattern (Post-Redirect-Get):** Form POST'tan sonra asla `return View()` yapma — kullanıcı sayfayı yenilerse formu tekrar gönderir. POST → kaydet → RedirectToAction → GET → View. Bu pattern hem çifte kayıt sorununu çözer hem de tarayıcının "formu tekrar göndermek ister misiniz?" uyarısını engeller.

**Anti-Forgery Token:** Form'a `@Html.AntiForgeryToken()` ekle, POST action'a `[ValidateAntiForgeryToken]` yaz. Olmadan CSRF saldırısına açık olursun — başka bir siteden formunu submit edebilirler.

---

## 11. Kontrol Soruları

1. Controller her request'te yeniden mi oluşturulur, singleton mı yaşar? Spring MVC ile farkı ne?

2. `return View(model)` ile `return Json(model)` arasındaki fark ne? Ne zaman hangisini kullanırsın?

3. `[FromBody]` ile `[FromForm]` farkı nedir? İkisini aynı anda kullanabilir misin?

4. `ModelState.IsValid` false döndüğünde neden `return View(model)` yapıp, `return RedirectToAction("Liste")` yapmıyoruz?

5. PRG pattern nedir? Neden kullanılır? Olmasa ne olur?

6. `ViewData` ile `TempData` arasındaki fark nedir? Redirect sonrası "İşlem başarılı" mesajı göstermek için hangisini kullanırsın?
