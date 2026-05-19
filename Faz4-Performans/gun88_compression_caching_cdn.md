# Gün 88 — Response Compression, Caching, CDN

---

## Büyük Resim — Bu Üçü Neden Bir Arada?

Bir API endpoint'in 50 KB JSON dönüyor diyelim. Kullanıcı her sayfa geçişinde aynı veriyi istiyor. Sunucun her seferinde:
1. Veritabanına gidiyor → sorgu çalıştırıyor
2. Sonucu serialize ediyor → 50 KB JSON oluşturuyor
3. 50 KB'ı olduğu gibi network'e veriyor

3 ayrı israf var:
- **Gereksiz hesaplama** → aynı veri değişmemişken neden tekrar hesaplıyorsun? → **Caching** çözer
- **Gereksiz büyük payload** → 50 KB'ı 12 KB'a düşürebilirsin → **Compression** çözer
- **Gereksiz uzun mesafe** → Amsterdam'daki sunucudan İstanbul'a 80ms latency → **CDN** çözer

Bu üç teknik birbirini tamamlar, biri diğerini ikame etmez.

---

## 1. Response Compression — Nedir?

### Konsept

Sunucu, HTTP yanıtını (JSON, HTML, CSS, JS) bir sıkıştırma algoritmasıyla küçültüp öyle gönderir. Tarayıcı/istemci otomatik açar.

**Gerçek hayat analojisi:** Mektup yazdın, zarfa sığmıyor. Kağıdı katlayıp zarfa koydun (sıkıştırma). Karşı taraf zarfı açınca kağıdı düzleştirdi (decompress). İçerik aynı, taşıma maliyeti düştü.

### Nasıl Çalışır? (Adım adım)

```
1. Tarayıcı istek gönderir:
   GET /api/kitaplar
   Accept-Encoding: br, gzip, deflate    ← "ben bu formatları açabilirim"

2. Sunucu yanıtı hazırlar (50 KB JSON)

3. Sunucu sıkıştırır → 12 KB (Brotli ile)

4. Yanıt gönderir:
   Content-Encoding: br                  ← "brotli ile sıkıştırdım"
   Content-Length: 12000

5. Tarayıcı otomatik açar → 50 KB JSON'u kullanır
```

### Brotli vs Gzip — Hangisini Ne Zaman?

| Özellik | Brotli | Gzip |
|---------|--------|------|
| Sıkıştırma oranı | %15-25 daha iyi | Standart |
| CPU maliyeti | Biraz daha yüksek | Düşük |
| Tarayıcı desteği | Modern tarayıcılar (Chrome, Firefox, Edge, Safari) | Her yer |
| Ne zaman tercih? | Statik dosyalar (önceden sıkıştırılabilir), CDN arkasında | Geriye uyumluluk lazımsa, çok düşük latency istiyorsan |

**Pratik karar:** İkisini birden ekle — sunucu Brotli'yi önce dener, tarayıcı desteklemezse Gzip'e düşer. Sen bir kere yapılandır, gerisini framework halleder.

### ASP.NET Core'da Konfigürasyon

