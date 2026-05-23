# Gün 131 — Retry ve Timeout Stratejileri

---

## Önce Problemi Hisset

### Senaryo 1 — Geçici Bir Hata

Saat 14:23. Veri merkezindeki bir ağ switch'i yazılım güncellemesi için 80 milisaniyeliğine yeniden başlıyor. Bu çok kısa bir süre — gözlerini kırpıp açana kadar geçiyor. Ama o 80 ms içinde ProductService'e giden paket havaya uçuyor.

```
Müşteri sipariş veriyor
  → OrderService: "ProductService, bu ürün stokta var mı?"
  → Paket switch'in restart'ına denk geldi — düştü
  → HttpRequestException: "Connection refused"
  → Müşteri: "Sipariş oluşturulamadı. Lütfen tekrar deneyin."
```

Müşteri sayfayı kapattı. Sepeti boşaldı. Bir daha dönmedi.

Oysa ProductService tamamen sağlamdı. Ağ 80 ms sonra düzelmişti. 1 saniye sonra aynı isteği yapsaydın başarılı olurdu. Ama sistem bir daha denemedi. **Geçici bir arıza, kalıcı bir hata gibi gösterildi.**

---

### Senaryo 2 — İyileşen Sistemi Yeniden Çökerttik

Saat 23:00. Bir indirim kampanyası başladı. ProductService beklenmedik trafikle yavaşladı — timeout'lar gelmeye başladı.

Retry mekanizması var ama exponential backoff yok. Herkes aynı anda tekrar deniyor.

```
1.000 kullanıcı sipariş veriyor
  → ProductService yavaşladı, timeout aldık
  → Retry: 1 saniye sonra tekrar dene
  → 1.000 kullanıcı tam 1 saniye sonra AYNI ANDA tekrar istek gönderiyor
  → ProductService şimdi 2.000 istek görüyor — daha da yavaşladı
  → Retry tekrar tetiklendi → 3.000 istek
  → ProductService tamamen çöktü
```

Retry, **iyileşmekte olan sistemi ezdi.** Buna **retry storm** (retry fırtınası) denir. Sistemi kurtarmaya çalışırken batırdık.

---

### Senaryo 3 — Sessiz Donma

Saat 09:00. ProductService'in veritabanı bir indeks taraması yüzünden 45 saniyeye çıktı. Servis yanıt veriyor, sadece çok yavaş. Hiç hata yok.

```
Müşteri sipariş veriyor
  → OrderService, ProductService'i bekliyor...
  → 10 sn... 20 sn... 30 sn... 45 sn... yanıt geldi
  → Sipariş oluştu ama...
```

Sistemde hiçbir hata logu yok. Her şey "çalışıyor" gibi görünüyor. Ama 200 eş zamanlı müşteri varsa 200 thread 45 saniye boyunca orada bekliyor. Thread havuzu tıkandı. Yeni isteklere yanıt verilemiyor. **Sistem dondu ama kimse fark etmedi.**

---

## Gerçek Hayat Analojileri

### Retry — Meşgul Hatta Telefon

Bir arkadaşını arıyorsun. Meşgul tonu geliyor. Ne yaparsın?

Makul olan: Bir süre bekleyip tekrar ararsın. Belki 30 saniye sonra, belki 2 dakika sonra. Birkaç denemeden sonra hâlâ açmıyorsa bırakırsın.

Makul olmayan: Meşgul tonunu duyduktan 0,1 saniye sonra tekrar arasın. Ve tekrar. Ve tekrar. Saniyede 10 kez. Santral bu yükü kaldıramaz, tamamen çöker.

Retry da aynı. Sorunu "dene, başarısız ol, bekle, tekrar dene" döngüsüyle çözmeye çalışırsın. Ama **nasıl beklediğin** en az **deneyip denemediğin** kadar önemlidir.

---

### Exponential Backoff — Beklemeyi Akıllıca Yapmak

Retry'da bekleme süresi her başarısız denemede katlanarak artar.

1. denemeden sonra 1 saniye bekle.  
2. denemeden sonra 2 saniye bekle.  
3. denemeden sonra 4 saniye bekle.  
Sonra vazgeç.

Neden katlanarak artar? Çünkü servis yavaşladıysa, kısa aralıklarla denemek yükü artırır ve toparlanmasını engeller. Uzun aralıkla beklersen servis nefes alabilir, kendi başına düzelir.

Düz retry "kapıyı hızlı hızlı vurmak" ise, exponential backoff "makul aralıklarla nazikçe çalmak"tır.

---

### Jitter — Aynı Anda Gitme

Exponential backoff tek başına yetmez. 1.000 kullanıcı aynı anda timeout aldıysa, hepsi aynı formülü hesaplar ve hepsi tam 2 saniye sonra AYNI ANDA tekrar istek gönderir. Yük azalmadı, sadece 2 saniye ertelendi.

Jitter bunu kırar. Her kullanıcının bekleme süresine küçük bir rastgele değer eklenir.

