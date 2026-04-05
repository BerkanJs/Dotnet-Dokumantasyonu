# KitabeviMVC — Proje Yapısı

Bu dosya projedeki her klasörün ve dosyanın ne işe yaradığını açıklar.  
Yeni bir dosya eklendiğinde buraya da not düşülecek.

---

## Klasör Haritası

```
KitabeviMVC/
│
├── Program.cs                      → Uygulamanın başlangıç noktası
├── appsettings.json                → Konfigürasyon (DB bağlantısı, JWT, log seviyesi)
├── appsettings.Development.json    → Sadece dev ortamında geçerli overridelar
├── KitabeviMVC.csproj              → Proje dosyası (paketler, hedef framework)
│
├── Controllers/                    → HTTP isteklerini karşılar
│   └── HomeController.cs
│
├── Models/                         → Veri yapıları
│   ├── Entities/                   → Veritabanı tabloları
│   │   └── Kitap.cs
│   └── ViewModels/                 → View'a veya API'ye gönderilen şekli
│       └── KitapViewModel.cs
│
├── Data/                           → Veritabanı katmanı
│   └── KitabeviDbContext.cs
│
├── Middleware/                     → Custom middleware'ler
│   └── IstekLoglamaMiddleware.cs
│
├── Views/                          → Razor HTML şablonları (MVC için)
│   ├── Home/
│   ├── Shared/
│   └── _ViewImports.cshtml
│
└── wwwroot/                        → Statik dosyalar (CSS, JS, resim)
    ├── css/
    ├── js/
    └── lib/
```

---

## Her Dosya Ne Yapar?

---

### `Program.cs`

Uygulamanın başladığı yer. İki bölümden oluşur:

```
1. builder → servisleri kaydet (DI container)
2. app     → middleware pipeline'ı kur, uygulamayı başlat
```

Faz ilerledikçe buraya yeni servis kayıtları ve middleware'ler eklenir.  
Gün 15'te `UseIstekLoglama()` eklendi.

---

### `Controllers/HomeController.cs`

HTTP isteklerini karşılayan sınıf. Spring'deki `@Controller` karşılığı.

```csharp
public class HomeController : Controller
{
    public IActionResult Index()      // GET /Home/Index → Views/Home/Index.cshtml
    public IActionResult Privacy()    // GET /Home/Privacy
    public IActionResult Error()      // GET /Home/Error → hata sayfası
}
```

Naming convention: `HomeController` → URL'de `Home`. ASP.NET Core bunu otomatik eşleştirir.

Faz ilerledikçe `KitapController.cs` eklenecek:
- `GET  /Kitap`        → tüm kitapları listele
- `GET  /Kitap/{id}`   → tek kitap detayı
- `POST /Kitap/Ekle`   → yeni kitap ekle
- `POST /Kitap/Sil`    → kitap sil

---

### `Models/Entities/Kitap.cs`

Veritabanındaki `Kitaplar` tablosunu temsil eden sınıf. EF Core bunu okuyarak tablo oluşturur.

```csharp
public class Kitap
{
    public int      Id           { get; set; }  // PRIMARY KEY
    public string   Baslik       { get; set; }  // VARCHAR(200) NOT NULL
    public string   Yazar        { get; set; }  // VARCHAR(100) NOT NULL
    public decimal  Fiyat        { get; set; }  // decimal(18,2)
    public string   Kategori     { get; set; }
    public int      StokAdedi    { get; set; }
    public DateTime EklemeTarihi { get; set; }
}
```

Entity doğrudan View'a verilmez — önce ViewModel'e dönüştürülür.

---

### `Models/ViewModels/KitapViewModel.cs`

View'a veya API response'a gönderilecek şekli. Entity'nin "dışarıya açılan yüzü".

**Neden entity'yi doğrudan kullanmıyoruz?**  
Entity'de şifreler, iç alanlar, navigation property'ler olabilir. Dışarıya hepsini göndermek güvensiz. ViewModel sadece gerekli alanları taşır.

```csharp
// Liste için — sadece gösterilecek alanlar
public record KitapListeViewModel(int Id, string Baslik, string Yazar, decimal Fiyat, ...);

// Form için — [Required], [Range] gibi validation attribute'ları var
public class KitapFormViewModel
{
    [Required(ErrorMessage = "Başlık zorunludur")]
    public string Baslik { get; set; }
    ...
}
```

Spring'deki DTO karşılığı: `KitapResponse`, `KitapRequest`.

---

### `Data/KitabeviDbContext.cs`

EF Core'un veritabanı bağlantı noktası. Spring'deki `JpaRepository` yapısına benzer ama daha merkezi.

```csharp
public class KitabeviDbContext : DbContext
{
    public DbSet<Kitap> Kitaplar => Set<Kitap>();
    // DbSet = veritabanındaki Kitaplar tablosu
    // kitaplar.Where(...).ToList() → SELECT * FROM Kitaplar WHERE ...

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Kolon tipleri, kısıtlamalar, seed data buraya
    }
}
```

`Program.cs`'de Scoped olarak kayıtlıdır — her HTTP request'inde yeni bir instance.

---

### `Middleware/IstekLoglamaMiddleware.cs`

Her HTTP isteğinde çalışan loglama middleware'i. Gün 15'te eklendi.

```
→ GET /Kitap (istek geldi)
← 200 /Kitap (15ms) (yanıt döndü)
```

`RequestDelegate _next` → bir sonraki middleware'i temsil eder.  
`await _next(context)` çağrılmadan önce ve sonra kod çalıştırılabilir.

---

### `Views/`

Razor şablonları — HTML + C# karışımı. MVC'de controller bir ViewModel döndürür, Razor onu HTML'e çevirir.

```
Views/
  Home/
    Index.cshtml     → HomeController.Index() action'ının view'ı
    Privacy.cshtml
  Shared/
    _Layout.cshtml   → Tüm sayfalarda ortak header/footer
    _ValidationScripts.cshtml
```

Naming convention: `HomeController.Index()` → `Views/Home/Index.cshtml`. ASP.NET Core otomatik bulur.

---

### `wwwroot/`

Statik dosyalar — tarayıcıya doğrudan gönderilir, C# kodu çalışmaz.

```
wwwroot/
  css/site.css     → özel stiller
  js/site.js       → özel JavaScript
  lib/             → bootstrap, jquery gibi kütüphaneler
```

`app.UseStaticFiles()` middleware'i bu klasörü serve eder. Auth gerekmez — herkes erişebilir.

---

## Faz Boyunca Eklenecekler

| Gün | Eklenecek Dosya | Ne İçin |
|-----|----------------|---------|
| 17  | `Controllers/KitapController.cs` | Routing |
| 18  | `Views/Kitap/*.cshtml` | MVC action + view |
| 18  | `Services/KitapServisi.cs` | Business logic katmanı |
| 19  | `Filters/LogActionFilter.cs` | Action filter |
| 20  | `Controllers/AuthController.cs` | JWT auth |
| 20  | `Services/TokenServisi.cs` | JWT üretimi |