```csharp
// Program.cs — Response Compression middleware kayıt
builder.Services.AddResponseCompression(opt =>
{
    opt.EnableForHttps = true;
    // ne yapar → HTTPS yanıtlarında da sıkıştırma aktif olur
    // bunu yazmasaydık → HTTPS yanıtları sıkıştırılMAZ (varsayılan false)
    // neden varsayılan false → CRIME/BREACH saldırısı riski (pratikte API'larda sorun değil)

    opt.Providers.Add<BrotliCompressionProvider>();
    // ne yapar → Brotli sıkıştırma sağlayıcısını listeye ekler
    // bunu yazmasaydık → yalnızca Gzip kullanılır

    opt.Providers.Add<GzipCompressionProvider>();
    // ne yapar → Gzip sıkıştırma sağlayıcısını listeye ekler
    // bunu yazmasaydık → Brotli desteklemeyen eski istemciler sıkıştırılmamış yanıt alır

    opt.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",           // API yanıtları
        "application/javascript",     // JS dosyaları
        "text/css"                    // CSS dosyaları
    });
    // ne yapar → hangi content-type'ların sıkıştırılacağını belirler
    // bunu yazmasaydık → varsayılan liste (text/plain, text/html vs.) kullanılır
    // JSON API'n varsa bunu eklemelisin, yoksa JSON sıkıştırılmaz
});

builder.Services.Configure<BrotliCompressionProviderOptions>(opt =>
{
    opt.Level = CompressionLevel.Fastest;
    // ne yapar → Brotli seviye 1 ile sıkıştırır (hızlı ama az sıkıştırma)
    // Optimal yazsaydık → %10 daha iyi sıkıştırma ama 3-5x CPU maliyeti
    // SmallestSize yazsaydık → en iyi sıkıştırma ama yanıt gecikmesi artar
    // API için Fastest yeterli — statik dosyalarda Optimal düşünülebilir
});

builder.Services.Configure<GzipCompressionProviderOptions>(opt =>
{
    opt.Level = CompressionLevel.Fastest;
    // Brotli ile aynı mantık — API'da hız öncelikli
});

// Middleware sırası ÖNEMLİ
app.UseResponseCompression();   // UseRouting'den ÖNCE olmalı
// bunu UseRouting'den sonra koyarsan → routing zaten response'u oluşturmuş olur,
// compression çalışmayabilir veya tutarsız davranır
app.UseRouting();
app.MapControllers();
```

### Compression Ne Zaman YAPILMAMALI?

- **Zaten sıkıştırılmış dosyalar:** PNG, JPEG, MP4, ZIP → tekrar sıkıştırmak boyutu artırabilir
- **Çok küçük yanıtlar:** 150 byte'lık yanıtı sıkıştırmak → header overhead'i ile net fayda yok
- **Streaming yanıtlar:** Chunk chunk gönderirken sıkıştırma buffer'laması latency ekler

---

## 2. HTTP Caching — Nedir?

### Konsept

Sunucu, yanıtla birlikte "bu veriyi şu süre sakla, tekrar sorma" talimatı gönderir. Kim saklar? Tarayıcı, CDN, proxy — hepsi bu talimata uyar.

**Gerçek hayat analojisi:** Arkadaşına "yarın hava nasıl?" diye sordun. "Güneşli, bu hafta değişmeyecek" dedi. Ertesi gün tekrar sormana gerek yok — cevap hafıza'nda (cache'te). Ama cuma günü tekrar sorarsın çünkü "bu hafta" süresi doldu.

### Cache-Control Header — Detaylı Açıklama

Bu header "kimin, ne kadar süre, nasıl cache'leyeceğini" belirler.

#### `public` vs `private`

```
Cache-Control: public, max-age=3600
```
- **Kim cache'ler:** Herkes — tarayıcı, CDN, ISP proxy'si
- **Ne kadar süre:** 3600 saniye (1 saat)
- **Ne zaman kullan:** Herkese aynı veriyi döndüğün endpoint'ler (kitap listesi, kategoriler, blog yazısı)

```
Cache-Control: private, max-age=300
```
- **Kim cache'ler:** YALNIZCA tarayıcı (CDN cache'lemez)
- **Ne kadar süre:** 300 saniye (5 dakika)
- **Ne zaman kullan:** Kullanıcıya özel veri — "benim siparişlerim", profil bilgisi
- **Neden private:** CDN cache'lerse başka kullanıcı senin verinizi görebilir!

#### `no-cache` vs `no-store` (En Çok Karıştırılan Konu)

```
Cache-Control: no-cache
```
- **Anlamı:** Cache'LE ama her kullanımda sunucuya "hâlâ geçerli mi?" sor
- **Ne zaman kullan:** Sık güncellenen ama genelde aynı kalan veri (ürün fiyatı — genelde aynı, ama değişebilir)
- **Avantaj:** Veri değişmediyse sunucu sadece `304 Not Modified` döner (boş body) → bant genişliği tasarrufu

```
Cache-Control: no-store
```
- **Anlamı:** ASLA cache'leme — disk'e yazma, bellekte tutma, hiçbir yerde saklama
- **Ne zaman kullan:** Kredi kartı bilgisi, şifre, kişisel sağlık verisi, ödeme token'ı
- **Neden `no-cache` yetmez:** `no-cache`'te veri hâlâ disk'te duruyor — paylaşımlı bilgisayarda başkası görebilir

#### `max-age` vs `s-maxage`

