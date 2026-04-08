# Gün 26 — Caching Stratejileri

---

## 1. Neden Cache Gerekir?

Her HTTP isteğinde veritabanına gidilirse ne olur?

```
Kullanıcı → /kitaplar → KitapServisi.HepsiniGetir() → DB sorgusu → 50ms
Kullanıcı → /kitaplar → KitapServisi.HepsiniGetir() → DB sorgusu → 50ms
Kullanıcı → /kitaplar → KitapServisi.HepsiniGetir() → DB sorgusu → 50ms
… (saniyede 500 istek gelirse: 500 × DB sorgusu)
```

Bu veriler sık değişmiyorsa (kitap listesi güne bir kez güncelleniyor) her seferinde veritabanına gitmek israf. Cache bunu engeller:

```
İlk istek   → DB sorgusu (50ms) → sonucu cache'e yaz
2–500. istek → cache'ten oku (1ms)
```

**Ne zaman cache işe yarar?**
- Verinin okuma/yazma oranı yüksekse (örnek: kitap listesi — 1000 okuma, 1 yazma)
- Hesaplama maliyeti yüksekse (rapor üretimi, ağır sorgu)
- Kısa süreli tutarsızlık tolere edilebiliyorsa

**Ne zaman cache yanlış seçimdir?**
- Her kullanıcıya özel veri (kullanıcının sipariş geçmişi — ortak cache işe yaramaz)
- Her istek için farklı veri döndürülüyorsa
- Anlık tutarlılık zorunluysa (banka bakiyesi)

---

## 2. IMemoryCache — Süreç İçi Cache

En basit yaklaşım: veriler uygulamanın belleğinde saklanır. Harici altyapı yok, kurulum sıfır.

```
HTTP Request → IMemoryCache → veri var → döndür (cache hit)
                            → veri yok → DB'den al → cache'e yaz → döndür (cache miss)
```

**Kayıt (Program.cs):**

```csharp
// "AddMemoryCache" → IMemoryCache'i DI container'a singleton olarak kaydeder.
// Boyut sınırı belirtilmezse belleği sınırsız tüketir — dikkat.
builder.Services.AddMemoryCache(options =>
{
    // Toplam cache boyutu: 1024 birim (birim sen tanımlarsın — byte, öğe sayısı vb.)
    // Her öğe kaydedilirken .SetSize(n) ile kaç birim kapladığı belirtilir.
    options.SizeLimit = 1024;
});
```

**Temel kullanım:**

```csharp
public class KitapServisi
{
    private readonly IMemoryCache _cache;
    private const string TumKitaplarAnahtar = "kitaplar:hepsi";

    public IReadOnlyList<KitapListeViewModel> HepsiniGetir()
    {
        // "TryGetValue" → cache'te var mı? Varsa "out" parametresine atar, true döner.
        // Varsa → doğrudan döndür (DB sorgusu yok)
        if (_cache.TryGetValue(TumKitaplarAnahtar, out IReadOnlyList<KitapListeViewModel>? cachedKitaplar))
        {
            return cachedKitaplar!; // cache hit
        }

        // Cache'te yok → DB'den al
        var kitaplar = _repository.HepsiniGetir();

        // Cache'e yaz: 5 dakika sonra otomatik silinir
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
            .SetSize(1); // bu öğe 1 birim yer kaplar

        _cache.Set(TumKitaplarAnahtar, kitaplar, cacheOptions);

        return kitaplar; // cache miss, DB'den döndürüldü
    }
}
```

---

## 3. Eviction Policies — Cache'ten Ne Zaman Silinir?

Cache sınırsız büyüyemez. Üç mekanizma ile öğeler silinir:

**Absolute Expiration (mutlak son kullanma tarihi):**

```csharp
// Yazıldıktan tam 5 dakika sonra silinir — kullanılıp kullanılmadığına bakılmaz.
options.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

// veya mutlak tarih:
options.SetAbsoluteExpiration(DateTimeOffset.UtcNow.AddHours(1));
```

**Sliding Expiration (kayar son kullanma tarihi):**

