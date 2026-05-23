# Gün 132 — Bulkhead Pattern

---

## Önce Problemi Hisset

OrderService üç farklı servisle konuşuyor: ProductService, PaymentService ve ShipmentService.

Bu servislerin hepsi bağımsız çalışıyor, hepsi sağlıklı. Ama tek bir gün, ProductService'in veritabanı sorgularında beklenmedik bir yavaşlama başladı. Her istek 8-10 saniye sürüyor. Hata vermiyor, yanıt veriyor — sadece yavaş.

```
OrderService'in toplam thread havuzu: 200 thread

Saat 10:00 — ProductService yavaşladı
  İstek 1   → ProductService bekliyor (8sn)... thread meşgul
  İstek 2   → ProductService bekliyor (8sn)... thread meşgul
  İstek 3   → ProductService bekliyor (8sn)... thread meşgul
  ...
  İstek 200 → Thread havuzu tükendi

Saat 10:01 — PaymentService ve ShipmentService tamamen sağlıklı
  Ödeme isteği geldi → thread yok → beklemede
  Kargo sorgusa geldi → thread yok → beklemede
  Sağlıklı servisler de yanıt veremez hale geldi
```

**ProductService yavaşladı → OrderService'teki tüm thread'leri tüketti → PaymentService de ShipmentService de yanıt veremez oldu.**

Oysa ne PaymentService ne ShipmentService'in bu olayda hiçbir suçu yok. İkisi de sağlıklı çalışıyor, ama ProductService'in yarattığı kaynak açlığından paylarını aldılar. Buna **noisy neighbor** (gürültülü komşu) problemi denir.

---

## Gerçek Hayat Analojisi

### Geminin Bölmeleri

"Bulkhead" kelimesi doğrudan gemi mühendisliğinden geliyor. Türkçesi: **su geçirmez bölme**.

Büyük bir kargo gemisinin gövdesi tek bir büyük boşluk değildir. Çelik bölmelerle onlarca küçük kompartımana ayrılmıştır. Her kompartıman diğerinden izole edilmiştir.

Gemi bir kayaya çarptı, burun kısmında bir delik açıldı. Su giriyor. Ama su sadece o kompartımanı dolduruyor. Arkadaki kompartımanlar kuru ve sızdırmaz. Gemi battı mı? Hayır. Yavaş, belki daha az manevra kabiliyetiyle, ama yüzüyor. Kargo korundu, mürettebat kurtarıldı.

Eğer o çelik bölmeler olmasaydı? Burun kısmına giren su tüm gövdeye yayılırdı. Gemi birkaç dakikada biterdi.

**Bulkhead pattern, servis mimarisinde tam olarak bu bölmeleri inşa ediyor.** ProductService için bir kaynak havuzu, PaymentService için başka bir havuz, ShipmentService için başka bir havuz. Biri sular altında kalırsa diğerleri kuru kalıyor.

---

### Hastane Acil Servisi

Büyük bir hastanenin acil servisini düşün. Travma bölümü, kardiyoloji bölümü, pediatri bölümü — hepsi ayrı ayrı organize edilmiş.

Bir trafik kazasından 15 yaralı geldi. Travma bölümü doldu taştı, doktorlar koşturuyor. Bu yoğunluk kardiyoloji bölümünü etkiliyor mu? Hayır. Kardiyoloji kendi doktorları ve odalarıyla normal çalışmaya devam ediyor.

Eğer tüm hastane tek büyük bir bölüm olsaydı, 15 travma vakası geldiğinde kalp krizi geçiren hasta kapıda beklerdi.

**Kaynak izolasyonu bu demek:** Her bağımlılık kendi kapasitesiyle sınırlı, başkasının kaynağını yiyemiyor.

---

## Teknik Açıklama

### Thread Isolation — Gerçek İzolasyon

Thread isolation'da her bağımlılık için **ayrı bir thread havuzu** açılır. ProductService için 20 thread, PaymentService için 20 thread, ShipmentService için 20 thread.

ProductService'in 20 thread'i tükenirse sıradaki istek hemen reddedilir — 21. thread başka havuzlardan ödünç alınamaz. PaymentService havuzunun 20 thread'ine tek bir etkisi olmaz.

Gerçek bir duvar bu. Fiziksel olarak ayrı thread havuzları.

Dezavantajı: thread'ler pahalı işletim sistemi kaynakları. 10 farklı servis için 10 ayrı havuz açmak ciddi bellek ve CPU maliyeti. Özellikle async/await ile yazılmış .NET kodunda thread'ler zaten bloke olmuyor — çoğunlukla sadece bir "slot" tutuyorlar. Dolayısıyla fiziksel thread izolasyonu .NET dünyasında tercih edilen yöntem değil.

---

### Semaphore Isolation — Pratik İzolasyon

Semaphore isolation'da thread havuzu ortak kalır ama her bağımlılığa eş zamanlı erişim için bir **limit** koyulur.