```
Cache-Control: public, max-age=60, s-maxage=300
```
- `max-age=60` → tarayıcı 1 dakika cache'ler
- `s-maxage=300` → CDN / shared proxy 5 dakika cache'ler
- **Neden farklı:** CDN'de cache invalidation yapabilirsin ama tarayıcı cache'ini kontrol edemezsin. CDN'e daha uzun süre ver, tarayıcıya kısa.

### ETag — Verinin Parmak İzi

ETag, yanıtın içeriğinden üretilen bir hash. Veri değişmedikçe ETag aynı kalır.

```
İlk İstek:
  GET /api/kitaplar/42
  → 200 OK
  → ETag: "a1b2c3d4"
  → Body: { "id": 42, "ad": "Clean Code", "fiyat": 150 }

İkinci İstek (tarayıcı otomatik gönderir):
  GET /api/kitaplar/42
  If-None-Match: "a1b2c3d4"       ← "bende bu versiyon var, değişti mi?"
  
  Veri değişmediyse:
  → 304 Not Modified              ← body YOK, sadece header — bant genişliği tasarrufu
  → tarayıcı eski cache'i kullanır

  Veri değiştiyse:
  → 200 OK
  → ETag: "e5f6g7h8"             ← yeni parmak izi
  → Body: { "id": 42, "ad": "Clean Code", "fiyat": 175 }   ← güncel veri
```

**Ne kazandın?**
- 50 KB'lık yanıt yerine 0 byte gönderdin (304 durumunda)
- Kullanıcı her zaman güncel veriyi görür (no-cache + ETag kombinasyonu)
- DB sorgusu yine çalışır ama network maliyeti düşer

### ASP.NET Core'da Cache Header Kullanımı

```csharp
// 1. Attribute ile — basit senaryolar
[HttpGet("kategoriler")]
[ResponseCache(
    Duration = 3600,                          // max-age=3600 (1 saat)
    Location = ResponseCacheLocation.Any,     // public — CDN de cache'leyebilir
    VaryByQueryKeys = new[] { "lang" }        // ?lang=tr ve ?lang=en ayrı cache'lenir
)]
// ne yapar → yanıta Cache-Control: public, max-age=3600 header'ı ekler
// bunu yazmasaydık → tarayıcı her seferinde sunucuya istek yapar
// Location=Client yazsaydık → CDN cache'lemez, sadece tarayıcı
public async Task<IActionResult> GetKategoriler([FromQuery] string lang = "tr")
{
    var kategoriler = await _repo.GetAllAsync(lang);
    return Ok(kategoriler);
}

// 2. Kullanıcıya özel veri — private cache
[HttpGet("profilim")]
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
// Client → private header'ı üretir — CDN cache'lemez
// Duration kısa (1 dk) çünkü profil değişebilir
public async Task<IActionResult> GetProfil()
{
    var userId = User.GetUserId();
    var profil = await _repo.GetProfilAsync(userId);
    return Ok(profil);
}

// 3. Hassas veri — asla cache'leme
[HttpGet("odeme-bilgilerim")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
// NoStore=true → Cache-Control: no-store header'ı üretir
// Duration=0 ve Location=None → ek güvenlik katmanı
public async Task<IActionResult> GetOdemeBilgileri()
{
    var bilgiler = await _repo.GetOdemeAsync(User.GetUserId());
    return Ok(bilgiler);
}

// 4. Cache Profile — tekrar eden ayarları tek yerde tanımla
// Program.cs'te:
builder.Services.AddControllersWithViews(opt =>
{
    opt.CacheProfiles.Add("Statik1Saat", new CacheProfile
    {
        Duration = 3600,
        Location = ResponseCacheLocation.Any
    });
    opt.CacheProfiles.Add("KisiyeOzel", new CacheProfile
    {
        Duration = 60,
        Location = ResponseCacheLocation.Client
    });
    opt.CacheProfiles.Add("CachYok", new CacheProfile
    {
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true
    });
});

// Controller'da:
[ResponseCache(CacheProfileName = "Statik1Saat")]
// ne yapar → Program.cs'teki profili uygular — DRY prensibi
// bunu yazmasaydık → her endpoint'e ayrı ayrı Duration/Location yazmak zorunda kalırdık
public async Task<IActionResult> GetYazarlar() { /* ... */ }
```

