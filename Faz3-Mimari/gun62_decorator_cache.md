# Gün 62 — Decorator Pattern: Cache Katmanı Onion'a Ekle

Gün 59–61'de `KitapRepository` doğrudan DB'ye gidiyordu. Her `/api/kitap` isteğinde `SELECT * FROM Kitaplar` çalışıyor. 50k kullanıcıda bu yük gereksiz — kitap listesi dakikalar içinde değişmiyor.

**Çözüm:** Decorator pattern ile `CachingKitapRepository` yazıyoruz. Handler, IKitapRepository interface'ini görüyor — cache'in varlığından haberi yok. Infrastructure değişti, Application ve Domain'e tek satır dokunmadık.

---

## Faz2 ile Karşılaştırma

```csharp
// Faz2 — cache eklemek için servise if yazmak gerekiyordu
public class KitapServisi
{
    private readonly KitabeviDbContext _context;
    private readonly IMemoryCache _cache;

    public async Task<List<Kitap>> TumunuGetirAsync()
    {
        if (_cache.TryGetValue("kitaplar", out List<Kitap> cached))
            return cached;                     // cache'de varsa dön

        var kitaplar = await _context.Kitaplar.ToListAsync();
        _cache.Set("kitaplar", kitaplar, TimeSpan.FromMinutes(5));
        return kitaplar;
        // ↑ cache mantığı iş mantığına gömüldü
        //   test etmek için hem DB hem cache mock gerekiyor
        //   başka metoda da cache ekleyeceksen aynı if-else kopyalanıyor
    }
}
```

**Onion Decorator:**

```csharp
// Yeni decorator — KitapListeleHandler'a tek satır dokunmadık
public class CachingKitapRepository : IKitapRepository
{
    // cache mantığı burada — Handler bilmiyor
}
```

---

## 1. Domain — Değişen Yok

Domain'e tek satır dokunmuyoruz. `IKitapRepository` interface'i aynı.

---

## 2. Application — Değişen Yok

`KitapListeleHandler` ve `KitapEkleHandler` aynı. Interface görüyorlar, implementasyon bilmiyorlar.

---

## 3. Infrastructure — Decorator Ekle

### `Persistence/Repositories/CachingKitapRepository.cs`

```csharp
namespace KitabeviOnion.Infrastructure.Persistence.Repositories;

public class CachingKitapRepository : IKitapRepository
//                                     ↑ aynı interface — Handler için şeffaf
{
    private readonly IKitapRepository _inner;
    //               ↑ asıl repository: CachingRepo başarısız olursa inner'a düşer
    //                 bunu yazmasaydık → DB çağrısını da buraya yazmak zorunda kalırdık,
    //                 decorator değil başka bir implementasyon olurdu

    private readonly IMemoryCache _cache;
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);
    //                                        ↑ 5 dakika cache — değişecekse tek yer

    private const string TumKitaplarKey = "kitaplar:tumü";
    //                                     ↑ cache key sabiti — string literal dağılmasın

    public CachingKitapRepository(IKitapRepository inner, IMemoryCache cache)
    //                             ↑ inner inject: KitapRepository (asıl DB katmanı)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(TumKitaplarKey, out IReadOnlyList<Kitap>? cached) && cached is not null)
        {
            return cached;
            // ↑ cache hit: DB'ye gitme
            //   bunu yazmasaydık → her istekte DB sorgusu, 50k'da gereksiz yük
        }

        var kitaplar = await _inner.TumunuGetirAsync(ct);
        //                          ↑ cache miss: asıl DB çağrısı

        _cache.Set(TumKitaplarKey, kitaplar, _ttl);
        //         ↑ 5 dakika sakla
        //           bunu yazmasaydık → bir sonraki istekte tekrar DB'ye gidilirdi

        return kitaplar;
    }

    public async Task EkleAsync(Kitap kitap, CancellationToken ct = default)
    {
        await _inner.EkleAsync(kitap, ct);
        //            ↑ önce DB'ye ekle
    }

    public async Task KaydetAsync(CancellationToken ct = default)
    {
        await _inner.KaydetAsync(ct);

        _cache.Remove(TumKitaplarKey);
        //             ↑ yeni kitap eklendi → cache stale (bayatladı) → temizle
        //               bunu yazmasaydık → eski liste 5 dakika daha serviste kalırdı,
        //               yeni eklenen kitap listede görünmezdi
    }

    public async Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default)
    {
        var cacheKey = $"kitap:{id}";
        //              ↑ her kitap için ayrı key — "kitap:42", "kitap:7"

        if (_cache.TryGetValue(cacheKey, out Kitap? cached))
            return cached;

        var kitap = await _inner.BulByIdAsync(id, ct);

        if (kitap is not null)
            _cache.Set(cacheKey, kitap, _ttl);
        //            ↑ sadece bulunanı cache'le — null'ı saklama
        //              bunu yazmasaydık → olmayan Id'ler için de DB'ye gidilirdi

        return kitap;
    }

    public Task<bool> IsbnMevcutMu(Isbn isbn, CancellationToken ct = default)
        => _inner.IsbnMevcutMu(isbn, ct);
    //   ↑ bu metodu cache'lemiyoruz — duplikat kontrolü her zaman taze veri istiyor
    //     bunu cache'lesek → yeni eklenen ISBN 5 dakika "yok" görünebilirdi
}
```