```csharp
// Son erişimden 10 dakika geçerse silinir.
// Her okunduğunda süre sıfırlanır — aktif kullanımdaysa canlı kalır.
options.SetSlidingExpiration(TimeSpan.FromMinutes(10));

// Dikkat: sliding + absolute birlikte kullanılabilir.
// Sürekli okunan bir öğenin sonsuza kadar yaşamasını önlemek için:
options
    .SetSlidingExpiration(TimeSpan.FromMinutes(10)) // hareketsizlikte 10dk'da sil
    .SetAbsoluteExpiration(TimeSpan.FromHours(1));  // ama en fazla 1 saat
```

**Priority (bellek baskısı altında öncelik):**

```csharp
// Sistem belleği azaldığında önce düşük öncelikli öğeler silinir.
// Low → Normal → High → NeverRemove
options.SetPriority(CacheItemPriority.High);
```

**Boyut sınırı:**

```csharp
// AddMemoryCache'de SizeLimit belirlediysen her öğeye boyut atamalısın.
// Boyut verilmezse SizeLimit olan cache'te hata fırlatır.
options.SetSize(1); // bu öğe 1 birim
```

---

## 4. Cache-Aside Pattern — En Yaygın Yaklaşım

"Uygulama kodu cache'i kendisi yönetir" — cache kütüphanesi otomatik doldurmaz:

```
Oku  → cache'e bak → var → döndür
               → yok → kaynağa git → cache'e yaz → döndür

Yaz  → kaynağa yaz → cache'teki ilgili anahtarı sil (invalidate)
```

**`GetOrCreateAsync` — cache-aside'ı tek satıra indirger:**

```csharp
// "GetOrCreateAsync" → anahtarı ara; yoksa factory'yi çalıştır, sonucu yaz, döndür.
// İki adımı (TryGetValue + Set) tek çağrıya sıkıştırır.
var kitaplar = await _cache.GetOrCreateAsync(
    key: "kitaplar:hepsi",
    factory: async entry =>
    {
        // Bu blok sadece cache miss'te çalışır
        entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
        entry.SetSize(1);
        return await _repository.HepsiniGetirAsync();
    });
```

**Yazma sırasında invalidation:**

```csharp
public async Task<int> Ekle(KitapFormViewModel model)
{
    var id = await _repository.EkleAsync(model);

    // Kitap listesi değişti → eski cache geçersiz, sil.
    // Bir sonraki okuma DB'den taze veriyi alır ve yeniden cache'e yazar.
    _cache.Remove("kitaplar:hepsi");

    return id;
}
```

---

## 5. IDistributedCache — Dağıtık Cache (Redis)

`IMemoryCache`'in sınırlaması: tek sunucuda çalışır. 3 sunucu varsa her birinde ayrı bellek cache'i olur — tutarsızlık kaçınılmaz.

```
Sunucu 1: kitaplar cache'te var (5 dakika önce yazıldı)
Sunucu 2: kitaplar cache'te yok (ilk kez istek geldi) → DB'den çekti
Sunucu 3: kitaplar cache'te var ama Sunucu 1'den farklı versiyon
```

**Redis** bu sorunu çözer — merkezi, tüm sunucular aynı cache'i okur/yazar:

```
Sunucu 1 → Redis → kitaplar var → döndür
Sunucu 2 → Redis → kitaplar var → döndür  (aynı veri)
Sunucu 3 → Redis → kitaplar var → döndür  (aynı veri)
```

**Kayıt:**

```csharp
// StackExchange.Redis paketi gerekir: dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    // appsettings.json: "Redis": "localhost:6379"
    options.InstanceName = "kitabevi:"; // tüm anahtarlara prefix — çakışmayı önler
});
```

**Kullanım — IDistributedCache byte[] ile çalışır:**

```csharp
public async Task<IReadOnlyList<KitapListeViewModel>> HepsiniGetirAsync()
{
    const string anahtar = "kitaplar:hepsi";

    // Redis'ten byte[] olarak al
    var cachedBytes = await _distributedCache.GetAsync(anahtar);

    if (cachedBytes is not null)
    {
        // Byte[] → JSON → nesne
        var json = Encoding.UTF8.GetString(cachedBytes);
        return JsonSerializer.Deserialize<List<KitapListeViewModel>>(json)!;
    }

    // Cache miss → DB'den al
    var kitaplar = await _repository.HepsiniGetirAsync();

    // Nesne → JSON → byte[]
    var jsonYaz = JsonSerializer.Serialize(kitaplar);
    var bytes   = Encoding.UTF8.GetBytes(jsonYaz);

    var cacheOptions = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    await _distributedCache.SetAsync(anahtar, bytes, cacheOptions);

    return kitaplar;
}
```