Kullanıcı 1: 2.000ms + 347ms = 2.347sn  
Kullanıcı 2: 2.000ms + 891ms = 2.891sn  
Kullanıcı 3: 2.000ms + 124ms = 2.124sn  

Artık 1.000 istek 2sn ile 3sn arasına yayıldı. Tek bir tepe yerine küçük dalgalar oluştu. Servis kaldırabilir bir yük bu.

Konser bileti örneğini düşün: gece yarısı 00:00'da 50.000 kişi site açıyor. Site çöküyor, herkes aynı anda retry yapıyor, tekrar çöküyor. Eğer herkesin retry zamanına küçük bir rastgele gecikme eklenebilseydi — kimi 1.2 saniyede, kimi 2.7 saniyede, kimi 1.8 saniyede denerdi — site bu yayılmış yükü kaldırabilir ve toparlanabilirdi.

---

### Timeout — Garsonla Anlaşma

Restorana girdin, siparişini verdin. Makul bir bekleme süren var: 30 dakika.

Garsona "30 dakika içinde gelmezse iptal et" diyorsun. Garson da kabul etti.

Ama garson MUTFAĞA gittiğinde bunu söylemedi. Mutfak senin bekleme sınırından habersiz. 45 dakikada yemeği hazırladı. Sen çoktan ayrılmıştın. Hem malzeme harcandı, hem mutfak 45 dakika o yemekle meşgul oldu, hem de sana ulaşmadı.

İşte **deadline propagation** bu: sen OrderService'e "10 saniyem var" diyorsun, OrderService ProductService'e "7 saniyem var, o zaman bu isteğe en fazla 7 saniye harca" diyor. Servisler zinciri boyunca süre bilgisi akar. Artık kimse boşa çalışmıyor.

---

## Teknik Açıklama

### Retry Her Zaman Güvenli mi?

Hayır. Retry'ın güvenli olup olmadığı tamamen o endpoint'in **idempotent** olup olmadığına bağlıdır.

**Idempotent** demek, aynı isteği birden fazla kez göndersen de sonucun değişmemesi demektir. Bir ürünü bir kez sorsam da on kez sorsam da aynı yanıtı alırım — bu idempotent. Güvenle retry yapabilirim.

Ama `POST /api/orders` idempotent değildir. Her çağrıda yeni bir sipariş oluşur. Timeout aldım, retry yaptım — müşterinin iki siparişi var, iki kez ödeme bekliyor. `POST /api/payment/charge` için retry yaptım — kartından iki kez para çekildim.

Çözüm: **Idempotency Key**. Gün 127'de öğrendiğimiz pattern. İstek içinde benzersiz bir anahtar gönderirsin. Servis bu anahtarı daha önce gördüyse "zaten işlendi" yanıtı döner, tekrar işlemez. Böylece POST endpoint'ler de güvenle retry yapılabilir hale gelir.

---

### Timeout Değerini Nasıl Seçersin?

"5 saniye koyayım" diye tahmin etmek yanlış. Timeout değeri **ölçülmeli**, tahmin edilmemeli.

Doğru yaklaşım şudur: o endpoint normalde kaç milisaniyede yanıt veriyor? P99 latency nedir? Yani 100 isteğin 99'u kaç ms içinde tamamlanıyor?

P99 = 800ms ise, timeout = 3-4 saniye makuldür. Bu değer ölçeklenebilir bir güvenlik payı bırakır ama thread'i dakikalarca bloke etmez.

Çok kısa koyarsan ne olur? Geçici ağ gecikmelerinde bile başarısız sayılır, gereksiz retry'lar ve CB tetiklemeleri olur, yavaş ama geçerli yanıtlar kesilir. Çok uzun koyarsan? Thread o süre boyunca bloke kalır. 50.000 kullanıcıda bu thread açığı cascade failure'a götürür.

---

### Polly Pipeline Sırası Neden Önemli?

Circuit Breaker, Retry ve Timeout bir arada kullanılırken sıra yanlış kurulursa istenen davranışın tam tersi olur.

**Doğru sıra:** Circuit Breaker (en dış) → Retry (orta) → Timeout (en iç, attempt başına)

Neden Circuit Breaker en dışta olmalı? Çünkü devre açıkken retry yapmak anlamsızdır. Retry'ın görevi geçici ağ hatalarını emmek, ama devre açıksa ProductService tamamen erişilemez — geçici değil, kasıtlı olarak bağlantı kesik. Bu durumda üç kez retry yapmak sadece boşa backoff bekleme süresi demektir. Circuit Breaker dışarıda olursa, devre açıkken sisteme "Retry'a bile gitme, anında reddet" diyebilirsin.

Neden Timeout en içte olmalı? Çünkü her bireysel deneme için zaman sınırı koymak istiyorsun. Timeout içteyken Retry onu sarar — her deneme başında yeni bir 4 saniyelik sayaç başlar. Timeout dışarıda olsaydı tek bir genel süre sınırın olurdu ve ilk retry uzun sürerse sonraki deneme hiç olmayabilirdi.

---

## Faz3 ile Karşılaştırma

