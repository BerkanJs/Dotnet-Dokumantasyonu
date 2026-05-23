# Gün 133 — Fallback ve Graceful Degradation

---

## Önce Problemi Hisset

Gün 130'dan beri Circuit Breaker kurduk. ProductService çöktüğünde devre açılıyor ve kullanıcıya 503 döndürüyoruz. Teknik olarak doğru ama iş sonucu açısından sert.

Gerçek şu: ProductService'in çöktüğü o 30 saniyede müşteri hâlâ ürüne bakabiliyor olmalı. Hâlâ alışveriş yapabilmeli. Belki stok bilgisi biraz eski olabilir — "5 dakika önce 45 adet vardı" demek, "sistem kapalı" demekten çok daha iyidir.

```
ProductService çöktü

Şu an olan:
  Müşteri: "Clean Code kitabı stokta var mı?"
  Sistem: 503 Service Unavailable
  Müşteri: Sayfayı kapattı

Olması gereken:
  Müşteri: "Clean Code kitabı stokta var mı?"
  Sistem: "Evet, stokta görünüyor (5 dk önce kontrol edildi)"
  Müşteri: Siparişi verdi
```

İkinci senaryo nasıl mümkün? Fallback ile. Asıl kaynaktan veri alamayınca alternatif bir kaynağa, alternatif bir yanıta ya da daha azına razı olmakla.

---

## Gerçek Hayat Analojileri

### Fallback — Bakkaldaki Kağıt Fiyat Listesi

Bakkalın bilgisayarı bozuldu. POS sistemi çalışmıyor. Ne yapıyor bakkal?

Çekmeceyi açıyor, kağıda yazılmış fiyat listesini çıkarıyor. Belki dünden bu yana bazı fiyatlar değişmiştir ama büyük fark yok. Müşteriye hizmet verebiliyor. Alışveriş devam ediyor.

**Kağıt fiyat listesi = cache'deki eski veri = fallback.**

Hiç fiyat listesi olmayan bakkal ne yapar? "Bugün satış yapamıyoruz" der ve kapıyı kapar. Rakibine müşteri göndermiş olur.

---

### Graceful Degradation — Motoru Çalışan Araba

Arabanda sol arka tekerlek patladı. Ne yaparsın?

Hayatın sona ermiyor. Dur, lastiği değiştir ya da yedek lastiğinle sür — yavaş, dikkatli ama sürüyorsun. Hedefe ulaşıyorsun. Dört tekerlek de yokken mükemmel gidişi bırak, şu an yeterli olan "gidebilmek".

Graceful degradation tam budur. Sistem bir parçası bozulduğunda "hiçbir şey çalışmıyor" değil, "azaltılmış kapasiteyle çalışıyoruz" moduna geçmek.

Netflix'i düşün: kişiselleştirme servisi çöktüğünde ne oluyor? Sana özel öneri listesi yüklenmiyor. Ama Netflix hâlâ çalışıyor. "Bu hafta çok izlenenler" gibi genel bir liste gösteriyor. İzleme deneyimi tam değil ama tamamen mahvolmuş da değil.

---

### Feature Flag — Işık Anahtarı

Ev tadilattayken tüm elektriği kesmiyorsun. Sadece o odanın sigortasını indiriyorsun.

Feature flag da aynı. "Bu özelliği şu an devre dışı bırak" diyebiliyorsun, kod değişikliği yapmadan, yeniden dağıtım yapmadan. Sadece bir konfigürasyon değişikliği.

Circuit Breaker otomatik kesiyor: "hata eşiğini aştın, devre açıldı." Feature flag manüel kesiyor: "biz bu özelliği şimdilik kapamak istiyoruz." Birincisi yangın söndürücü, ikincisi planlı bakım anahtarı.

---

## Teknik Açıklama

### Üç Farklı Fallback Stratejisi

Fallback deyince tek bir çözüm yok. Senaryoya göre üç farklı yaklaşım var.