---

## 3. Output Caching (ASP.NET Core 7+) — Nedir?

### ResponseCache vs Output Cache — Temel Fark

| | ResponseCache | Output Cache |
|---|---|---|
| **Nerede çalışır?** | Tarayıcıda / CDN'de | Sunucu belleğinde |
| **Sunucu ne yapar?** | Header ekler, handler HER SEFERINDE çalışır | İlk istekte çalışır, sonra cache'ten döner |
| **DB'ye gider mi?** | Evet, her istekte | Hayır (cache süresi boyunca) |
| **Cache invalidation** | Kontrol edemezsin (tarayıcıdaki cache) | Programatik olarak temizleyebilirsin |
| **Ne zaman kullan?** | İstemci tarafı yeterli olduğunda | Sunucu yükünü düşürmek istediğinde |

**Analoji:** 
- ResponseCache = "müşteriye tarifi veriyorsun, kendi evinde pişirsin tekrar gelmene gerek yok"
- Output Cache = "yemeği sen pişirip buzdolabına koyuyorsun, aynı sipariş gelince tekrar pişirmeden servis ediyorsun"

### Detaylı Konfigürasyon

```csharp
// Program.cs — Output Cache kayıt
builder.Services.AddOutputCache(opt =>
{
    // Varsayılan politika — tüm GET istekleri
    opt.AddBasePolicy(policy => policy.Expire(TimeSpan.FromMinutes(2)));
    // ne yapar → belirtilmemiş tüm GET endpoint'leri 2 dk cache'lenir
    // bunu yazmasaydık → base policy olmaz, her endpoint'e ayrı politika lazım

    // İsimli politika — kitap detay
    opt.AddPolicy("KitapDetay", policy =>
        policy.SetVaryByRouteValue("id")
        // ne yapar → /kitaplar/1 ve /kitaplar/2 AYRI cache girişleri olur
        // bunu yazmasaydık → ilk istenen kitap herkese dönülür! (tehlikeli bug)
              .SetVaryByHeader("Accept-Language")
        // ne yapar → türkçe ve ingilizce yanıtlar ayrı cache'lenir
        // bunu yazmasaydık → ilk dil hangisiyse herkes o dilde görür
              .Expire(TimeSpan.FromMinutes(10))
              .Tag("kitaplar"));
        // ne yapar → bu cache girişlerini "kitaplar" tag'iyle gruplar
        // bunu yazmasaydık → tek tek invalidate etmek zorunda kalırsın

    // Kullanıcıya özel veri — cache'leme
    opt.AddPolicy("NoCache", policy => policy.NoCache());
    // ne yapar → bu politika atanan endpoint'ler hiç cache'lenmez
    // neden var → bazı endpoint'leri base policy'den muaf tutmak için
});

app.UseOutputCache();   // UseRouting'den SONRA, UseAuthorization'dan SONRA
// sıra önemli — auth'tan sonra olmalı ki yetkisiz istekler cache'lenmesini
```

### Endpoint'lerde Kullanım

```csharp
// Minimal API ile
app.MapGet("/kitaplar/{id}", async (int id, IKitapRepo repo) =>
{
    var kitap = await repo.GetAsync(id);       // sadece ilk istekte çalışır
    // bunu yazmasaydık (cache olmasa) → her istekte DB'ye gider
    return kitap is null ? Results.NotFound() : Results.Ok(kitap);
})
.CacheOutput("KitapDetay");
// ne yapar → "KitapDetay" politikasını bu endpoint'e uygular
// bunu yazmasaydık → sadece base policy uygulanır (VaryByRouteValue yok → bug!)

// Kullanıcıya özel endpoint — cache'lenmemeli
app.MapGet("/sepetim", async (HttpContext ctx, ISepetRepo repo) =>
{
    var userId = ctx.User.GetUserId();
    return Results.Ok(await repo.GetSepetAsync(userId));
})
.CacheOutput("NoCache");
// ne yapar → bu endpoint kesinlikle cache'lenmez
// bunu yazmasaydık → base policy devreye girer, Ahmet'in sepeti Mehmet'e görünür!
```

### Cache Invalidation — Veri Güncellendiğinde Ne Yaparsın?

