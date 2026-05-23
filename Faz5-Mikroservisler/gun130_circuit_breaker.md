# Gün 130 — Circuit Breaker Pattern




---

## Önce Problemi Hisset

Saat 03:17. ProductService'in DB bağlantısı koptu. Servis yanıt vermeyi bıraktı.

```
Müşteri 1 sipariş veriyor
  → OrderService, ProductService'e HTTP isteği gönderiyor
  → ProductService cevap vermiyor... 5 sn... 10 sn... timeout
  → Müşteri 1 hata aldı

Müşteri 2 sipariş veriyor — aynı şey, thread bekliyor
Müşteri 3, 4, 5 ... hepsi bekliyor

OrderService'in thread havuzunda 200 thread var.
200 thread'in hepsi ProductService'in cevap vermesini bekliyor.
201. müşteri sipariş verdi — thread yok, beklemede.

OrderService artık hiçbir isteğe yanıt veremez durumda.

ApiGateway OrderService'e soruyor: "Sağ mısın?"
OrderService cevap veremiyor — thread havuzu dolu.
ApiGateway de cevap veremiyor.

Müşteriler ana sayfayı bile açamıyor.
```

**ProductService'in DB bağlantısı koptu → Tüm platform çöktü.**

Buna **cascade failure** (zincirleme çöküş) denir.  
Bir servisteki küçük arıza, birbirine bağlı tüm servisleri etkiler.

Ve gerçek şu ki: mikroservis mimarisinde cascade failure, **kaçınılmazdır** — önlenecek değil, **yönetilecek** bir risktir. Servisler düşer. Ağ kesilir. DB yavaşlar. Bunlar olacak. Soru şu: **olduğunda ne yapacaksın?**

---

## Gerçek Hayat Analojisi

### Sigorta Kutusu

Evinin elektrik tesisatını düşün.

Mutfakta bir cihaz kısa devre yaptı. Anında çok yüksek akım çekiyor.

Sigorta kutusu yoksa:
```
Kısa devre → aşırı akım → kablolar ısınıyor → duvar yanıyor → ev yanıyor
```

Sigorta kutusu varsa:
```
Kısa devre → sigorta açılır → sadece o devre kesilir
Salon, yatak odası, banyo etkilenmez.
Kablo yanmaz, ev yanmaz.
```

15 dakika sonra ustayı çağırdın, sorunu çözdün.  
Sigortayı kapattın. Mutfak normale döndü.

**Sigortanın yaptığı şey:**
1. Sorun var → devreyi kes (Open)
2. Bir süre bekle
3. Sorun geçti mi? Test et (Half-Open)
4. Geçtiyse → devreyi kapat (Closed)
5. Geçmediyse → tekrar kes (Open)

İşte Circuit Breaker budur. HTTP istekleri için elektrik sigortası.

---

## Teknik Açıklama

### Üç Durum (State Machine)

Circuit Breaker, her zaman üç durumdan birinde bulunur:

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│   [CLOSED]  ──── hata eşiği aşıldı ────►  [OPEN]          │
│      ▲                                       │              │
│      │                              bekleme süresi doldu   │
│      │                                       │              │
│   test başarılı ◄──── [HALF-OPEN] ◄──────────             │
│                             │                               │
│                        test başarısız                       │
│                             │                               │
│                          [OPEN] (timer sıfırlandı)          │
└─────────────────────────────────────────────────────────────┘
```

---

### CLOSED — Normal Hayat

Her şey yolunda. İstekler geçer.

Ama arka planda bir sayaç çalışıyor.

```
Son 30 saniyede kaç istek geldi?     → 50
Bunların kaçı hata verdi?            → 4
Hata oranı = 4/50 = %8

Eşik %50 → henüz açılmıyor, izlemeye devam
```

Dikkat: Sayaç her istek için değil, **son N saniyedeki pencere** için hesaplanır. Buna **sliding window** denir.

---

### OPEN — Devre Açık

Hata oranı eşiği aştı. Devre açıldı.

Artık ProductService'e tek bir istek bile **gitmez**.

İstek gelir gelmez — ProductService'e ulaşmadan — `BrokenCircuitException` fırlatılır. Bu milisaniyeler içinde olur. Thread havuzu dolmaz.

```
Müşteri 201 sipariş veriyor
  → Circuit Breaker: "Devre açık, ProductService'e gitme"
  → Anında hata → OrderService: "503 - Stok servisi geçici olarak kapalı"
  → Müşteri mesajı görüyor, thread serbest bırakıldı
  → 0 ms bekleme