**1. Cache-based fallback:** ProductService'ten en son başarılı yanıtı önbellekte tutarsın. Servis düştüğünde önbellekteki eski veriyi dönersin. Veri biraz bayat olabilir ama hiç olmamaktan iyidir. Buna **stale data serving** denir — bayat ama var olan veri sunmak.

Ne zaman makul? Stok bilgisi için kabul edilebilir: "5 dakika önce 45 adet vardı" doğru olmayabilir ama büyük ihtimalle hâlâ bir şeyler var. Ne zaman tehlikeli? Fiyat bilgisi için daha dikkatli olmak gerekir — fiyat değişmişse müşteriye yanlış bilgi göstermek sorun yaratabilir.

**2. Default response fallback:** Cache'de de veri yoksa ya da hiç cache kurmadıysan, makul bir varsayılan yanıt dönersin. "Stok durumu şu an doğrulanamıyor, sipariş vermek isteyebilirsiniz" gibi. Müşteriye bilgi veriyorsun ama boş bırakmıyorsun.

**3. Partial response / graceful degradation:** Sayfanın bir kısmı çalışmıyor ama tamamını kapatmıyorsun. Ürün listesi yükleniyor, stok göstergesi yüklenmiyor. Kullanıcı ürünleri görebiliyor, sadece stok göstergesi "—" ya da "Kontrol ediliyor" diyor.

---

### Stale Data — Ne Kadar Eskimiş Veri Kabul Edilebilir?

Bu sorunun cevabı iş gereksinimine bağlı, teknik bir karar değil.

"Son 5 dakika içindeki ürün bilgisi" genellikle güvenli. Ürün adı, fiyatı 5 dakikada değişmez. Stok 5 dakikada ±1-2 adet değişebilir ama sıfıra da düşmemiş olabilir.

"Son 24 saatteki ürün bilgisi" riskli olabilir. Fiyat kampanyası başlamış olabilir, ürün kaldırılmış olabilir.

Kural: Cache TTL (Time-To-Live) değerini, o verinin ne kadar hızlı değiştiğine göre ayarla. Hızlı değişen veri = kısa TTL. Yavaş değişen veri = uzun TTL.

---

### Feature Flag Ne Zaman Kullanılır?

Circuit Breaker iyi ama tamamen otomatik. Bazen bir özelliği kasıtlı olarak, kontrollü biçimde kapatmak istiyorsun.

Senaryolar:

- **Yeni özellik dağıtımı:** "Gerçek zamanlı stok kontrolü" özelliğini yeni açtın. Bir sorun çıkarsa hızla kapatabilmek istiyorsun. Feature flag ile: konfigürasyondan `EnableRealTimeStockCheck = false` yaparsın, sistem anında eski davranışa döner, yeni deployment gerekmez.

- **Planlı bakım:** ProductService bakımda. Müşterilere "stok bilgisi geçici olarak güncellenmeyecek" mesajı göstermek istiyorsun. CB'yi elle açamazsın ama feature flag ile stok kontrolünü bypass edip cached veri döndürebilirsin.

- **A/B testi:** Kullanıcıların %10'una yeni stok gösterimi, %90'ına eskisi. Feature flag bunu da yönetir.

---

### Fallback vs Circuit Breaker — Fark Ne?

Circuit Breaker şunu yapar: "Servis çöktü, artık ona istek gönderme."  
Fallback şunu yapar: "Servis çöktü, o servis yerine şunu sun."

Birbirini tamamlıyorlar. CB kapıyı kapatıyor, fallback alternatif kapıyı açıyor. CB olmadan fallback kurulabilir ama o zaman her başarısız istekte fallback'e düşersin — performans açısından verimsiz. CB + fallback birlikte: CB tetiklendiğinde fallback devreye girer, gereksiz HTTP denemeleri olmaz.

---

## Faz3 ile Karşılaştırma

Faz3 monolith'te fallback genellikle null check veya varsayılan değer döndürmek kadar basittir. Bir repository null dönerse varsayılan bir nesne dönersin. Servis çalışmıyorsa diye ayrı bir cache katmanı kurman gerekmez — zaten aynı process, aynı DB.