**IMemoryCache vs IDistributedCache özet:**

```
IMemoryCache      → tek sunucu, nesne olarak saklar, çok hızlı (RAM)
IDistributedCache → çok sunucu, byte[] olarak saklar, ağ gecikmesi var (Redis ~1ms)
```

---

## 6. Output Cache — Yanıtı Önbelleğe Al

`IMemoryCache` uygulama katmanını önbelleğe alır. **Output Cache** HTTP yanıtının tamamını saklar — controller çalışmaz bile.

```
İlk istek  → controller çalışır → yanıt üretilir → yanıt cache'e yazılır
Sonraki    → controller çalışmaz → cache'teki yanıt doğrudan döndürülür
```

**Kayıt (ASP.NET Core 7+):**

```csharp
builder.Services.AddOutputCache(options =>
{
    // "kitaplar" adında bir policy tanımla
    options.AddPolicy("kitaplar", builder =>
        builder.Expire(TimeSpan.FromMinutes(5))
               .Tag("kitaplar-tag")); // tag ile toplu invalidation
});

// Pipeline'a ekle (UseRouting'den SONRA, MapControllers'dan ÖNCE)
app.UseOutputCache();
```

**Controller'da kullanım:**

```csharp
// Bu action'ın yanıtı 5 dakika cache'lenir.
// İkinci istekte controller kodu çalışmaz — cache'teki yanıt döner.
[OutputCache(PolicyName = "kitaplar")]
public async Task<IActionResult> Index()
{
    var kitaplar = await _kitapServisi.HepsiniGetirAsync();
    return View(kitaplar);
}
```

**Tag ile invalidation — veri değişince cache'i temizle:**

```csharp
[HttpPost]
public async Task<IActionResult> Ekle(KitapFormViewModel model)
{
    await _kitapServisi.EkleAsync(model);

    // "kitaplar-tag" etiketli tüm cache girdilerini temizle
    await _outputCacheStore.EvictByTagAsync("kitaplar-tag", HttpContext.RequestAborted);

    return RedirectToAction(nameof(Index));
}
```

---

## 7. Write-Through vs Write-Behind

Cache güncelleme stratejileri — "veri yazıldığında cache ne yapmalı?"

**Write-Through — yaz ve cache'i güncelle:**

```
Uygulama → DB'ye yaz → cache'e de yaz (veya güncelle) → döndür

Avantaj: cache her zaman güncel
Dezavantaj: yazma yavaşlar (hem DB hem cache)
```

```csharp
public async Task<bool> Guncelle(KitapFormViewModel model)
{
    var basarili = await _repository.GuncelleAsync(model);
    if (!basarili) return false;

    // DB'ye yazdık, cache'i de güncelleyelim — tutarlı
    var cacheOptions = new MemoryCacheEntryOptions()
        .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
        .SetSize(1);

    _cache.Set($"kitap:{model.Id}", model, cacheOptions);
    _cache.Remove("kitaplar:hepsi"); // liste cache'ini geçersiz kıl

    return true;
}
```

**Write-Behind (Write-Back) — önce cache, sonra DB:**

```
Uygulama → cache'e yaz → hemen döndür (hızlı!)
                       → arka planda DB'ye yaz (gecikmiş)

Avantaj: yazma çok hızlı
Dezavantaj: uygulama çökerse cache'deki veri kaybolur
Kullanım: sosyal medya beğeni sayaçları, analitik — küçük kayıp tolere edilir
```

**Uygulama çoğunluğu için önerilen:** Cache-aside (okuma) + Write-Through (yazma) veya sadece invalidation.

---

## 8. Cache Invalidation — "2 Hard Problems" Biri

Computer science'ın en ünlü sözlerinden biri:

> "There are only two hard things in Computer Science: cache invalidation and naming things." — Phil Karlton

Neden zor? Veriler birbirine bağlı. Kitap güncellenince sadece "kitap:5" değil, belki "kitaplar:hepsi", "kitaplar:roman", "yazar:orwell:kitaplar" da geçersiz olur.

**Stratejiler:**

```
TTL (Time-to-Live)       → sür ve unut — en basit, tutarsızlık tolere ediliyorsa
Aktif invalidation       → veri değişince ilgili anahtarı sil — güvenilir ama karmaşık
Tag-based invalidation   → gruplara tag ver, tag silerek toplu temizle
Event-driven invalidation → veri değiştiğinde event yayınla, tüm sunucular kendi cache'ini temizlesin
```