```csharp
// Kitap güncellendiğinde ilgili cache'i temizle
app.MapPut("/kitaplar/{id}", async (
    int id,
    KitapGuncelleDto dto,
    IKitapRepo repo,
    IOutputCacheStore cacheStore) =>       // DI ile inject
{
    await repo.UpdateAsync(id, dto);

    // Tag bazlı invalidation — "kitaplar" tag'li TÜM cache girişlerini temizle
    await cacheStore.EvictByTagAsync("kitaplar", default);
    // ne yapar → kitap listesi + tüm kitap detay cache'leri temizlenir
    // bunu yazmasaydık → kullanıcı 10 dk boyunca eski veriyi görür
    // sadece tek id temizlemek istesen → tag'i "kitap-{id}" yapardın

    return Results.NoContent();
});
```

### Output Cache — Dikkat Edilmesi Gerekenler

1. **Authenticated endpoint'leri cache'leme:** Varsayılan olarak `Authorization` header'ı olan istekler cache'lenmez. Ama `SetVaryByHeader("Authorization")` yazarsan her token için ayrı cache girişi oluşur → bellek patlar.

2. **POST/PUT/DELETE cache'lenmez:** Output cache sadece GET ve HEAD istekleri cache'ler. Doğru davranış.

3. **Bellek limiti koy:**
```csharp
builder.Services.AddOutputCache(opt =>
{
    opt.SizeLimit = 100 * 1024 * 1024;   // 100 MB — cache bu boyutu geçemez
    // bunu yazmasaydık → varsayılan 100 MB (zaten makul)
    // çok düşük yazarsan → sık eviction olur, cache hit oranı düşer
});
```

---

## 4. CDN (Content Delivery Network) — Nedir?

### Konsept

CDN, içeriğini dünya genelindeki "edge" sunuculara kopyalar. Kullanıcı en yakın edge'den alır — hem hızlı hem sunucuna yük binmez.

**Gerçek hayat analojisi:** Bir kitabevi zincirisinin merkez deposu Amsterdam'da. İstanbul'daki müşteri sipariş verince Amsterdam'dan kargo geliyor (yavaş). Ama İstanbul'da bir şube açarsan — popüler kitaplar orada hazır, müşteri hemen alır. Nadir kitap? Şube merkeze sorar, getirir, bir kopyayı da rafına koyar (sonraki müşteri için).

### Nasıl Çalışır?

```
                         İnternet
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
   Edge (İstanbul)     Edge (Londra)      Edge (Tokyo)
        │                   │                   │
        └───────────────────┼───────────────────┘
                            │
                     Origin Server
                      (Amsterdam)

İstanbul'daki kullanıcı:
1. DNS → en yakın edge (İstanbul) çözümlenir
2. Edge'de cache var mı?
   → EVET: 5ms'de yanıt (cache HIT)
   → HAYIR: origin'e gider (80ms), yanıtı alır, kopyasını saklar (cache MISS)
3. Sonraki İstanbul kullanıcısı → 5ms (HIT)
```

### Ne Zaman CDN Kullanılır?

| Senaryo | CDN Gerekli mi? | Neden? |
|---------|-----------------|--------|
| Statik dosyalar (CSS, JS, resim) | EVET — her zaman | Değişmez, herkese aynı, büyük boyut |
| Public API yanıtları (kitap listesi) | EVET — kısa TTL ile | Herkese aynı, sık istenen |
| Kullanıcıya özel API (sepetim) | HAYIR | Her kullanıcıya farklı → cache'lenemez |
| Gerçek zamanlı veri (canlı skor) | HAYIR | Saniye bazında değişir → cache anlamsız |
| Ödeme/auth endpoint'leri | HAYIR | Güvenlik — asla cache'lenmemeli |

### CDN ile Çalışan Cache Header'ları