Faz5'te farklı. ProductService bağımsız bir servis, istediği zaman düşebilir. Düştüğünde OrderService'in "hiçbir şey yapamıyorum" demesi iş kaybıdır. Fallback katmanı, bu bağımsızlığın getirdiği kırılganlığa karşı bir güvence.

```csharp
// Faz3: Null kontrolü yeter
var product = await _repository.GetByIdAsync(id);
return product ?? ProductDto.Default;

// Faz5: Önce cache'e bak, yoksa servise git, o da olmadıysa varsayılan dön
var cached = await _cache.GetAsync<ProductInfo>($"product:{id}");
if (cached is not null) return cached;

try
{
    var product = await _productClient.GetProductAsync(id);
    await _cache.SetAsync($"product:{id}", product, TimeSpan.FromMinutes(5));
    return product;
}
catch (BrokenCircuitException)
{
    // CB açık: cache'de bile yoksa varsayılan dön
    return ProductInfo.Unknown(id);
}
```

---

## 500 vs 50.000 Kullanıcı

| Durum | 500 Kullanıcı | 50.000 Kullanıcı |
|-------|--------------|-----------------|
| ProductService düştü, fallback YOK | Birkaç müşteri 503 görür | Tüm ürün sayfaları çalışmıyor, topluca çıkış |
| Cache-based fallback | Eski veri gösteriliyor, fark edilmez | 50.000 kullanıcı alışverişe devam ediyor |
| Cache TTL 1 saat, fiyat değişti | Eski fiyat gösteriliyor, küçük sorun | Binlerce müşteriye yanlış fiyat → büyük sorun |
| Feature flag ile stok kontrolü kapatıldı | Kullanıcılar uyarı görüyor | Sipariş akışı devam ediyor, stok sonradan doğrulanıyor |
| Graceful degradation | Stok göstergesi "—" ama sayfa açık | Kullanıcı deneyimi bozulmadı, satış devam etti |

---

## Kod

### Cache-Based Fallback

```csharp
// OrderService/HttpClients/ProductHttpClient.cs — fallback eklendi
public class ProductHttpClient : IProductHttpClient
{
    private readonly HttpClient         _httpClient;
    private readonly IMemoryCache       _cache;
    private readonly ILogger<ProductHttpClient> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    // 5 dakika: fiyat ve stok için makul; daha uzun = bayat veri riski
    // bunu yazmasaydık: her fallback'te önbelleksiz veri yoktur, 503 kaçınılmaz

    public ProductHttpClient(HttpClient httpClient, IMemoryCache cache,
                             ILogger<ProductHttpClient> logger)
    {
        _httpClient = httpClient;
        _cache      = cache;
        _logger     = logger;
    }

    public async Task<ProductInfo?> GetProductAsync(Guid productId)
    {
        var cacheKey = $"product:{productId}";

        try
        {
            var response = await _httpClient.GetAsync($"api/products/{productId}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _cache.Remove(cacheKey); // Ürün silindi, cache'den de çıkar
                return null;
            }

            response.EnsureSuccessStatusCode();
            var product = await response.Content.ReadFromJsonAsync<ProductInfo>();

            if (product is not null)
                _cache.Set(cacheKey, product, CacheTtl);
            // Başarılı yanıtı cache'e yaz — sonraki fallback için hazır
            // bunu yazmasaydık: fallback'te cache boş, varsayılan dönmek zorunda kalırız

            return product;
        }
        catch (Exception ex) when (ex is BrokenCircuitException or RateLimiterRejectedException
                                      or TimeoutRejectedException or HttpRequestException)
        {
            _logger.LogWarning("⚠️ ProductService erişilemiyor ({Type}), cache kontrol ediliyor...",
                               ex.GetType().Name);

            // Fallback 1: Cache'de var mı?
            if (_cache.TryGetValue(cacheKey, out ProductInfo? cached))
            {
                _logger.LogInformation("📦 Cache fallback — ürün bilgisi önbellekten döndürüldü: {Id}", productId);
                return cached;
                // bunu yazmasaydık: exception'ı fırlatırdık → controller 503 dönerdi
            }

            // Fallback 2: Cache'de de yok — varsayılan yanıt
            _logger.LogWarning("❓ Cache'de de yok — varsayılan yanıt döndürülüyor: {Id}", productId);
            return ProductInfo.Unknown(productId);
            // bunu yazmasaydık: null dönerdik → controller NotFound dönerdi → müşteri ürünü göremez
        }
    }
}
```

