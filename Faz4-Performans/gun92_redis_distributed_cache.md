# Gün 92 — Redis: Distributed Cache Derinlemesine

---

## Redis Nedir?

Redis = **Remote Dictionary Server.** Bellek üzerinde çalışan, çok hızlı bir key-value veritabanı. Veritabanı değil "cache" olarak düşün — ama sadece cache değil, kuyruk, pub/sub, lock mekanizması da olabilir.

**Neden "distributed"?** Uygulaman 3 sunucuda çalışıyor. In-memory cache (MemoryCache) kullansan — her sunucunun kendi cache'i var, tutarsız. Redis **tek bir merkezi cache** — 3 sunucu da aynı Redis'e bağlanır, aynı veriyi görür.

**Analoji:** Ofisteki 3 kişi ayrı ayrı not defteri tutuyor (in-memory cache). Biri notu güncelleyince diğerleri bilmiyor. Bunun yerine duvardaki ortak beyaz tahta (Redis) — herkes aynı yere bakıyor.

**Ne zaman Redis?**
- Birden fazla uygulama instance'ı var (load balancer arkası)
- Oturum bilgisi paylaşılmalı (session)
- Sık okunan, nadir değişen veri (kategori listesi, config)
- Rate limiting, distributed lock, job queue

**Ne zaman gereksiz?**
- Tek instance uygulama — `MemoryCache` yeterli
- Veri zaten çok hızlı geliyor (5ms DB sorgusu cache'lemeye değmez)

---

## Redis Veri Yapıları

Redis sadece string tutmaz. 6 temel veri yapısı var ve her biri farklı bir problemi çözer:

| Yapı | Ne tutar | Gerçek kullanım |
|------|----------|-----------------|
| **String** | Tekil değer (text, sayı, JSON) | Cache'lenmiş API yanıtı, session token |
| **List** | Sıralı eleman listesi (queue/stack) | Son eklenen 10 bildirim, job queue |
| **Set** | Tekrarsız elemanlar (sırasız) | Online kullanıcı listesi, tag'ler |
| **Sorted Set** | Skor ile sıralı tekrarsız elemanlar | Liderlik tablosu, "en çok satan" |
| **Hash** | Field-value çiftleri (mini tablo) | Kullanıcı profili (ad, email, rol) |
| **Stream** | Append-only event log | Event sourcing, mesaj kuyruğu |

```csharp
// StackExchange.Redis ile örnekler:
var redis = ConnectionMultiplexer.Connect("localhost:6379");
var db = redis.GetDatabase();

// String — basit cache
await db.StringSetAsync("kitap:42", jsonString, TimeSpan.FromMinutes(10));
// ne yapar → "kitap:42" key'ine JSON'u yazar, 10 dk sonra otomatik silinir
var cached = await db.StringGetAsync("kitap:42");

// Hash — kullanıcı profili (tüm alanları tek seferde)
await db.HashSetAsync("user:123", new HashEntry[]
{
    new("ad", "Berkan"),
    new("email", "berkan@mail.com"),
    new("rol", "admin")
});
// ne yapar → tek key altında birden fazla alan saklar
// avantaj → sadece "ad" alanını okuyabilirsin (tüm JSON'u parse etmeden)
var ad = await db.HashGetAsync("user:123", "ad");

// Sorted Set — liderlik tablosu
await db.SortedSetAddAsync("leaderboard", "user:123", score: 1500);
await db.SortedSetAddAsync("leaderboard", "user:456", score: 2200);
var top10 = await db.SortedSetRangeByRankAsync("leaderboard", 0, 9, Order.Descending);
// ne yapar → skora göre sıralı tutar, top-N sorgusu O(log N)

// List — son bildirimler (queue)
await db.ListLeftPushAsync("notifications:123", "Yeni sipariş geldi");
var son5 = await db.ListRangeAsync("notifications:123", 0, 4);
// ne yapar → en yeni elemanı başa ekler, son 5'i alır
```

---

## IDistributedCache — ASP.NET Core Soyutlaması

ASP.NET Core'un built-in cache arayüzü. Redis'e doğrudan bağımlılık yerine bu soyutlamayı kullanırsan — test'te MemoryCache, production'da Redis kullanabilirsin.

```csharp
// Program.cs — Redis'i IDistributedCache olarak kaydet
builder.Services.AddStackExchangeRedisCache(opt =>
{
    opt.Configuration = "localhost:6379";
    // ne yapar → Redis bağlantı adresi
    opt.InstanceName = "KitapApp:";
    // ne yapar → tüm key'lerin başına "KitapApp:" ekler
    // bunu yazmasaydık → aynı Redis'i kullanan başka uygulama ile key çakışması
});
```

```csharp
// Kullanım — IDistributedCache inject et:
public class KitapService
{
    private readonly IDistributedCache _cache;
    private readonly IKitapRepo _repo;

    public async Task<List<Kitap>> GetKategorilerAsync()
    {
        var cacheKey = "kategoriler";
        var cached = await _cache.GetStringAsync(cacheKey);
        // ne yapar → Redis'ten "kategoriler" key'ini oku

        if (cached is not null)
            return JsonSerializer.Deserialize<List<Kitap>>(cached)!;
            // cache'te varsa → deserialize et, DB'ye gitme

        // Cache'te yok → DB'den al
        var kategoriler = await _repo.GetKategorilerAsync();
        
        await _cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(kategoriler),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                // ne yapar → 30 dk sonra otomatik silinir
                SlidingExpiration = TimeSpan.FromMinutes(10)
                // ne yapar → 10 dk kimse okumazsa silinir (erişildikçe uzar)
                // ikisi birden varsa → sliding uzar ama absolute'u geçemez
            });

        return kategoriler;
    }
}
```

**IDistributedCache'in limitleri:**
- Sadece `byte[]` veya string tutar (JSON serialize/deserialize senin işin)
- Get + Set arası race condition olabilir (iki istek aynı anda cache miss yaşarsa)
- Redis'in gelişmiş özelliklerini (Hash, Set, Pub/Sub) kullanamaz

---

## StackExchange.Redis — Doğrudan Client

IDistributedCache yetmediğinde doğrudan Redis client'ı kullanırsın. Daha güçlü ama daha düşük seviye.

```csharp
// Program.cs — ConnectionMultiplexer singleton olarak kaydet
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));
// ne yapar → Redis bağlantısını tek instance olarak paylaşır
// bunu her seferinde new yapsan → bağlantı havuzu tükenir (Redis max connection)

// Kullanım:
public class SepetService
{
    private readonly IDatabase _redis;

    public SepetService(IConnectionMultiplexer mux)
    {
        _redis = mux.GetDatabase();
    }

    public async Task SepeteEkleAsync(int userId, int kitapId)
    {
        var key = $"sepet:{userId}";
        await _redis.SetAddAsync(key, kitapId);
        // ne yapar → Set yapısına ekler (tekrar aynı kitap eklenmez)
        await _redis.KeyExpireAsync(key, TimeSpan.FromHours(24));
        // ne yapar → 24 saat sonra sepet otomatik temizlenir
    }

    public async Task<int[]> SepetGetirAsync(int userId)
    {
        var members = await _redis.SetMembersAsync($"sepet:{userId}");
        return members.Select(m => (int)m).ToArray();
    }
}
```

---

## Cache-Aside Pattern

En yaygın cache stratejisi. Mantık: önce cache'e bak → yoksa DB'den al → cache'e yaz.

```
İstek geldi
  → Cache'te var mı? (GET)
     EVET → direkt dön (cache HIT)
     HAYIR → DB'den oku → cache'e yaz (SET) → dön (cache MISS)
```

```csharp
public async Task<Kitap?> GetKitapAsync(int id)
{
    var key = $"kitap:{id}";

    // 1. Cache'e bak
    var json = await _redis.StringGetAsync(key);
    if (json.HasValue)
        return JsonSerializer.Deserialize<Kitap>(json!);

    // 2. Cache miss — DB'den al
    var kitap = await _repo.GetAsync(id);
    if (kitap is null) return null;

    // 3. Cache'e yaz (TTL ile)
    await _redis.StringSetAsync(key,
        JsonSerializer.Serialize(kitap),
        TimeSpan.FromMinutes(10));

    return kitap;
}

// Güncelleme olduğunda cache'i invalidate et:
public async Task GuncelleAsync(int id, KitapDto dto)
{
    await _repo.UpdateAsync(id, dto);
    await _redis.KeyDeleteAsync($"kitap:{id}");
    // ne yapar → eski cache silinir, sonraki okuma DB'den taze veri alır
    // bunu yazmasaydık → 10 dk boyunca eski veri gösterilir
}
```

---

## TTL ve Eviction Policy

### TTL (Time-To-Live) — Ne Kadar Süre Saklansın?

```csharp
await _redis.StringSetAsync("key", "value", TimeSpan.FromMinutes(5));
// 5 dk sonra Redis bu key'i otomatik siler — "expire" olur
```

**TTL nasıl belirlersin?**
- Sık değişen veri (fiyat, stok) → kısa TTL (1-5 dk)
- Nadir değişen veri (kategori, yazar listesi) → uzun TTL (30 dk - 1 saat)
- Asla değişmeyen (ülke listesi) → çok uzun TTL (24 saat) veya manuel invalidation

### Eviction Policy — Bellek Dolduğunda Ne Olur?

Redis bellek limiti dolduğunda eski verileri silmesi gerekir. Hangi stratejiyi kullandığını `maxmemory-policy` belirler:

| Policy | Ne yapar | Ne zaman kullan |
|--------|----------|-----------------|
| **noeviction** | Hiç silme, yeni yazma hata verir | Veri kaybı kabul edilemezse |
| **allkeys-lru** | En az erişilen key'i sil | Genel cache — en yaygın |
| **allkeys-lfu** | En az sıklıkla erişileni sil | Bazı key'ler çok sık okunuyorsa |
| **volatile-lru** | Sadece TTL'li key'lerden LRU sil | Kalıcı ve geçici veri karışıksa |

**LRU** = Least Recently Used (en uzun süredir erişilmeyen)
**LFU** = Least Frequently Used (en az sıklıkla erişilen)

---

## Redis Pub/Sub — Basit Event Bus

Bir servis event yayınlar, dinleyen servisler anında alır. Kalıcı değil — dinlemeyen kaçırır.

```csharp
// Publisher — kitap güncellendiğinde event yayınla:
var pub = _mux.GetSubscriber();
await pub.PublishAsync("kitap:guncellendi", JsonSerializer.Serialize(new { Id = 42 }));
// ne yapar → "kitap:guncellendi" kanalına mesaj gönderir
// bunu dinleyen tüm subscriber'lar anında alır

// Subscriber — başka serviste dinle:
var sub = _mux.GetSubscriber();
await sub.SubscribeAsync("kitap:guncellendi", (channel, message) =>
{
    var data = JsonSerializer.Deserialize<KitapEvent>(message!);
    // cache invalidation, bildirim gönderme, vs.
});
// ne yapar → bu kanal'a mesaj gelince callback çalışır
```

**Ne zaman kullan:** Basit event iletimi, cache invalidation bildirimi, gerçek zamanlı bildirim.
**Ne zaman kullanMA:** Mesaj kaybı kabul edilemezse (Pub/Sub kalıcı değil) → RabbitMQ/Kafka kullan.

---

## Atomic Operasyonlar

Redis single-threaded — komutlar sırayla çalışır. Bazı operasyonlar doğası gereği atomic:

```csharp
// INCR — sayaç (race condition yok)
await _redis.StringIncrementAsync("sayac:ziyaret", 1);
// ne yapar → değeri 1 artırır, iki istek aynı anda gelse bile doğru sonuç verir
// bunu read + write olarak yapsan → race condition (iki istek aynı değeri okur)

// SETNX — sadece key yoksa yaz (lock mekanizması temeli)
bool acquired = await _redis.StringSetAsync("lock:kitap:42", "owner-1",
    TimeSpan.FromSeconds(30),
    When.NotExists);
// ne yapar → key yoksa yazar (true döner), varsa yazmaz (false döner)
// When.NotExists = SETNX komutu
// neden önemli → distributed lock'un temeli
```

---

## Distributed Lock — RedLock Pattern

Birden fazla instance aynı kaynağa aynı anda erişmesin istiyorsun.

**Analoji:** Paylaşımlı tuvalet. Kapıyı kilitledin (lock aldın), işin bitince açtın (release). Başkası kilitli görünce bekler.

```csharp
// Basit distributed lock:
public async Task<bool> TryAcquireLockAsync(string resource, string owner, TimeSpan ttl)
{
    return await _redis.StringSetAsync(
        $"lock:{resource}",
        owner,
        ttl,
        When.NotExists);
    // ne yapar → lock key'i yoksa oluşturur (lock alındı)
    // TTL → lock sahibi çökerse TTL sonunda otomatik açılır (deadlock önlenir)
    // owner → sadece lock'u alan kişi release edebilsin diye
}

public async Task ReleaseLockAsync(string resource, string owner)
{
    var currentOwner = await _redis.StringGetAsync($"lock:{resource}");
    if (currentOwner == owner)
        await _redis.KeyDeleteAsync($"lock:{resource}");
    // ne yapar → sadece sen aldıysan serbest bırakırsın
    // bunu kontrol etmeseydin → başkasının lock'unu yanlışlıkla açabilirdin
}

// Kullanım:
if (await TryAcquireLockAsync("odeme:user:123", Environment.MachineName, TimeSpan.FromSeconds(30)))
{
    try { /* ödeme işle — tek instance çalışır */ }
    finally { await ReleaseLockAsync("odeme:user:123", Environment.MachineName); }
}
```

**RedLock nedir?** Tek Redis node çökerse lock kaybolur. RedLock birden fazla bağımsız Redis node'da lock alır — çoğunluk (N/2+1) alınırsa lock geçerli. Production'da güvenilir lock istiyorsan.

---

## HybridCache (.NET 9) — L1 + L2 Otomatik

.NET 9'da yeni: in-memory (L1) + Redis (L2) birlikte, framework yönetir.

```csharp
// Program.cs
builder.Services.AddHybridCache(opt =>
{
    opt.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),         // L2 (Redis) TTL
        LocalCacheExpiration = TimeSpan.FromMinutes(2) // L1 (bellek) TTL
    };
});
builder.Services.AddStackExchangeRedisCache(opt => opt.Configuration = "localhost:6379");

// Kullanım:
public class KitapService(HybridCache cache, IKitapRepo repo)
{
    public async Task<Kitap?> GetKitapAsync(int id)
    {
        return await cache.GetOrCreateAsync($"kitap:{id}",
            async ct => await repo.GetAsync(id),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(10) });
        // ne yapar:
        // 1. L1 (bellek) → varsa anında dön (0ms, network yok)
        // 2. L1 miss → L2 (Redis) → varsa dön + L1'e yaz
        // 3. L2 miss → factory çalışır (DB) → hem L1 hem L2'ye yaz
        // bunu elle yapsan → cache-aside + iki katman + stampede koruması kendin yazardın
    }
}
```

**Neden HybridCache?**
- L1 çok hızlı (0ms) ama instance-local → tutarsızlık riski
- L2 (Redis) tutarlı ama network hop (1-2ms)
- HybridCache ikisini birleştirip stampede protection (aynı anda 100 istek miss yaparsa sadece 1 tanesi DB'ye gider) ekler

---

## Redis Cluster vs Sentinel

| | Sentinel | Cluster |
|---|---|---|
| **Ne yapar** | Master çökünce replica'yı master yapar (failover) | Veriyi birden fazla node'a dağıtır (sharding) |
| **Kapasite** | Tek master'ın bellek limiti | Birden fazla master → toplam bellek artar |
| **Ne zaman** | Yüksek erişilebilirlik yeterli, veri tek node'a sığıyor | Veri çok büyük (>25 GB) veya çok yüksek throughput |
| **Karmaşıklık** | Düşük-orta | Yüksek |

**500 kullanıcı:** Tek Redis instance yeterli — Sentinel bile gereksiz.
**50K kullanıcı:** Sentinel ile failover koruması. Cluster ancak veri çok büyükse.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de cache yok — her istek DB'ye gidiyor. Tek instance olduğu için distributed cache ihtiyacı da yok. 50K'da:
- 3 instance load balancer arkasında → MemoryCache tutarsız → Redis gerekli
- Kategori listesi saniyede 500 kez sorgulanıyor → Redis'ten 0.5ms'de dönüyor (DB'den 15ms)
- Session yönetimi → instance çökünce session kaybolur → Redis-backed session

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| MemoryCache | Yeterli (tek instance) | Tutarsız — Redis'e geç |
| IDistributedCache | Gereksiz | Temel cache ihtiyacı için yeterli |
| StackExchange.Redis | Gereksiz | Gelişmiş yapılar (Set, Sorted Set) lazımsa |
| Pub/Sub | Gereksiz | Cache invalidation, gerçek zamanlı bildirim |
| Distributed Lock | Gereksiz | Ödeme/stok gibi kritik işlemlerde |
| HybridCache | Gereksiz | L1+L2 performans farkı hissedilir |
| Cluster/Sentinel | Gereksiz | Sentinel ile failover en azından |

---

## Kontrol Soruları

1. Redis'in 6 veri yapısından hangisini, hangi senaryoda kullanırsın?
2. IDistributedCache ile doğrudan StackExchange.Redis arasındaki fark ve trade-off nedir?
3. Cache-aside pattern'de güncelleme sonrası cache nasıl invalidate edilir?
4. TTL belirlerken neye göre karar verirsin?
5. Distributed lock neden TTL ile oluşturulmalı?
6. HybridCache'te L1 miss ama L2 hit olursa ne olur?
7. Pub/Sub neden kalıcı mesajlaşma için uygun değildir?