---

## 4. API — Program.cs Decorator Kaydı

```csharp
// Program.cs

// Önce asıl implementasyonu kaydet
builder.Services.AddScoped<KitapRepository>();
//               ↑ interface değil, concrete tip — decorator içine inject edilecek

// Sonra decorator'ı IKitapRepository olarak kaydet
builder.Services.AddScoped<IKitapRepository>(sp =>
{
    var inner = sp.GetRequiredService<KitapRepository>();
    //             ↑ asıl DB repository'sini al
    var cache = sp.GetRequiredService<IMemoryCache>();
    return new CachingKitapRepository(inner, cache);
    //          ↑ decorator içine sar — dışarıya IKitapRepository gibi görün
});
// bunu yazmasaydık → IKitapRepository resolve edilince CachingRepo değil KitapRepository gelirdi
// Handler hep DB'ye giderdi

builder.Services.AddMemoryCache();
//               ↑ IMemoryCache DI container'a kaydet — olmadan inject edilemez

// ISiparisRepository değişmedi — aynı kayıt
builder.Services.AddScoped<ISiparisRepository, SiparisRepository>();
```

---

## 5. Test — Decorator Unit Test

```csharp
// Tests/Infrastructure/CachingKitapRepositoryTests.cs
namespace KitabeviOnion.Tests.Infrastructure;

public class CachingKitapRepositoryTests
{
    private readonly Mock<IKitapRepository> _innerMock;
    private readonly IMemoryCache _cache;
    private readonly CachingKitapRepository _cachingRepo;

    public CachingKitapRepositoryTests()
    {
        _innerMock = new Mock<IKitapRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        //        ↑ gerçek MemoryCache — mock değil
        //          bunu mock'lasaydık → Set/Get davranışı gerçeği yansıtmazdı

        _cachingRepo = new CachingKitapRepository(_innerMock.Object, _cache);
    }

    [Fact]
    public async Task TumunuGetir_IlkCagri_DbdenAlir()
    {
        // Arrange
        var beklenen = new List<Kitap>
        {
            new("Clean Code", new Isbn("9780132350884"), new Fiyat(150), 10)
        }.AsReadOnly();

        _innerMock
            .Setup(r => r.TumunuGetirAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(beklenen);

        // Act
        var sonuc = await _cachingRepo.TumunuGetirAsync();

        // Assert
        Assert.Equal(beklenen, sonuc);

        _innerMock.Verify(
            r => r.TumunuGetirAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        //  ↑ ilk çağrıda DB'ye gitti
    }

    [Fact]
    public async Task TumunuGetir_IkinciCagri_CachedenAlir()
    {
        // Arrange
        var beklenen = new List<Kitap>
        {
            new("Clean Code", new Isbn("9780132350884"), new Fiyat(150), 10)
        }.AsReadOnly();

        _innerMock
            .Setup(r => r.TumunuGetirAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(beklenen);

        // Act — iki kez çağır
        await _cachingRepo.TumunuGetirAsync();
        await _cachingRepo.TumunuGetirAsync();

        // Assert — DB sadece bir kez çağrıldı
        _innerMock.Verify(
            r => r.TumunuGetirAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        //  ↑ ikinci çağrıda cache'den geldi — DB çağrısı yok
        //    bunu test etmeseydik → cache çalışıp çalışmadığından emin olamazdık
    }

    [Fact]
    public async Task KaydetAsync_SonrasiCache_Temizlenir()
    {
        // Arrange — önce cache'i doldur
        var beklenen = new List<Kitap>().AsReadOnly();
        _innerMock
            .Setup(r => r.TumunuGetirAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(beklenen);
        _innerMock
            .Setup(r => r.KaydetAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _cachingRepo.TumunuGetirAsync(); // cache doldu

        // Act — kaydet → cache temizlenmeli
        await _cachingRepo.KaydetAsync();

        // Assert — bir sonraki TumunuGetir DB'den gitmeli
        await _cachingRepo.TumunuGetirAsync();

        _innerMock.Verify(
            r => r.TumunuGetirAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        //  ↑ KaydetAsync sonrası cache temizlendi → ikinci DB çağrısı yapıldı
        //    bunu test etmeseydik → stale cache davranışını yakalayamazdık
    }
}
```

---

## 6. LoggingKitapRepository — İkinci Decorator