**Tag tabanlı örnek (Output Cache ile):**

```csharp
// Tüm kitap sayfaları "kitaplar" tag'iyle işaretlendi.
// Bir kitap güncellenince tek komutla hepsini temizleyebilirsin.
await _outputCacheStore.EvictByTagAsync("kitaplar", cancellationToken);
```

---

## 9. Stampede Problemi (Thundering Herd)

Popüler bir cache girdisi süresini doldurduğunda ne olur?

```
Cache girişi sona erdi
10.000 eş zamanlı istek geldi
→ 10.000'i de "cache miss" görür
→ 10.000'i de DB'ye sorgu gönderir
→ DB çöker
```

Bu probleme **cache stampede** veya **thundering herd** denir.

**Çözüm 1 — Locking (kilit):**

```csharp
// "GetOrCreateAsync" + SemaphoreSlim → ilk istek DB'ye gitsin, geri kalanlar beklesin.
// İlk istek sonucu cache'e yazınca diğerleri cache'ten okur.
private readonly SemaphoreSlim _kilit = new(1, 1);

public async Task<IReadOnlyList<KitapListeViewModel>> HepsiniGetirAsync()
{
    if (_cache.TryGetValue("kitaplar:hepsi", out var cachedKitaplar))
        return cachedKitaplar!;

    await _kilit.WaitAsync(); // sadece bir thread geçer
    try
    {
        // İkinci kontrol: kilit alındı, bir önceki thread yüklemiş olabilir
        if (_cache.TryGetValue("kitaplar:hepsi", out cachedKitaplar))
            return cachedKitaplar!;

        var kitaplar = await _repository.HepsiniGetirAsync();
        _cache.Set("kitaplar:hepsi", kitaplar,
            new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                .SetSize(1));
        return kitaplar;
    }
    finally
    {
        _kilit.Release();
    }
}
```

**Çözüm 2 — Stale-While-Revalidate:**

```
Cache girdisi "yumuşak son kullanma tarihi"ne gelince:
→ mevcut (eski) veriyi döndür (hızlı)
→ arka planda yenile (kullanıcıyı bekletme)
```

**Çözüm 3 — Rastgele TTL jitter:**

```csharp
// Tüm cache girdileri aynı anda dolmasın → rastgele ±30 saniye ekle
var jitter = Random.Shared.Next(-30, 30);
options.SetAbsoluteExpiration(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(jitter)));
```

---

## 10. KitabeviMVC'de Cache-Aside Uygulaması

Mevcut `KitapServisi` zaten in-memory liste kullanıyor (gerçek projede DB olurdu). Gerçek projede `IMemoryCache` katmanı servisin üzerine eklenir:

Bkz. [Services/CachedKitapServisi.cs](KitabeviMVC/Services/CachedKitapServisi.cs)

Temel yapı — Decorator pattern ile mevcut servisi sarmala:

```
HTTP Request → KitapController
                  → CachedKitapServisi (IMemoryCache kontrolü)
                       → cache hit  → döndür
                       → cache miss → KitapServisi (DB / veri kaynağı) → cache'e yaz → döndür
```

Bu yaklaşımın avantajı: `KitapServisi` caching'den habersiz, tek sorumluluk korunur. Cache katmanı ayrı sınıfta yaşar — değiştirmek veya devre dışı bırakmak kolay.

**DI kaydı (Program.cs):**

```csharp
// İki aşamalı kayıt: önce somut tip, sonra decorator.
// DI container IKitapServisi isteyenlere CachedKitapServisi verir.
// CachedKitapServisi içindeki KitapServisi bağımlılığını somut tipten çözer.
builder.Services.AddSingleton<KitapServisi>();
builder.Services.AddSingleton<IKitapServisi>(sp =>
    new CachedKitapServisi(
        sp.GetRequiredService<KitapServisi>(),
        sp.GetRequiredService<IMemoryCache>(),
        sp.GetRequiredService<ILogger<CachedKitapServisi>>()));
```

**Uygulamadaki özellikler:**