```csharp
// Statik dosyalar — 1 yıl cache (dosya adı versiyonlu: style.v3.css)
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.Name;
        if (path.EndsWith(".css") || path.EndsWith(".js") || path.EndsWith(".woff2"))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            // public → CDN cache'leyebilir
            // max-age=31536000 → 1 yıl
            // immutable → tarayıcı "hâlâ geçerli mi?" bile sormaz
            // bunu yazmasaydık → tarayıcı her navigasyonda revalidation isteği yapar
            // neden güvenli → dosya adı değişince (v3→v4) yeni URL = yeni cache girişi
        }
    }
});

// API yanıtları — CDN'e kısa, tarayıcıya daha kısa
[HttpGet("kitaplar")]
[ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "sayfa", "kategori" })]
// Bu header'ı CDN için özelleştirmek istersen:
public async Task<IActionResult> GetKitaplar(int sayfa = 1, int? kategori = null)
{
    Response.Headers["CDN-Cache-Control"] = "max-age=300";
    // ne yapar → CDN'e "5 dk cache'le" der (Cloudflare/Vercel bu header'ı tanır)
    // bunu yazmasaydık → CDN de Cache-Control'deki 60 saniyeyi kullanır
    // neden CDN'e daha uzun → CDN'de purge yapabilirsin, tarayıcıda yapamazsın

    var kitaplar = await _repo.GetListAsync(sayfa, kategori);
    return Ok(kitaplar);
}
```

### Popüler CDN Sağlayıcıları

| Sağlayıcı | Avantaj | Ne Zaman Tercih? |
|-----------|---------|-------------------|
| **Cloudflare** | Ücretsiz planı var, kolay kurulum | Küçük-orta projeler, başlangıç |
| **AWS CloudFront** | AWS ekosistemi ile entegre | Zaten AWS kullanıyorsan |
| **Azure CDN** | Azure ekosistemi ile entegre | .NET/Azure projeleri |
| **Vercel Edge** | Next.js/frontend odaklı | Frontend deployment |

### CDN Invalidation (Cache Temizleme)

Veri güncellediğinde CDN cache'inin eski veriyi göstermesini istemezsin:

```
Yöntem 1 — TTL (Time To Live):
  Cache-Control: public, max-age=300   → 5 dk sonra otomatik expire olur
  Pro: Basit. Con: 5 dk boyunca eski veri gösterilir.

Yöntem 2 — Purge/Invalidate API:
  POST https://api.cloudflare.com/zones/{zone}/purge_cache
  Body: { "files": ["https://api.example.com/kitaplar"] }
  Pro: Anında temizlenir. Con: API çağrısı gerekir, rate limit var.

Yöntem 3 — Versiyonlu URL (statik dosyalar için en iyi):
  style.css?v=1  →  style.css?v=2
  veya
  style.abc123.css → style.def456.css
  Pro: Eski URL'yi purge etmeye gerek yok. Con: Sadece dosya adı değişebilen şeyler.
```

---

## 5. Üçünü Birlikte Kullanmak — Tam Senaryo

Bir kitap listeleme endpoint'i düşün:

```
İstek akışı (optimum):

Kullanıcı (İstanbul)
  → CDN edge (İstanbul) — cache HIT? → Sıkıştırılmış 12 KB yanıt, 5ms ✓
                         — cache MISS? ↓
  → Origin sunucu (Amsterdam)
      → Output Cache — HIT? → Sıkıştırılmış yanıt, 20ms ✓
                     — MISS? ↓
      → DB sorgusu çalışır → 50 KB JSON üretilir
      → Output Cache'e yazılır (10 dk)
      → Compression → 12 KB'a düşer
      → CDN'e yanıt gider + Cache-Control header
      → CDN kendi edge'ine yazar (5 dk)
      → Kullanıcıya ulaşır

Sonraki kullanıcı (İstanbul, 1 dk sonra):
  → CDN edge → HIT → 5ms, sunucuya hiç gitmez
```

**Katman katman tasarruf:**
- Compression: 50 KB → 12 KB (%76 bant genişliği tasarrufu)
- Output Cache: DB sorgusu 1 yerine 0 (10 dk boyunca)
- CDN: Latency 80ms → 5ms, origin'e istek sayısı %90 azalır

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de:
- **Compression yok** → her yanıt sıkıştırılmadan gönderiliyor. 500 kullanıcıda fark etmezsin. 50K'da 50 KB × 50.000 = 2.5 GB/gün gereksiz transfer.
- **Cache header yok** → tarayıcı her sayfa geçişinde her şeyi yeniden istiyor. Sunucu aynı kitap listesini dakikada 100 kez hesaplıyor.
- **Output Cache yok** → her istek DB'ye gidiyor. 50K kullanıcıda DB connection pool tükenir.
- **CDN yok** → tüm trafik tek sunucuya vuruyor. İstanbul'dan Amsterdam'a 80ms latency her istekte.