```csharp
// ─── Faz3: Monolith ──────────────────────────────────────────────────────
// IProductRepository.GetByIdAsync() aynı process içinde çalışır
// Başarısız olursa: ya anında DbException fırlatır ya da çalışır
// Aralarında "80ms ağ gecikmesi", "switch restart", "geçici bağlantı hatası" yok
// Retry ihtiyacı neredeyse sıfır

// ─── Faz5: Mikroservis ──────────────────────────────────────────���─────────
// HTTP → ağ → başka sunucu → başka süreç
// "Geçici arıza" diye yeni bir hata kategorisi ortaya çıktı
// Bu kategori Faz3'te yoktu, dolayısıyla Faz3'te retry pattern gerekmiyordu
// Faz5'te retry + backoff + jitter + timeout + CB production zorunluluğu
```

Faz3'ten Faz5'e geçerken karşılaştığın en köklü değişiklerden biri bu: in-process çağrı ya çalışır ya çöker, ağ çağrısı ise bir de **geçici olarak başarısız olabilir**. Retry pattern tam olarak bu yeni kategoriye hitap ediyor.

---

## 500 vs 50.000 Kullanıcı

| Durum | 500 Kullanıcı | 50.000 Kullanıcı |
|-------|--------------|-----------------|
| Retry YOK | Birkaç kullanıcı hata görür, fark edilir | Binlerce başarısız sipariş, gelir kaybı |
| Retry var, backoff YOK | Fark edilmez | Retry storm → sistemi tekrar çökerttik |
| Exponential backoff var, jitter YOK | Fark edilmez | Tüm retrylar aynı anda → hâlâ thundering herd |
| Backoff + Jitter | Fark edilmez | Yük zamana yayıldı, sistem toparladı |
| Timeout 60sn | Yavaş ama çalışıyor | Thread havuzu doluyor, cascade failure |
| Timeout P99 × 3-4 | Makul, hızlı hata | Thread havuzu sağlam, sistem ayakta |

---

## Kod

### Tam Resilience Pipeline

```csharp
// OrderService/Program.cs
.AddResilienceHandler("product-pipeline", pipeline =>
{
    // Sıra: CB (dış) → Retry (orta) → Timeout (iç, attempt başına)
    // CB OPEN ise Retry'a hiç gidilmez — gereksiz backoff beklenmez

    // 1. Circuit Breaker — en dışta
    pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        SamplingDuration  = TimeSpan.FromSeconds(30),
        MinimumThroughput = 5,       // 5 istek gelmeden karar verme
        FailureRatio      = 0.5,     // %50 hata oranı → devre açılır
        BreakDuration     = TimeSpan.FromSeconds(30),
        OnOpened     = args => { Console.WriteLine($"🔴 CB AÇILDI {args.BreakDuration.TotalSeconds}sn"); return default; },
        OnClosed     = args => { Console.WriteLine("🟢 CB KAPANDI");                                     return default; },
        OnHalfOpened = args => { Console.WriteLine("🟡 CB YARI AÇIK");                                  return default; }
    });

    // 2. Retry — ortada
    pipeline.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType      = DelayBackoffType.Exponential, // 1sn → 2sn → 4sn
        UseJitter        = true,  // ±%50 rastgele gecikme — thundering herd önler
        Delay            = TimeSpan.FromSeconds(1),

        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TimeoutRejectedException>()
            .HandleResult(r => (int)r.StatusCode >= 500),
            // 4xx → retry yapma: 404 "ürün yok" için 3 kez denesek de sonuç aynı
            // bunu yazmasaydık: iş mantığı hataları için de retry yapılır — anlamsız yük

        OnRetry = args =>
        {
            Console.WriteLine($"🔄 Retry {args.AttemptNumber + 1}/3 | " +
                              $"Bekleme: {args.RetryDelay.TotalMilliseconds:F0}ms");
            return default;
        }
    });

    // 3. Timeout — en içte (her attempt için ayrı sayaç)
    pipeline.AddTimeout(TimeSpan.FromSeconds(4));
    // bunu yazmasaydık: tek deneme 15sn sürebilir, Retry tüm bu süreyi bekler
});
```

---

## Kontrol Soruları

1. `Delay = 1sn`, `BackoffType = Exponential`, `UseJitter = false` ile üç retry yapıldı.  
   Her retrydeki bekleme süreleri tam olarak ne olur?

2. `POST /api/orders` retry güvenli mi? Güvenli hale getirmek için ne eklemelisin?

3. Circuit Breaker CLOSED, servis yavaş.  
   3 retry × 4sn = 12sn + backoff süreleri ≈ 19sn.  
   Bir HTTP isteği için kullanıcı 19 saniye beklemeli mi? Bu durumda ne yapardın?

4. Retry'ı CB'nin DIŞINA koysan (önce Retry, sonra CB), CB OPEN olduğunda ne olur?  
   Backoff süreleri de dahil toplam ne kadar süre boşa harcanır?

5. OrderService içinde toplam 10sn bütçen var. ProductService'e çağrı yapıyorsun.  
   2 saniye geçti. Kalan 8 saniyeyi ProductService'e nasıl iletirsin?