### ProductInfo.Unknown — Varsayılan Yanıt

```csharp
// OrderService/HttpClients/IProductHttpClient.cs — Unknown factory metodu eklendi
public record ProductInfo(
    Guid    ProductId,
    string  Name,
    int     Stock,
    decimal Price,
    bool    InStock,
    bool    IsStockVerified = true  // false → stok bilgisi doğrulanamadı
);

public static class ProductInfoExtensions
{
    public static ProductInfo Unknown(Guid productId) => new(
        ProductId:        productId,
        Name:             "Ürün",
        Stock:            0,
        Price:            0,
        InStock:          false,
        IsStockVerified:  false
        // IsStockVerified = false → controller "stok doğrulanamadı" mesajı gösterebilir
        // bunu yazmasaydık: "stokta yok" dönerdik — belki stokta var ama bilemiyoruz
    );
}
```

### Feature Flag ile Stok Kontrolü

```csharp
// OrderService/Controllers/OrderController.cs — feature flag kontrolü
[HttpPost]
public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
{
    var enableStockCheck = _configuration.GetValue<bool>("Features:RealTimeStockCheck", true);
    // appsettings.json veya env variable'dan okunur
    // Bakım modunda: Features__RealTimeStockCheck=false → stok kontrolü atlanır
    // bunu yazmasaydık: stok kontrolünü kapatmak için yeni deployment gerekir

    ProductInfo? product = null;

    if (enableStockCheck)
    {
        try
        {
            product = await _productClient.GetProductAsync(request.ProductId);
        }
        catch (Exception ex) when (ex is BrokenCircuitException or RateLimiterRejectedException)
        {
            return StatusCode(503, new { Message = "Stok servisi geçici olarak kullanılamıyor." });
        }

        if (product is null)
            return NotFound(new { Message = "Ürün bulunamadı." });

        if (product.IsStockVerified && !product.InStock)
            return BadRequest(new { Message = $"'{product.Name}' stokta bulunmuyor." });
    }

    // Stok doğrulanamadı veya stok kontrolü kapalı → siparişi yine de al, async doğrula
    // ...sipariş oluşturma devam eder
}
```

---

## Kontrol Soruları

1. Cache TTL'ini 1 saat ayarladın. ProductService 45 dakika boyunca bir ürünün fiyatını güncelledi.  
   Bu sürede kaç müşteri eski fiyatı görür? Bu iş açısından kabul edilebilir mi?

2. `ProductInfo.Unknown` döndürdün, `IsStockVerified = false`.  
   Sipariş oluşturdun, ama ürün gerçekten stokta yoktu.  
   Bu durumu nasıl düzeltirsin? Hangi pattern devreye girer? (İpucu: Gün 128)

3. Feature flag ile stok kontrolünü kapattın.  
   Hangi kullanıcılar bu değişiklikten anında etkilenir?  
   Konfigürasyon değişikliği için servisin restart'a ihtiyacı var mı?

4. Cache fallback ile Circuit Breaker birlikte çalışıyor.  
   CB OPEN olduğunda her istek anında exception alıyor.  
   Cache doluysa kaç milisaniyede yanıt verilir?  
   Cache boşsa ne olur?

5. Graceful degradation ile "stok bilinmiyor" siparişi aldın.  
   Stok gerçekten yoksa ve siparişi iptal etmek zorunda kalırsan hangi pattern devreye girer?  
   (İpucu: Gün 128-129)
