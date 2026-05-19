# Gün 122 — API Gateway

## Teorik

### Problem: Dışarıdan Kim Hangi Servise Bağlanacak?

Faz5'te 3 servisimiz var, hepsi farklı portta çalışıyor:

```
CatalogService    → :5001
OrderService      → :5002
NotificationService → :5003
```

Mobil uygulama kitap listesi isteyecek. Sipariş verecek. Bunun için:
- `http://catalog-service:5001/api/books` → kitap listesi
- `http://order-service:5002/api/orders` → sipariş oluştur

Sorunlar:
1. Mobil uygulama 3 farklı adresi bilmek zorunda
2. Her servis kendi auth kontrolünü yapmak zorunda
3. `catalog-service:5001` adresi dışarıya açılırsa servis doğrudan erişilebilir — güvenlik riski
4. Rate limiting her serviste ayrı ayrı yapılacak

---

### API Gateway Pattern — Tek Giriş Noktası

**Analoji: Şirket resepsiyonu**

Şirkete geldiğinde doğrudan mühendis odasına giremezsin. Resepsiyona gidersin. "Ahmet Bey ile görüşeceğim" dersin. Resepsiyonist:
- Kim olduğunu kontrol eder (auth)
- Ahmet Bey'in nerede oturduğunu bilir (routing)
- Ziyaretçi defterine yazar (logging)
- "Bugün 100'den fazla ziyaretçi kabul edemeyiz" diyebilir (rate limiting)

Sonra seni doğru odaya yönlendirir.

```
                    [API Gateway — Resepsiyonist]
                           :8080
                          /      \
           /api/books    /        \   /api/orders
          ▼                            ▼
  [CatalogService]              [OrderService]
      :5001                         :5002
```

Dışarıdan sadece `:8080` görünür. Servisler iç ağda, dışarıya kapalı.

---

### Reverse Proxy Nedir?

Gateway aslında bir **reverse proxy**. Bunu anlamak için önce normal proxy'yi anlamak gerekiyor.

**Normal Proxy (Forward Proxy) — Senin adına internete çıkar**

Şirkette çalışıyorsun, IT departmanı internet trafiğini bir proxy üzerinden geçiriyor. Sen `google.com`'a gitmek istiyorsun ama direkt gidemiyorsun. İstek önce proxy'ye gidiyor, proxy senin adına Google'a bağlanıyor, cevabı sana getiriyor.

```
Sen ──► [Proxy] ──► google.com
              ◄── cevap ──┘
Google, senin IP'ni değil proxy'nin IP'sini görür.
Proxy, senin adına hareket etti.
```

**Reverse Proxy — Servisinin adına sana cevap verir**

Yönü tersine çevir. Bu sefer sen Google'sın, kullanıcılar sana bağlanmak istiyor. Ama kullanıcılar doğrudan senin gerçek sunucularına değil, önündeki reverse proxy'ye bağlanıyor. Reverse proxy isteği alıyor, arkadaki doğru sunucuya iletiyor, cevabı kullanıcıya döndürüyor.

```
Kullanıcı ──► [Reverse Proxy] ──► CatalogService (gerçek sunucu)
         ◄────────── cevap ──────────┘
Kullanıcı, CatalogService'in varlığından bile haberdar değil.
Reverse proxy, servisin adına hareket etti.
```

**Temel fark:**

```
Forward proxy  → kullanıcı tarafında,  kullanıcıyı gizler
Reverse proxy  → sunucu tarafında,     sunucuyu gizler
```

**Gerçek hayat örneği:**

Büyük bir web sitesi düşün (örneğin Trendyol). Milyonlarca istek geliyor. Arka planda 50 sunucu var. Ama sen hep `trendyol.com`'a bağlanıyorsun — 50 sunucudan hangisine gittiğini bilmiyorsun, bilmen gerekmiyor. Önündeki reverse proxy (Nginx/YARP) isteği alıyor, uygun sunucuya yönlendiriyor.

```
Kullanıcı A ──►                    ──► Sunucu 1
Kullanıcı B ──► [Reverse Proxy]   ──► Sunucu 2
Kullanıcı C ──►                    ──► Sunucu 3
              trendyol.com:443
              (tek adres görünür)
```

Kitabevi'mizde YARP bu reverse proxy görevini üstleniyor. Kullanıcı `gateway:8080/api/books` istiyor, YARP bunu alıp `catalog-service:5001/api/books`'a iletiyor. Kullanıcı `catalog-service:5001`'in varlığını bilmiyor.