`SemaphoreSlim(10)` dersen: ProductService'e aynı anda en fazla 10 eş zamanlı istek gidebilir. 11. istek semaphore'a ulaşmaya çalışır, slot boş değil, ya bekler ya reddedilir.

Bu çok daha hafif bir mekanizma. Ayrı thread havuzu açmıyorsun, sadece bir sayaç tutuyorsun. 10 slot doluysa yeni istek girmez. Herhangi bir slot boşalınca sıradaki girer.

Async kod için bu ideal: `await semaphore.WaitAsync()` çağrısı thread'i bloke etmez, sadece o görevi askıya alır. Thread başka işlere koşabilir, görev slot boşalınca devam eder.

---

### Kaç Slot Vermeli?

Bu sorunun cevabı ölçümden geliyor, tahminden değil.

Şunu düşün: ProductService normalde 200ms'de yanıt veriyor. Saniyede 50 istek geliyor. Herhangi bir anda kaç eş zamanlı istek uçuşta olabilir? `50 istek/sn × 0.2sn latency = 10 eş zamanlı istek`. Yani 10-15 slot makul bir başlangıç noktası.

Ama ProductService yavaşladı, 2 saniyeye çıktı. Şimdi 50 istek/sn × 2sn = 100 eş zamanlı istek. Limit 10'sa 90 istek reddedilecek. Bu kötü mü? Hayır, bu tam istediğimiz şey: ProductService yavaşladığında sadece onun havuzu etkileniyor, rest tüm sisteme zarar veremiyor.

**Ölçüm yöntemi:** P99 latency × saniyedeki istek sayısı = gerekli slot sayısı. Buna %20-30 güvenlik payı ekle.

---

### Queue ile Rejection Arasındaki Fark

Concurrent limit aşıldığında iki seçenek var: beklemeye al (queue) ya da anında reddet.

Queue: "Şu an 10 slot dolu ama 5 kişi daha bekleyebilir." 11. istek sırada bekliyor, slot boşalınca giriyor. Bir nevi güvenlik tamponu.

Immediate rejection: "10 slot dolu, geri dön." Yeni istek anında `RateLimiterRejectedException` alıyor.

Queue güvenli görünüyor ama bir riski var: yavaş servislerde queue de dolabilir. Hem 10 eş zamanlı hem 5 kuyrukta bekliyor — hepsi 8 saniye bekliyorsa sonuçta 15 thread/slot meşgul. Anlık reddetmek, hata bütçeni koruman için daha öngörülebilir bir davranış.

Kural olarak: **latency'nin düşük olduğu servisler için queue mantıklı, yüksek latency'li ve yavaşlama eğilimli servisler için immediate rejection daha güvenli.**

---

## Faz3 ile Karşılaştırma

Faz3 monolith'te bulkhead ihtiyacı pratikte çok nadirdir. Çünkü tüm "bağımlılıklar" aynı process içinde yaşıyor. IProductRepository, IPaymentService, IShipmentService — hepsi aynı bellek alanında, aynı thread havuzunda.

Bir repository yavaş çalışsa bile genellikle veritabanı bağlantı havuzundan yavaşlıyor — bu EF Core seviyesinde yönetilen bir havuz. Bir başka modülün yavaşlaması diğerinin thread'ini doğrudan tüketmez.

Mikroserviste ise her bağımlılık bir HTTP çağrısı. Her HTTP çağrısı bir thread tutabilir. Farklı servislerin hepsi aynı thread havuzundan beslendiğinde biri yavaşlasa tümünü etkileyebilir. Bulkhead bu doğal izolasyonu yapay olarak ağ katmanına taşıyor.

```csharp
// Faz3: IProductRepository yavaşladı → sadece DB connection pool etkilenir
// Diğer repository'ler kendi DB bağlantılarını kullanır — bağımsız

// Faz5: ProductService HTTP yavaşladı → thread havuzu paylaşık
// Bulkhead olmadan: tüm servisler aynı thread havuzundan → birinin yavaşlığı hepsini etkiler
// Bulkhead ile: ProductService'in max 10 concurrent slot'u var → fazlası reddedilir
//              PaymentService'in kendi 10 slotu var → ProductService'ten etkilenmez
```

---

## 500 vs 50.000 Kullanıcı

| Durum | 500 Kullanıcı | 50.000 Kullanıcı |
|-------|--------------|-----------------|
| ProductService yavaşladı, bulkhead YOK | Sistem yavaşlar ama ayakta | Thread havuzu dolar, tüm servisler çöker |
| Bulkhead VAR, ProductService yavaşladı | Sadece sipariş sorgulama yavaş | Ödeme ve kargo hâlâ çalışıyor — kısmi degrade |
| Queue = 5, slot = 10 | Fark edilmez | Fazla istekler reddedilir, sistem stabil |
| Immediate rejection, slot = 10 | Bazı 503'ler görünür | Öngörülebilir davranış, cascade yok |
| Her servis kendi slotunda izole | Anlamsız overhead | Noisy neighbor problemi tamamen önlendi |