```

Bu sayede OrderService hayatta kalır. Sadece stok kontrolü çalışmıyor.

---

### HALF-OPEN — Iyileşti mi?

OPEN süresinde bir timer çalışıyor. Diyelim 30 saniye.

30 saniye doldu. Circuit Breaker şunu düşünüyor:
> "ProductService'in DB bağlantısı düzeldi mi? Bilmiyorum. Bir test yapayım."

HALF-OPEN'a geçiyor.

Yapılandırmana göre **1 veya birkaç istek** ProductService'e geçirilir.

```
Test isteği → ProductService'e gidiyor
  ┌─ Başarılı → Circuit CLOSED, normal hayata dön
  └─ Başarısız → Circuit OPEN, 30 saniye daha bekle
```

Bu akıllıca bir mekanizma. "30 saniyede bir ben kontrol ederim, siz rahatsız olmayın" diyor.

---

### Hangi Hatalar Sayılır?

Önemli bir nüans: **Her hata circuit breaker'ı tetiklemez.**

```
✅ Sayılan hatalar (gerçek arıza işareti):
   - HTTP 500 (Internal Server Error) — sunucu çöktü
   - HTTP 503 (Service Unavailable) — servis kapalı
   - Timeout — servis yanıt vermiyor
   - HttpRequestException — ağ bağlantısı yok

❌ Sayılmayan hatalar (iş mantığı hatası):
   - HTTP 404 (Not Found) — ürün bulunamadı, servis sağlıklı
   - HTTP 400 (Bad Request) — yanlış veri gönderdik, servis sağlıklı
   - HTTP 401/403 — auth hatası, servis sağlıklı
```

ProductService sağlıklı ama ürün yok → devre açılmaz.  
ProductService çöktü → devre açılır.

Neden fark önemli? 404 için devre açılırsa, "stokta olmayan bir ürünü soran kullanıcı" yüzünden tüm stok kontrolü devre dışı kalır. Bu felaket olur.

---

### MinimumThroughput Neden Var?

Diyelim servis yeni başladı. Henüz 2 istek geldi, ikisi de hata.

Hata oranı = 2/2 = **%100**

Circuit breaker açılmalı mı?

**Hayır.** 2 istek, karar vermek için çok az. Belki sadece startup gecikmesi.

`MinimumThroughput = 5` dersen:
- 5 istek gelmeden karar verilmez
- 5 istek geldi, 4'ü hata → hata oranı %80 → karar verilir

Bu küçük parametre, "false positive"leri (gereksiz devre açılmasını) büyük ölçüde azaltır.

---

## Faz3 ile Karşılaştırma

```csharp
// ─── Faz3: Monolith ───────────────────────────────────────────────────────
public class OrderService
{
    private readonly IProductRepository _products;
    // Aynı process, aynı bellek alanı, aynı DB bağlantısı

    public async Task CreateOrderAsync(CreateOrderDto dto)
    {
        var product = await _products.GetByIdAsync(dto.ProductId);
        // GetByIdAsync hata verirse → Exception → sadece bu istek patlar
        // Diğer 199 istek normal çalışmaya devam eder
        // Thread o kadar beklemez — ya çalışır ya da hata verir
    }
}

// ─── Faz5: Mikroservis — Circuit Breaker olmadan ──────────────────────────
public class OrderService
{
    private readonly HttpClient _httpClient;
    // Farklı process, farklı sunucu, ağ üzerinden

    public async Task CreateOrderAsync(CreateOrderDto dto)
    {
        var response = await _httpClient.GetAsync($"api/products/{dto.ProductId}");
        // ProductService yavaşladı → bu await burada 10 sn bekler
        // 100 eş zamanlı istek → 100 thread burada bekler
        // Thread havuzu dolar → tüm sistem yanıt veremez
    }
}

// ─── Faz5: Mikroservis — Circuit Breaker ile ──────────────────────────────
public class OrderService
{
    private readonly IProductHttpClient _productClient;
    // Aynı HTTP çağrısı, ama Circuit Breaker sarmalı