```csharp
// Infrastructure/Persistence/Repositories/LoggingKitapRepository.cs
namespace KitabeviOnion.Infrastructure.Persistence.Repositories;

public class LoggingKitapRepository : IKitapRepository
//                                     ↑ yine aynı interface
{
    private readonly IKitapRepository _inner;
    private readonly ILogger<LoggingKitapRepository> _logger;

    public LoggingKitapRepository(IKitapRepository inner, ILogger<LoggingKitapRepository> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var sonuc = await _inner.TumunuGetirAsync(ct);
        sw.Stop();

        _logger.LogInformation("TumunuGetir: {Adet} kitap, {Ms}ms", sonuc.Count, sw.ElapsedMilliseconds);
        //      ↑ performans log — hangi sorgu kaç ms sürdü
        //        bunu yazmasaydık → handler'a logging kodu eklemek zorunda kalırdık

        return sonuc;
    }

    public async Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default)
    {
        var sonuc = await _inner.BulByIdAsync(id, ct);
        _logger.LogInformation("BulById({Id}): {Sonuc}", id, sonuc is null ? "bulunamadı" : "bulundu");
        return sonuc;
    }

    public Task EkleAsync(Kitap kitap, CancellationToken ct = default)
        => _inner.EkleAsync(kitap, ct);

    public Task KaydetAsync(CancellationToken ct = default)
        => _inner.KaydetAsync(ct);

    public Task<bool> IsbnMevcutMu(Isbn isbn, CancellationToken ct = default)
        => _inner.IsbnMevcutMu(isbn, ct);
}
```

### İki Decorator Zincirleme — Program.cs

```csharp
// Logging → Cache → DB zinciri

builder.Services.AddScoped<KitapRepository>();
//               ↑ en içteki: gerçek DB

builder.Services.AddScoped<IKitapRepository>(sp =>
{
    var dbRepo = sp.GetRequiredService<KitapRepository>();
    var cache  = sp.GetRequiredService<IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<LoggingKitapRepository>>();

    var cachingRepo  = new CachingKitapRepository(dbRepo, cache);
    //                                              ↑ DB'yi sar: cache katmanı

    var loggingRepo  = new LoggingKitapRepository(cachingRepo, logger);
    //                                              ↑ cache'i sar: logging katmanı

    return loggingRepo;
});

// Zincir:
// Handler → LoggingRepo → CachingRepo → KitapRepository (DB)
//
// Cache hit senaryosu:
// Handler → LoggingRepo (log başlangıç)
//         → CachingRepo (cache'de var → DB'ye gitme)
//         → LoggingRepo (log süre: ~0ms, cache)
//
// Cache miss senaryosu:
// Handler → LoggingRepo
//         → CachingRepo (cache boş)
//         → KitapRepository (DB query)
//         → CachingRepo (cache'e yaz)
//         → LoggingRepo (log süre: ~24ms, DB)
```

---

## Decorator vs Inheritance — Neden Decorator

```csharp
// ❌ Kalıtım ile (Go'da yok, C#'ta da antipattern)
public class CachingKitapRepository : KitapRepository
//                                     ↑ concrete sınıfa bağımlı
//                                       KitapRepository değişirse burası da kırılır
{
    public override async Task<IReadOnlyList<Kitap>> TumunuGetirAsync(...)
    {
        // override — ama KitapRepository'nin internals'ına bağımlı
    }
}

// ✅ Decorator (interface'e bağımlı)
public class CachingKitapRepository : IKitapRepository
//                                     ↑ sadece interface bilgisi
//                                       KitapRepository'yi NpgsqlKitapRepository ile
//                                       değiştirsen decorator değişmiyor
{
    private readonly IKitapRepository _inner; // herhangi bir implementasyon
}
```

---

## Decorator Zinciri Görsel

```
Handler (IKitapRepository görüyor)
    │
    ▼
LoggingKitapRepository
    │  log: "TumunuGetir çağrıldı"
    │  inner.TumunuGetirAsync()
    ▼
CachingKitapRepository
    │  cache'de var mı?
    │  ─ Evet → return cached (DB'ye gitme)
    │  ─ Hayır → inner.TumunuGetirAsync()
    ▼
KitapRepository (EF Core)
    │  SELECT * FROM Kitaplar
    ▼
PostgreSQL
```

---

## 500 vs 50k

| Konu | 500 | 50k |
|---|---|---|
| **Cache gerekli mi?** | Hayır — DB yük yok | ✅ Tekrarlı okuma → zorunlu |
| **Decorator mı, inline cache mi?** | Inline kabul | ✅ Decorator — Handler değişmez |
| **Cache TTL** | — | ⚠️ Domain'e göre ayarla: fiyat 1 dk, kategori 1 saat |
| **Cache invalidation** | — | ✅ KaydetAsync sonrası temizle |
| **Logging decorator** | Overkill | ✅ Hangi sorgu ne kadar sürdü — production debug |

---

## Sorular

1. `IsbnMevcutMu` metodunu neden cache'lemedik? Cache'lesek ne olurdu?
2. Decorator zincirinde `LoggingRepo → CachingRepo → DbRepo` sırası neden böyle? Tersine çevrilseydi fark ne olurdu?
3. `CachingKitapRepository` test edilirken gerçek `MemoryCache` kullandık, neden mock kullanmadık?