| Metod | Cache Stratejisi | TTL | Neden |
|---|---|---|---|
| `HepsiniGetir()` | Double-checked locking + Cache-Aside | 5dk abs, 2dk sliding | Stampede koruması, en çok çağrılan metod |
| `KategoriyeGoreGetir()` | GetOrCreate | 5dk abs, 2dk sliding | Her kategori ayrı key |
| `BulById()` | Cache-Aside + Negative Cache | 10dk (bulunan), 30sn (null) | Detay sayfası yükü + scraper önleme |
| `Ekle()` | Invalidation | — | Liste + kategori cache'i temizle |
| `Guncelle()` | Invalidation | — | Bireysel + liste + kategori temizle |
| `Sil()` | Invalidation | — | Silinen kitabın tüm cache'ini temizle |
| `BaslikVarMi()` | **Cache YOK** | — | Veri bütünlüğü — anlık tutarlılık zorunlu |

---

## 11. Seçim Kılavuzu

```
Tek sunucu, basit senaryo                 → IMemoryCache
Birden fazla sunucu (load balancer)        → IDistributedCache + Redis
HTTP yanıtının tamamını cache'le           → Output Cache
Anlık tutarsızlık tolere edilemez          → Cache kullanma veya event-driven invalidation
Her kullanıcıya özel veri                  → Cache key'e kullanıcı ID'si ekle
Ağır hesaplama (rapor, aggregate sorgu)    → IMemoryCache + uzun TTL
Sık değişen veri (anlık fiyat)             → Cache kullanma veya çok kısa TTL (30 sn)
```

---

## 12. Dikkat Edilmesi Gerekenler

**Cache anahtarı çakışması:** Farklı sorgular aynı anahtarı kullanırsa yanlış veri döner.

```csharp
// Kötü: parametre cache key'ine yansımıyor
_cache.Set("kitaplar", KategoriyeGoreGetir("Roman")); // "Roman" kayboldu

// İyi: parametreyi key'e dahil et
_cache.Set($"kitaplar:kategori:{kategori.ToLower()}", sonuc);
```

**Büyük nesneleri cache'leme:** 100 MB'lık raporu cache'e atmak RAM'i tüketir. Boyut limiti ve `SetSize` birlikte kullanılmalı.

**IMemoryCache thread-safety:** `IMemoryCache` thread-safe okuma/yazma sağlar ama "kontrol et, sonra yaz" (check-then-act) operasyonu atomik değildir. `GetOrCreateAsync` bu sorunu çözer — tek çağrıda kontrol + yazma.

**Distributed cache serileştirme:** `IDistributedCache` byte[] alır — serileştirme hatası (type mismatch, null değer) cache hit olsa bile runtime'da patlar. Model değiştiğinde eski cache girdisi bozuk deserialize olabilir. Versiyon veya TTL ile önle.

**Output Cache ve kimlik doğrulama:** Output Cache varsayılan olarak authentication cookie'sine göre farklı yanıt saklamaz. Giriş yapmış kullanıcılara özel sayfalarda kullanırsan yanlış kullanıcıya yanlış veri döner. `Vary` ile ayarla veya authenticated route'larda Output Cache kullanma.

---

## 13. Gerçek Hayatta Cache Zorunlu Olan Senaryolar

Cache'i "isteğe bağlı optimizasyon" olarak düşünmek yanıltıcı olur. Aşağıdaki senaryolarda cache olmadan uygulama ya çöker, ya kabul edilemez biçimde yavaşlar, ya da astronomik maliyet üretir.

---

### E-Ticaret

**Ürün kataloğu ve kategori ağacı**

Amazon, Trendyol, Hepsiburada gibi platformlarda ana sayfa ve kategori sayfaları saniyede on binlerce kez görüntülenir. Ürün kataloğu ise milyonlarca satırdan oluşur.

```
Senaryo: 50.000 eş zamanlı kullanıcı, her biri ana sayfayı açıyor.
Cache YOK → 50.000 DB sorgusu/saniye → DB sunucusu CPU %100 → site çöküyor.
Cache VAR → 1 DB sorgusu/5dakika → 49.999 istek bellekten yanıtlanıyor.
```

Cache olmadan bu ölçekte çalışmak fiziksel olarak imkânsız — sayfa başına 500ms DB süresi, saniyede 50.000 sayfa = 25.000 saniye sürer (1 saniye içinde).

**Fiyat ve stok gösterimi**