```csharp
// Faz2'de böyle yaptık — herhangi bir cache mekanizması yok:
public async Task<IActionResult> Index()
{
    var kitaplar = await _context.Kitaplar.ToListAsync();  // her istekte DB'ye gider
    return View(kitaplar);                                  // sıkıştırılmadan gönderilir
}

// Faz4'te böyle yapıyoruz:
app.MapGet("/kitaplar", async (IKitapRepo repo) =>
{
    var kitaplar = await repo.GetListAsync();
    return Results.Ok(kitaplar);
})
.CacheOutput("KitapListesi");  // sunucu cache'ler → DB'ye gitmez
// + ResponseCompression middleware → 12 KB'a düşer
// + Cache-Control header → CDN ve tarayıcı da cache'ler
```

---

## 500 vs 50K Kullanıcı — Ne Zaman Ne Kullanmalısın?

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay | Overengineering sinyali |
|--------|-------------------|-------------------|--------------------------|
| **Response Compression** | Ekle — 2 satır config, zararı yok | Zorunlu — bant genişliği maliyeti belirgin | Compression seviyesiyle saatlerce uğraşmak |
| **Cache-Control headers** | `max-age=300` yeterli | ETag + s-maxage + vary — tam strateji lazım | 500 kullanıcıda CDN-Cache-Control header'ları |
| **Output Cache** | Çoğu endpoint'te gereksiz | Sıcak endpoint'lerde DB yükünü %90 düşürür | Her endpoint'i cache'lemek (invalidation karmaşası) |
| **CDN** | Statik dosyalar için belki | Zorunlu — global kullanıcı, yük dağılımı | 500 kullanıcı, tek coğrafya, sadece API |
| **Cache Invalidation stratejisi** | TTL yeterli | Tag-based + purge API lazım | Küçük projede event-driven invalidation |

---

## Sık Yapılan Hatalar

### 1. Kullanıcıya özel veriyi public cache'lemek
```
❌ Cache-Control: public, max-age=300   (sepetim endpoint'inde)
→ CDN cache'ler → Ahmet'in sepeti Mehmet'e görünür!

✓ Cache-Control: private, max-age=60    (veya no-store)
```

### 2. Output Cache'te VaryBy unutmak
```csharp
// ❌ id'ye göre vary yok
app.MapGet("/kitaplar/{id}", ...).CacheOutput();
// → ilk istenen kitap (id=1) herkese dönülür, id=2 isteyen de id=1 görür

// ✓ 
.CacheOutput(p => p.SetVaryByRouteValue("id"))
```

### 3. Cache süresini çok uzun tutmak (invalidation olmadan)
```
❌ max-age=86400 (1 gün) — invalidation mekanizması yok
→ kitap fiyatı güncellendi ama kullanıcı 1 gün boyunca eski fiyatı görür

✓ max-age=300 + ETag   veya   max-age=3600 + purge mekanizması
```

### 4. Compression'ı statik dosyalarda runtime yapmak
```
❌ Her istekte CSS/JS sıkıştırılıyor (CPU israfı)
✓ Build time'da sıkıştır (webpack/vite brotli plugin), sunucu hazır dosyayı gönderir
```

---

## Kontrol Soruları

1. `Cache-Control: no-cache` ile `no-store` arasındaki fark nedir? Hangi senaryoda hangisini kullanırsın?
2. Output Cache ile ResponseCache attribute'u arasındaki temel fark nedir? Hangisi DB yükünü düşürür?
3. ETag nasıl çalışır? Sunucu tarafında ne kazandırır, ne kazandırmaz?
4. Kullanıcının sepet bilgisini CDN'e `public` olarak cache'lemek neden tehlikelidir?
5. Bir kitap güncelleme endpoint'i çağrıldığında Output Cache nasıl invalidate edilir?
6. `s-maxage` nedir ve neden `max-age`'den farklı bir değer verilir?
7. Brotli ve Gzip arasındaki trade-off nedir? API'da hangisini neden tercih edersin?
8. Versiyonlu URL (style.v3.css) cache stratejisinde neden `immutable` kullanabilirsin?