---

## Kod

### SemaphoreSlim ile Manuel Bulkhead

```csharp
// OrderService/HttpClients/ProductHttpClient.cs
public class ProductHttpClient : IProductHttpClient
{
    private static readonly SemaphoreSlim _semaphore = new(10, 10);
    // static: tüm instance'lar aynı semaphore'u paylaşır — gerçek bir limit bu
    // bunu yazmasaydık: her instance kendi semaphore'unu yaratır, limit işe yaramaz
    // 10: aynı anda en fazla 10 eş zamanlı istek ProductService'e gidebilir

    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductHttpClient> _logger;

    public async Task<ProductInfo?> GetProductAsync(Guid productId)
    {
        var acquired = await _semaphore.WaitAsync(TimeSpan.Zero);
        // TimeSpan.Zero → kuyrukta bekleme, anında red
        // bunu yazmasaydık: slot doluysa istek bekler → yavaş servis için bu queue de doluyor

        if (!acquired)
        {
            // Slot yok → anında reddet → thread serbest
            // bunu yazmasaydık: WaitAsync sonsuz bekler → thread bloke olur → amacın tersine döner
            _logger.LogWarning("⛔ Bulkhead doldu — ProductService slot yok, istek reddedildi");
            throw new InvalidOperationException("ProductService concurrency limit aşıldı.");
        }

        try
        {
            var response = await _httpClient.GetAsync($"api/products/{productId}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProductInfo>();
        }
        finally
        {
            _semaphore.Release();
            // bunu yazmasaydık: slot bir daha serbest bırakılmaz → 10 istek sonra sistem tamamen kilitlenir
        }
    }
}
```

### Polly Pipeline'a Ekleme (Gün 130-131-132 Tam Pipeline)

```csharp
// OrderService/Program.cs — güncellenmiş pipeline
.AddResilienceHandler("product-pipeline", pipeline =>
{
    // Sıra: CB → ConcurrencyLimiter → Retry → Timeout
    // ConcurrencyLimiter, CB'den sonra gelir:
    // CB OPEN → zaten reddedildi, limiter'a gerek yok
    // CB CLOSED → limiter devreye girer: slot var mı?

    // 1. Circuit Breaker
    pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        SamplingDuration  = TimeSpan.FromSeconds(30),
        MinimumThroughput = 5,
        FailureRatio      = 0.5,
        BreakDuration     = TimeSpan.FromSeconds(30),
        OnOpened     = args => { Console.WriteLine($"🔴 CB AÇILDI {args.BreakDuration.TotalSeconds}sn"); return default; },
        OnClosed     = args => { Console.WriteLine("🟢 CB KAPANDI");                                     return default; },
        OnHalfOpened = args => { Console.WriteLine("🟡 CB YARI AÇIK");                                  return default; }
    });

    // 2. Concurrency Limiter (Bulkhead) — Gün 132
    pipeline.AddConcurrencyLimiter(new ConcurrencyLimiterOptions
    {
        PermitLimit = 10,
        // Aynı anda en fazla 10 eş zamanlı istek ProductService'e gidebilir
        // bunu yazmasaydık: ProductService yavaşlayınca tüm thread'leri tüketebilir

        QueueLimit  = 0
        // Kuyruk yok — 11. istek anında reddedilir
        // bunu yazmasaydık: kuyruk da dolabilir ve gecikme birikerek cascade'e yol açabilir
    });

    // 3. Retry
    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType      = DelayBackoffType.Exponential,
        UseJitter        = true,
        Delay            = TimeSpan.FromSeconds(1),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TimeoutRejectedException>()
            .HandleResult(r => (int)r.StatusCode >= 500)
    });

    // 4. Timeout (attempt başına)
    pipeline.AddTimeout(TimeSpan.FromSeconds(4));
});
```

---

## Kontrol Soruları

1. ProductService saniyede 50 istek alıyor ve P99 latency 300ms.  
   Optimal bulkhead slot sayısı ne olmalı? Hesabı göster.

2. `QueueLimit = 5` koysan ne olur? ProductService 8sn'ye çıktığında  
   queue'daki 5 istek ne kadar bekler? Bu bir sorun mu?

3. `SemaphoreSlim` `static` tanımlanmazsa ne olur?  
   10 eş zamanlı istek geldiğinde kaç istek geçer?

4. Bulkhead ve Circuit Breaker aynı problemi çözüyor gibi görünüyor.  
   İkisi arasındaki temel fark ne? Birini koysan diğerine gerek kalır mı?

5. OrderService → ProductService → StockService zinciri var.  
   OrderService'te ProductService için bulkhead koydun.  
   ProductService'te StockService için bulkhead koymadın.  
   Ne olur?