Ürün detay sayfasında fiyat ve stok miktarı gösterilir. Fiyat her 10 dakikada bir güncellenir, stok değişimi anlık önem taşımaz (sepete ekleyince kontrol edilir).

```csharp
// Fiyat: 10 dakika cache — kullanıcı "eskimiş" fiyat görebilir ama tolere edilir.
// Sepete ekleme anında taze fiyat alınır — o noktada cache bypass edilir.
_cache.GetOrCreate($"urun:{urunId}:fiyat",
    entry => { entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(10)); return fiyatServisi.Getir(urunId); });
```

**Arama sonuçları**

"iPhone 15" araması günde yüz binlerce kez yapılır. Her seferinde milyonlarca ürün içinden full-text search + sıralama + filtreleme yapılması yerine:

```
"iphone 15" → cache key → mevcut → 2ms'de döner
             → yok      → Elasticsearch sorgusu (200ms) → cache'e yaz (5 dk TTL)
```

Özellikle kampanya dönemlerinde (Black Friday, 11.11) arama trafiği 100x artabilir. Cache olmadan Elasticsearch cluster'ı bu yükü kaldıramaz.

**Sepet ve checkout oturumu**

Kullanıcının sepeti her sayfada gösterilir (header'da ürün sayısı). Her page load'da DB sorgusu yerine:

```
Kullanıcı ID → Redis → sepet bilgisi (30 dk sliding expiration)
```

Sepet Redis'te yaşar. Tarayıcı kapansa bile 30 dakika içinde geri gelince sepet korunur.

---

### Finansal Sistemler ve Borsa

**Döviz kurları**

Bir banka uygulaması döviz kurunu binlerce ekranda gösterir. Merkez bankası API'si saniyede 1 kez güncellenir, dakikada 1 kez sorgulanması yeterlidir.

```
Merkez bankası API'si: rate limit = saniyede 10 istek.
Uygulamaya gelen istek: saniyede 5000.
Cache olmadan → API rate limit aşıldı → tüm kullanıcılar hata görür.
Cache VAR → API'ye dakikada 1 kez git → kur 60 saniyeye kadar eski olabilir (kabul edilir).
```

**Hisse senedi fiyatları — ne zaman cache YANLIŞ seçimdir?**

```
Borsa ekranı: gerçek zamanlı fiyat gösterimi.
Cache → yanlış: 5 saniye önce 150₺ olan hisse şimdi 130₺ olmuş olabilir.
Çözüm: WebSocket / SignalR ile push — HTTP cache değil, event-driven.
```

Bu örnek önemli: "anlık tutarlılık zorunlu" senaryolarda cache kullanılmaz, push mimarisi tercih edilir.

**Hesap bakiyesi**

Banka bakiyesi asla cache'lenmemelidir. Başka kanaldan para çekilmiş olabilir. Finansal uygulamalarda her bakiye sorgusu DB'ye (veya çekirdek bankacılık sistemine) gider.

```
Cache → TEHLİKELİ: müşteri hesabında 1000₺ var, başka ATM'den 900₺ çekildi,
        cache hâlâ 1000₺ gösteriyor → çifte harcama riski (double-spend).
```

---

### Gaming ve Sosyal Medya

**Liderlik tablosu (leaderboard)**

Oyunlarda anlık sıralama listeleri saniyede binlerce kez görüntülenir. Sıralama 60 saniyede bir hesaplanır.

```
Redis Sorted Set → liderlik tablosu burada yaşar.
Her oy/puan değişikliğinde ZADD ile güncellenir (O(log n)).
Okuma: ZRANGE → her zaman cache'ten → DB sorgusu yok.

Alternatif: 60 sn'de bir arka plan job hesaplar, sonucu Redis'e yazar.
Kullanıcılar her zaman Redis'ten okur.
```

**Beğeni ve yorum sayaçları**

Instagram'da bir gönderi saniyede binlerce beğeni alabilir. Her beğenide DB'ye `UPDATE SET likes = likes + 1` yazmak performans katilidir.

```
Write-Behind (Write-Back) stratejisi:
1. Beğeni geldi → Redis'te sayacı artır (INCR) → anında döndür
2. Arka plan job'u (10 sn'de bir) → Redis'teki değeri DB'ye yaz

Küçük kayıp kabul edilir: uygulama çökerse son 10 sn'deki beğeniler kaybolur.
Fayda: DB'ye yazma trafiği 100x azalır.
```

**Kullanıcı profil bilgisi**

Her sayfada gösterilen avatar, kullanıcı adı, takipçi sayısı:

```
JWT token içinde temel bilgiler zaten var (kullanıcı adı, rol).
Ek profil bilgisi → IMemoryCache ile 15 dk cache.
Her profil güncellemesinde cache invalidate edilir.

Sık değişmeyen veri + yüksek okuma oranı = cache için ideal.
```

---

### Kimlik Doğrulama ve Yetkilendirme

**JWT public key doğrulama**

Microservice mimarisinde her servis gelen JWT token'ı doğrulamak için imza anahtarını bilmelidir. Public key'i her istekte kimlik sağlayıcısından (Keycloak, Auth0) almak:

```
İstek başına: 1 HTTP çağrısı (JWKS endpoint) = 50ms ek gecikme
1000 istek/sn = saniyede 1000 ek HTTP çağrısı → kimlik sağlayıcı çöker.

Çözüm: Public key'i 1 saat cache'le.
Key değişirse (rotation) yeni token doğrulanamaz → o anda cache'i yenile.
```

**İzin/rol kontrolü (Permission Cache)**

```csharp
// Kullanıcının izinleri DB'de, her istek başında sorgulanırsa:
// 100 endpoint × 1000 kullanıcı = her saniye binlerce "izin var mı?" sorgusu

// Çözüm: kullanıcı izinlerini Redis'e cache'le (5-15 dk)
// Rol değişince cache invalidate et (event-driven veya TTL'e güven)
var izinler = await _cache.GetOrCreateAsync(
    $"kullanici:{kullaniciId}:izinler",
    entry => { entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(10)); return _izinRepo.GetirAsync(kullaniciId); });
```

---

### Harici API Entegrasyonları

**Ödeme gateway token'ı**

Stripe, İyzico, PayTR gibi ödeme sistemleri ile çalışırken API erişim token'ı saatlik sürer. Her işlem öncesinde token almak yerine:

```csharp
// Token 1 saat geçerli, 50 dakika cache'le (yenileme payı bırak)
var token = await _cache.GetOrCreateAsync("odeme:api-token", async entry =>
{
    entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(50));
    return await _odemeServisi.TokenAl(); // harici API çağrısı
});
```

**Hava durumu / konum verileri**

Hava durumu 15 dakikada bir değişir. Binlerce kullanıcı İstanbul hava durumunu görüntülerken:

```
Cache YOK → her kullanıcı için OpenWeather API çağrısı → rate limit ($$$)
Cache VAR → 15 dk'da bir 1 API çağrısı → tüm kullanıcılara aynı veri

Aynı şehir için farklı kullanıcı isteklerini tek cache entry'de birleştirmek
"shared cache" örneğidir — kullanıcı başına değil, şehir başına cache.
```

**Harita ve coğrafi kodlama (Geocoding)**

"Kadıköy, İstanbul" → koordinat dönüşümü. Google Maps API: istek başına ücret.

```
Adres cache'i: "kadıköy istanbul" → {lat: 40.99, lng: 29.02}
TTL: 30 gün (adres koordinatları değişmez)
Tasarruf: aynı adres için tekrarlayan aramada API ücreti ödemezsin.
```

---

### Konfigürasyon ve Özellik Bayrakları (Feature Flags)

**Uygulama ayarları**

Admin panelinden değiştirilen ayarlar (maksimum sipariş limiti, komisyon oranı, dil seçenekleri) her istekte DB'den okunmamalı:

```csharp
// Ayarlar 5 dk cache'lenir. Admin değişiklik yapınca cache temizlenir.
var ayarlar = await _cache.GetOrCreateAsync("uygulama:ayarlar",
    entry => { entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5)); return _ayarRepo.TumAyarlariGetirAsync(); });
```

**Feature flag'ler**

"Ödeme sayfasında yeni tasarımı %20 kullanıcıya aç" gibi A/B test ayarları:

```
LaunchDarkly / Azure App Configuration → flag değerleri Redis'te cache'lenir.
Her istekte flag servisi sorgulanmaz — 30 sn cache, yeterince güncel.
Flag değişince push notification ile cache invalidate edilir.
```

---

### İçerik Yönetimi (CMS)

**Ana sayfa banner ve duyurular**

Haber siteleri, blog platformları: aynı içerik tüm ziyaretçilere gösterilir.

```
Output Cache için ideal: yanıtın tamamı cache'lenir.
Controller ve view render işlemi çalışmaz bile.

[OutputCache(Duration = 300)] // 5 dakika
public IActionResult AnaSayfa() { ... }

Bir makale yayınlanınca: EvictByTagAsync("icerik") ile toplu temizleme.
```

**Statik referans verileri**

İl/ilçe listesi, ülke kodları, para birimleri, banka listesi — yılda bir güncellenir:

```csharp
// TTL: 24 saat veya uygulama yeniden başlayana kadar (NeverRemove)
options.SetPriority(CacheItemPriority.NeverRemove);
options.SetAbsoluteExpiration(TimeSpan.FromHours(24));
```

---

### Raporlama ve Analitik

**Dashboard ve ağır sorgular**

"Bu ayki toplam satış rakamı" gibi sorgular milyonlarca satırı tarar, 30 saniye sürebilir.

```
Strateji: pre-computation + cache
- Arka plan job'u her 15 dakikada bir raporu hesaplar
- Sonucu Redis/bellek cache'e yazar
- Dashboard her zaman cache'ten okur (anlık sonuç değil)

Kullanıcı "Yenile" düğmesine basarsa → cache bypass + yeniden hesapla
```

**Sayfalı sorgu sonuçları**

"2. sayfa, 20 öğe" gibi sayfalı listeler:

```csharp
// Key: sayfa numarası + sıralama + filtreler dahil edilmeli
var key = $"kitaplar:sayfa:{sayfa}:sirala:{siralamaAlani}:yon:{yon}";
// Her farklı kombinasyon ayrı cache girdisi
```

---

### Microservice Mimarisi

**Servisler arası veri paylaşımı**

Sipariş servisi, kullanıcı bilgisine her sipariş işleminde ihtiyaç duyar. Kullanıcı servisini her seferinde çağırmak yerine:

```
Kullanıcı servisi → kullanıcı adı, email, adres
Sipariş servisi → bu bilgiyi 10 dk Redis'te cache'le
Her kullanıcı güncelleme event'inde → cache invalidate et (event-driven)
```

**API Gateway katmanı**

Kong, Nginx, YARP gibi gateway'ler:

```
Rate limiting sayaçları → Redis (dağıtık, tüm node'lar aynı sayacı görür)
Auth token doğrulama sonuçları → Redis (her microservice kendi doğrulamasını yapmaz)
Yanıt cache'i → sık istenen endpoint'lerin yanıtlarını cache'le
```

---

### Özet: Cache Olmadan Çalışmaz mı, Yavaş Çalışır mı?

```
Kategori                   | Cache Olmadan Sonuç
─────────────────────────────────────────────────────────────────
Ürün kataloğu (yüksek trafik)  | DB çöker — sistem durur
Döviz kuru (rate limit)        | API kotası aşılır — veri gelmiyor
JWT public key                 | Kimlik sağlayıcı çöker — giriş yapılamıyor  
Liderlik tablosu               | Agregasyon sorgular yavaşlar — kullanıcı bekler
İl/ilçe listesi                | Çalışır ama gereksiz DB trafiği — para israfı
Admin ayarları                 | Her istekte DB — ölçeklenemez
Beğeni sayacı                  | Her write için row-level lock — DB bottleneck
Ağır rapor sorgusu             | Her kullanıcı 30 sn bekler — kullanılamaz UX
Hava durumu API                | Dakikada binlerce ücretli çağrı — maliyet patlar
```

---

## 14. Kontrol Soruları

1. `IMemoryCache` ile `IDistributedCache` arasındaki temel fark nedir? Hangisini ne zaman tercih edersin?

2. Absolute expiration ve sliding expiration farkı nedir? İkisini birlikte kullanmanın amacı nedir?

3. Cache-aside pattern nedir? "Yaz" operasyonunda cache nasıl yönetilmeli — write-through mu yoksa invalidation mu?

4. Cache stampede (thundering herd) problemi nedir? Nasıl engellenir?

5. Output Cache ile `IMemoryCache`'in farkı nedir? Output Cache'in "Vary" özelliği ne işe yarar?

6. "Kitap listesi sayfası" için cache stratejisi tasarla: TTL ne olmalı, invalidation ne zaman tetiklenmeli, hangi cache tipi seçilmeli?