---

### Reverse Proxy'nin Görevleri

**1. Routing — Doğru servise yönlendirme**
```
/api/books/*   → CatalogService:5001
/api/orders/*  → OrderService:5002
/api/notify/*  → NotificationService:5003
```

**2. Auth — Tek noktada kimlik doğrulama**

Her servis JWT doğrulamak zorunda kalmaz. Gateway token'ı doğrular, servisler "bu kullanıcı kim?" bilgisini header'dan alır.

```
Kullanıcı → [Bearer token ile istek] → Gateway
Gateway → token geçerli mi? → Evet
Gateway → isteği CatalogService'e iletir + X-User-Id: 42 header'ı ekler
CatalogService → X-User-Id header'ını okur, kim olduğunu bilir
```

**3. Rate Limiting — İstek sınırlama**

Gateway seviyesinde "bir IP'den saniyede max 100 istek" kuralı koyarsın. Her servise ayrı yazmak zorunda kalmazsın.

**4. SSL Termination — HTTPS sonlandırma**

Dışarıdan HTTPS gelir. Gateway şifreli isteği çözer, iç servislere HTTP olarak iletir. İç ağda servisler HTTP ile haberleşir — sertifika yönetimi tek yerde.

```
Kullanıcı ──HTTPS──► [Gateway] ──HTTP──► [CatalogService]
                     SSL burada           İç ağda HTTP yeterli
                     sonlanır
```

**5. Logging / Observability**

Her istek gateway'den geçtiği için merkezi log, trace ID oluşturma tek noktada yapılabilir.

---

### YARP — Yet Another Reverse Proxy

Microsoft'un .NET için yazdığı reverse proxy kütüphanesi. NuGet paketi olarak gelir, ASP.NET Core uygulaması olarak çalışır.

**Neden YARP?**
- .NET kodunda yazıldığı için C# ile özelleştirilebilir
- `appsettings.json` veya kod ile konfigüre edilir
- .NET middleware pipeline'ına entegre — Faz2'de yazdığımız middleware burada da çalışır
- Hot reload — config değişince yeniden başlatmaya gerek yok

**Basit YARP Konfigürasyonu:**

```json
// appsettings.json
{
  "ReverseProxy": {
    "Routes": {
      "catalog-route": {
        "ClusterId": "catalog-cluster",
        "Match": {
          "Path": "/api/catalog/{**catch-all}"
        }
      },
      "order-route": {
        "ClusterId": "order-cluster",
        "Match": {
          "Path": "/api/orders/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "catalog-cluster": {
        "Destinations": {
          "catalog-service": {
            "Address": "http://catalog-service:5001/"
          }
        }
      },
      "order-cluster": {
        "Destinations": {
          "order-service": {
            "Address": "http://order-service:5002/"
          }
        }
      }
    }
  }
}
```

```csharp
// Program.cs — ApiGateway projesi
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// JWT doğrulaması gateway seviyesinde
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* config */ });

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();   // YARP devreye giriyor

app.Run();
```

**İstek akışı:**

```
Kullanıcı: GET /api/catalog/books
    ↓
ApiGateway (YARP)
    → "catalog-route" eşleşti
    → catalog-cluster'a yönlendir
    → http://catalog-service:5001/api/catalog/books
    ↓
CatalogService cevaplar
    ↓
ApiGateway cevabı kullanıcıya iletir
```

---

### Kong, Nginx, Ocelot Karşılaştırması