    public async Task CreateOrderAsync(CreateOrderDto dto)
    {
        // CLOSED: normal HTTP gider
        // OPEN: buraya bile gelmez, anında BrokenCircuitException
        var product = await _productClient.GetProductAsync(dto.ProductId);
    }
}
```

**Temel fark:**  
Monolith'te bir bileşen yavaşladığında diğerleri izole edilmiş.  
Mikroserviste ağ üzerinden konuşuyorsun — yavaş servis tüm thread havuzunu tüketebilir.  
Circuit Breaker, monolithin doğal izolasyonunu ağ katmanına taşır.

---

## 500 vs 50.000 Kullanıcı

| Durum | 500 Kullanıcı | 50.000 Kullanıcı |
|-------|--------------|-----------------|
| ProductService 10 sn yavaşladı | 20-30 thread bekler, sistem yavaşlar | 500+ thread dolar, OrderService çöker, cascade failure |
| Circuit Breaker YOK | Tolere edilebilir, fark edilmeyebilir | Tüm platform 503, gerçek para kaybı |
| Circuit Breaker VAR | Etkisi yok | İstekler anında reddedilir, platform ayakta |
| ProductService düzeldi, recovery | Elle restart veya bekle | Half-Open ile **otomatik** iyileşme, sıfır müdahale |
| Monitoring | "Neden yavaş?" sorusu | OnOpened/OnClosed event'leri → alert → anında müdahale |

---

## Kod

### Konfigürasyon

```csharp
// OrderService/Program.cs
builder.Services.AddHttpClient<IProductHttpClient, ProductHttpClient>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["ProductService__BaseUrl"] ?? "http://product-service:8080/");
        client.Timeout = TimeSpan.FromSeconds(10);
        // bunu yazmasaydık: default timeout 100 sn — thread o kadar bekler
    })
    .AddResilienceHandler("product-pipeline", pipeline =>
    {
        // Önce timeout, sonra circuit breaker — sıra önemli
        // Timeout, circuit breaker'ın "hata" sayabilmesi için önce gelir

        pipeline.AddTimeout(TimeSpan.FromSeconds(5));
        // bunu yazmasaydık: 10 sn timeout var ama CB bunu "hata" saymaz — kendi timer'ı işler

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration  = TimeSpan.FromSeconds(30),
            // Son 30 sn içindeki isteklere bak — bunu yazmasaydık: hangi pencere? belirsiz

            MinimumThroughput = 5,
            // 5 istek gelmeden karar verme — bunu yazmasaydık: 1 hata = devre açılır, çok hassas

            FailureRatio      = 0.5,
            // %50 hata oranı → devre açılır — bunu yazmasaydık: 1 hata bile devreyi açar

            BreakDuration     = TimeSpan.FromSeconds(30),
            // 30 sn OPEN, sonra Half-Open — bunu yazmasaydık: devre bir daha kapanmaz

            OnOpened     = args => { Console.WriteLine($"🔴 CB AÇILDI — {args.BreakDuration.TotalSeconds}sn bekleniyor"); return default; },
            OnClosed     = args => { Console.WriteLine("🟢 CB KAPANDI — Servis geri döndü");                             return default; },
            OnHalfOpened = args => { Console.WriteLine("🟡 CB YARI AÇIK — Test isteği gönderiliyor...");                 return default; }
        });
    });
```

### Controller — Graceful Hata Yönetimi

```csharp
// OrderService/Controllers/OrderController.cs
[HttpPost]
public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
{
    ProductInfo? product;
    try
    {
        product = await _productClient.GetProductAsync(request.ProductId);
        // OPEN durumda: bu satıra ulaşmadan BrokenCircuitException fırlatılır
        // bunu yazmasaydık: try/catch olmadan 500 döner, kullanıcı "Sistem hatası" görür
    }
    catch (BrokenCircuitException)
    {
        // Devre açık: ProductService şu an erişilemez, 0 ms bekleme oldu
        return StatusCode(503, new
        {
            Message           = "Stok kontrolü geçici olarak kullanılamıyor. Lütfen 30 saniye sonra tekrar deneyin.",
            RetryAfterSeconds = 30
            // bunu yazmasaydık: kullanıcı neden hata aldığını bilemez
        });
    }

    if (product is null)   return NotFound(new { Message = "Ürün bulunamadı." });
    if (!product.InStock)  return BadRequest(new { Message = $"'{product.Name}' stokta bulunmuyor." });

    // Stok var → normal sipariş akışı devam eder (Outbox + Saga)
    // ...
}
```

---

## Kontrol Soruları

1. `MinimumThroughput = 5` ve `FailureRatio = 0.5` ile şu senaryo gerçekleşti:  
   30 saniyede 6 istek geldi, 3'ü hata verdi. Devre açıldı mı? Neden?

2. ProductService HTTP 404 döndürüyor (ürün bulunamadı). Bu Circuit Breaker sayacını artırır mı?  
   Artırmaması için ne yapman gerekir?

3. HALF-OPEN'da test isteği başarısız oldu. Bir sonraki HALF-OPEN ne zaman gerçekleşir?  
   Bu süreç sonsuza kadar devam edebilir mi?

4. Circuit Breaker OPEN durumdayken sipariş almayı durdurmak yerine  
   "stok kontrolü sonraya ertele, siparişi al" kararı verseydin hangi pattern'i kullanırdın?  
   Hint: Gün 127'de öğrendik...

5. Aynı servis hem RabbitMQ consumer hem HTTP endpoint sunuyor.  
   Circuit Breaker sadece HTTP katmanını koruyor. RabbitMQ consumer için ne kullanılır?
