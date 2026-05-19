# Gün 94 — Hafta 13 Özet

---

## Bu Hafta Ne Öğrendik?

| Gün | Konu | Bir Cümle |
|-----|------|-----------|
| 88 | Response Compression, Caching, CDN | Veriyi küçült, tekrar hesaplama, kullanıcıya yaklaştır |
| 89 | Rate Limiting | API'nı aşırı istekten koru — 429 Too Many Requests |
| 90 | Minimal API Performance | Controller overhead'ini at, compile-time optimizasyonlar |
| 91 | SQL ve Index Stratejisi | Index yoksa full scan, OFFSET büyük tabloda ölçeklenmez |
| 92 | Redis Distributed Cache | Merkezi cache, lock, pub/sub — çok instance'da tutarlılık |
| 93 | Health Checks | Uygulamanın nabzını ölç, bağımlılık çöktüyse trafik gönderme |

---

## Performans Checklist — Code Review'da Kontrol Et

Her PR'da veya deploy öncesinde şu soruları sor:

### Veritabanı & EF Core

- [ ] **AsNoTracking()** kullanıldı mı? (Read-only sorgularda change tracker gereksiz)
- [ ] **N+1 sorunu** var mı? (Loop içinde lazy load → `Include` veya split query ile çöz)
- [ ] **Index** var mı? (WHERE/JOIN/ORDER BY kolonlarında — execution plan kontrol et)
- [ ] **Pagination** doğru mu? (Büyük tabloda Skip/Take yerine keyset düşün)
- [ ] **ExecuteUpdate/Delete** kullanılabilir mi? (Toplu güncelleme tek tek mi yapılıyor?)

### Bellek & Allocation

- [ ] **Büyük koleksiyon** tamamen memory'de mi? (100K satır ToListAsync → IAsyncEnumerable düşün)
- [ ] **String concat loop'ta mı?** (Her + yeni string allocation → StringBuilder kullan)
- [ ] **IDisposable** her yerde dispose ediliyor mu? (HttpClient, Stream, DbContext → using)
- [ ] **Closure allocation** var mı? (Hot path'te lambda içinde dış değişken yakalama)

### Async & Thread Safety

- [ ] **Synchronous I/O** var mı? (`.Result`, `.Wait()` → thread pool starvation riski)
- [ ] **I/O olmadan async** var mı? (Gereksiz Task overhead — senkron dönebilir)
- [ ] **Fire-and-forget** güvenli mi? (Exception kaybolur — en azından log'la)

### Network & Caching

- [ ] **Response compression** aktif mi? (JSON API → Brotli/Gzip)
- [ ] **Cache-Control header** var mı? (Statik dosyalar → uzun TTL, API → kısa veya ETag)
- [ ] **Output Cache** düşünüldü mü? (Sıcak endpoint'lerde DB'ye gitmeden cevap ver)
- [ ] **Rate limiting** var mı? (Public endpoint → koruma şart)

### Deployment & Monitoring

- [ ] **Health check** endpoint'i var mı? (Liveness + readiness)
- [ ] **Bağımlılık health check'leri** kayıtlı mı? (DB, Redis, dış servis)
- [ ] **Connection pool** ayarları uygun mu? (Min/Max pool size)

---

## Senaryo Soruları

### Senaryo 1: Paralel URL Fetch

> 10.000 URL'yi paralel fetch edeceksin. ThreadPool starvation olmadan nasıl yaparsın?

**Cevap:**
```csharp
// SemaphoreSlim ile parallelizmi sınırla:
var semaphore = new SemaphoreSlim(50);  // aynı anda max 50 HTTP isteği
var tasks = urls.Select(async url =>
{
    await semaphore.WaitAsync();
    try
    {
        return await httpClient.GetStringAsync(url);
    }
    finally
    {
        semaphore.Release();
    }
});
var results = await Task.WhenAll(tasks);
```

**Neden 50 ile sınırladık?**
- 10.000 paralel istek → 10.000 socket açılır → OS limiti aşılır
- HttpClient connection pool tükenir
- Hedef sunucuya DDoS yaparsın
- SemaphoreSlim thread bloklamaz (async-friendly) → starvation yok

---

### Senaryo 2: Sıcak Endpoint Optimizasyonu

> `/api/kategoriler` endpoint'i saniyede 500 kez çağrılıyor. Her çağrıda DB'ye gidiyor. Nasıl optimize edersin?

**Katmanlı çözüm:**

```
1. Output Cache (5 dk) → handler hiç çalışmaz, sunucu cache'ten döner
2. Response Compression → 8 KB yerine 2 KB gider
3. Cache-Control: public, max-age=300 → CDN ve tarayıcı da cache'ler
4. CDN arkasında → origin'e istek sayısı %95 azalır
```

Sonuç: 500 istek/sn → origin'e sadece 1 istek/5 dk.

---

### Senaryo 3: Task.Run Her Yerde

> Bir geliştirici her controller action'ı `Task.Run(() => ...)` içine sarmış. Neden yanlış?

**Cevap:**
- ASP.NET Core zaten async — her istek ThreadPool thread'inde çalışıyor
- `Task.Run` yeni bir ThreadPool thread'i alır → ama ilk thread zaten boşa çıktı
- Net sonuç: 1 istek = 2 thread tüketir (biri bekler, biri çalışır)
- 100 eşzamanlı istek → 200 thread → thread pool starvation

**Doğru:** Action zaten async olmalı, `Task.Run` yalnızca CPU-bound iş varsa (hesaplama, sıkıştırma) kullanılmalı.

---

### Senaryo 4: Redis Cache Stampede

> Redis cache expire oldu. Aynı anda 100 istek geldi. Hepsi cache miss yaşadı, hepsi DB'ye gitti. Bu "stampede" nasıl önlenir?

**Çözümler:**

| Yöntem | Nasıl |
|--------|-------|
| **Lock (SemaphoreSlim)** | İlk istek DB'ye gider + cache yazar, diğerleri bekler |
| **HybridCache (.NET 9)** | Otomatik stampede protection — tek istek factory çalıştırır |
| **Stale-while-revalidate** | Expire olmuş veriyi dön, arka planda yenile |
| **Randomized TTL** | TTL'e ±%10 random ekle → tüm key'ler aynı anda expire olmaz |

---

## Hafta 13 Kazanımları — Checklist

Bu haftayı tamamladıysan şunları yapabilmelisin:

- [ ] Response Compression middleware'ini yapılandırabilirsin
- [ ] Cache-Control header'larını senaryoya göre seçebilirsin (public/private/no-store)
- [ ] Output Cache ile sunucu tarafı cache'leme yapabilirsin
- [ ] Rate limiting stratejilerini (fixed/sliding/token/concurrency) açıklayabilirsin
- [ ] Minimal API'da TypedResults, endpoint filter ve short-circuit kullanabilirsin
- [ ] Execution plan okuyup eksik index tespit edebilirsin
- [ ] Keyset pagination'ın neden Skip/Take'den hızlı olduğunu açıklayabilirsin
- [ ] Redis'i IDistributedCache veya StackExchange.Redis ile kullanabilirsin
- [ ] Health check endpoint'i yazıp liveness/readiness ayrımını yapabilirsin