| | YARP | Ocelot | Nginx | Kong |
|---|---|---|---|---|
| Dil | .NET (C#) | .NET (C#) | C | Lua/Go |
| Konfigürasyon | JSON + C# kod | JSON | nginx.conf | Admin API / YAML |
| .NET entegrasyonu | ✅ Native | ✅ Native | ❌ Ayrı process | ❌ Ayrı process |
| Özelleştirme | ✅ C# middleware | ✅ C# middleware | ⚠️ Lua modülü | ✅ Plugin sistemi |
| Production yaygınlığı | ✅ Microsoft projesi | ⚠️ Bakım yavaşladı | ✅ Çok yaygın | ✅ Çok yaygın |
| Ne zaman? | .NET stack, kod ile kontrol | Eski .NET projeler | Sade proxy, .NET dışı | Büyük ölçek, plugin ekosistemi |

Kitabevi'mizde **YARP** kullanıyoruz. .NET stack'te en doğal entegrasyon.

---

### BFF — Backend for Frontend Pattern

API Gateway'in özelleşmiş versiyonu. "Her frontend türü için ayrı bir gateway" demek.

**Problemi somut görelim:**

CatalogService'te bir kitabın tüm verisi şöyle:

```json
{
  "id": 42,
  "title": "Clean Code",
  "author": "Robert C. Martin",
  "authorBio": "Robert Cecil Martin... (500 kelime biyografi)",
  "isbn": "978-0-13-235088-4",
  "price": 149.90,
  "discountedPrice": 127.42,
  "stock": 34,
  "category": "Software Engineering",
  "tags": ["refactoring", "best-practices", "oop"],
  "coverImageUrl": "https://...",
  "coverImageHighResUrl": "https://...",
  "description": "Even bad code can function... (2000 kelime)",
  "publishedAt": "2008-08-11",
  "pageCount": 431,
  "language": "English",
  "publisher": "Prentice Hall",
  "ratings": { "average": 4.7, "count": 12043 },
  "reviews": [ /* 50 son yorum */ ]
}
```

**Tek gateway ile sorun:**

Mobil uygulama kitap listesi ekranında sadece şunu gösteriyor:

```
[Kapak resmi]  Clean Code
               Robert C. Martin
               149.90 TL  ★4.7
```

Ama API yanıtı 10KB'lık dev bir JSON dönüyor. Mobil uygulama bunun %90'ını çöpe atıyor. 3G bağlantıda bu israf, pil tüketimi demek.

Web admin paneli ise kitabı düzenlemek için her şeyi istiyor — biyografi, açıklama, tüm alanlar.

**Tek gateway herkese aynı JSON'ı döner:**

```
Mobil App  ─► [Gateway] ─► CatalogService ─► 10KB JSON ─► Mobil 9KB'ı çöpe atar
Web App    ─► [Gateway] ─► CatalogService ─► 10KB JSON ─► Web hepsini kullanır
```

**BFF çözümü — her client için ayrı gateway, ayrı DTO:**

```
Mobil App  ─► [Mobile BFF]  ─► CatalogService ─► sadece gerekli alanları al ─► 1KB
Web App    ─► [Web BFF]     ─► CatalogService ─► tüm alanlar                ─► 10KB
Admin      ─► [Admin BFF]   ─► CatalogService ─► tüm alanlar + yönetim API  ─► 12KB
```

Mobile BFF, CatalogService'ten tam veriyi alır ama sadece mobil'in ihtiyacı olan alanları döner:

```csharp
// Mobile BFF — BookController
[HttpGet("{id}")]
public async Task<MobileBookDto> GetBook(int id)
{
    // CatalogService'ten tam veriyi al (internal HTTP çağrısı)
    var fullBook = await _catalogClient.GetBookAsync(id);

    // Sadece mobil'in ihtiyacı olan 5 alanı döndür
    return new MobileBookDto(
        Id:            fullBook.Id,
        Title:         fullBook.Title,
        Author:        fullBook.Author,
        Price:         fullBook.DiscountedPrice,
        CoverImageUrl: fullBook.CoverImageUrl   // sadece küçük resim
        // Rating: fullBook.Ratings.Average    // belki bu da
        // Gerisi yok — 500 kelime biyografi yok, reviews yok, isbn yok
    );
}

// Web BFF — BookController
[HttpGet("{id}")]
public async Task<WebBookDto> GetBook(int id)
{
    var fullBook = await _catalogClient.GetBookAsync(id);
    var reviews  = await _reviewClient.GetReviewsAsync(id);  // ayrı servis

    // Web için hem kitap hem yorumlar birleştirilmiş
    return new WebBookDto(fullBook, reviews);
}
```

**BFF'in ikinci avantajı: Veri birleştirme (aggregation)**

Web sayfası hem kitap bilgisini hem son yorumları hem stok durumunu bir sayfada gösteriyor. Bunlar 3 farklı servisten geliyor.

BFF olmadan web uygulaması 3 ayrı API çağrısı yapmak zorunda:
```
Web App → CatalogService  /books/42
Web App → ReviewService   /reviews?bookId=42
Web App → StockService    /stock/42
(3 ayrı istek, 3 ayrı bekleme)
```

BFF ile web uygulaması tek istekte hepsini alır:
```
Web App → [Web BFF] /books/42/detail
              ↓ BFF paralel olarak 3 servisi çağırır
              ↓ hepsini birleştirir
              ↓ tek response döner
```

```csharp
// Web BFF — tek endpoint, 3 servisi birleştirir
[HttpGet("{id}/detail")]
public async Task<BookDetailDto> GetBookDetail(int id)
{
    // Paralel çağır — hepsini aynı anda başlat
    var bookTask    = _catalogClient.GetBookAsync(id);
    var reviewsTask = _reviewClient.GetReviewsAsync(id);
    var stockTask   = _stockClient.GetStockAsync(id);

    await Task.WhenAll(bookTask, reviewsTask, stockTask);

    return new BookDetailDto(
        Book:    bookTask.Result,
        Reviews: reviewsTask.Result,
        Stock:   stockTask.Result
    );
}
```

**Ne zaman BFF, ne zaman tek gateway?**

```
Tek gateway yeterli:
  → Tek client türü var (sadece web veya sadece mobil)
  → Tüm clientlar aynı veriyi istiyor
  → Kitabevi'mizde şimdilik bu durum

BFF gerekli:
  → Farklı client'lar çok farklı veri istiyor (mobil vs web vs admin)
  → Mobil performansı kritik, veri azaltmak önemli
  → Client'lar farklı ekiplerce geliştiriliyor
  → Her ekip kendi BFF'ini yönetiyor, bağımsız deploy
```

Kitabevi'mizde şimdilik tek gateway yeterli. Mobil uygulama çıkınca BFF'e geçmek mantıklı olur.

---

### API Gateway Antipattern — İş Mantığı Gateway'de Olmamalı

Gateway'in tek işi **yönlendirmek**. İş mantığı içermemeli.

```csharp
// ❌ Yanlış — gateway'de iş mantığı var
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        // YANLIŞ: sipariş fiyatı hesaplama gateway'de
        var discount = await _discountService.GetUserDiscountAsync(userId);
        context.Request.Headers["X-Discount"] = discount.ToString();
        await next();
    });
});

// ✅ Doğru — gateway sadece auth ve routing
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        // Sadece token doğrulama ve user id iletme — routing concern
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        context.Request.Headers["X-User-Id"] = userId;
        await next();
    });
});
```

**Neden?** İş mantığı gateway'e girince:
- Her iş değişikliğinde gateway deploy edilmek zorunda
- Gateway şişer, test etmesi zorlaşır
- "Resepsiyonist iş kararı veriyor" — sorumluluk karışıklığı

---

### Faz3 ile Karşılaştırma

Faz3'te tek uygulama vardı, routing ASP.NET Core controller'larla yapılıyordu:

```csharp
// Faz3 — tek uygulama, controller routing
[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase { }

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase { }
// Her şey tek process, routing framework tarafından yönetilir
```

Faz5'te her controller farklı bir serviste. Routing gateway'e taşındı:

```
Faz3: Kullanıcı → ASP.NET Core Router → Controller → Service
Faz5: Kullanıcı → YARP Gateway → Servis → Controller → Service
```

---

### 500 vs 50K Kullanıcı

| Karar | 500 kullanıcı/ay | 50K kullanıcı/ay |
|-------|-----------------|-----------------|
| API Gateway | ❌ Tek servis varsa overkill | ✅ Birden fazla servis varsa zorunlu |
| YARP | ✅ .NET stack için ideal | ✅ .NET stack için ideal |
| BFF | ❌ Overkill | ⚠️ Farklı client türleri varsa düşün |
| SSL termination gateway'de | ✅ Her zaman iyi pratik | ✅ Zorunlu |
| Rate limiting gateway'de | ⚠️ Opsiyonel | ✅ Public API için gerekli |
| Kong/Nginx | ❌ .NET için gereksiz karmaşıklık | ⚠️ .NET dışı servisler de varsa |

---

### Kontrol Soruları

1. Şirket resepsiyonu analogisiyle: rate limiting, SSL termination ve routing görevlerini resepsiyonistin gerçek hayattaki hangi davranışlarına benzetirsin?
2. Gateway seviyesinde JWT doğrulaması yapılırsa CatalogService token'ı tekrar doğrulamak zorunda mı? Neden / neden değil?
3. "API Gateway antipattern" nedir? İndirim hesaplama neden gateway'de yapılmamalı?
4. BFF pattern'i ne zaman tek gateway'den daha iyi? Kitabevi'nin kullanıcı kitlesi büyüseydi ne zaman BFF gerekli olurdu?
5. YARP ile Nginx arasında seçim yapman gerekse hangi kriterlere bakarsın?
