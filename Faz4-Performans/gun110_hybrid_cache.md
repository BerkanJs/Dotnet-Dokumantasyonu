# Gün 110 — HybridCache (.NET 9): L1 + L2 Cache Pattern

---

## Bu Ders Neden Var?

Gün 92'de Redis ve distributed cache'i gördük. Gün 88'de MemoryCache ve OutputCache vardı. Bu iki cache türünün avantajları ve dezavantajları farklıydı.

.NET 9 ile gelen **HybridCache** ikisini birleştiriyor — hem hızlı yerel cache (L1) hem de paylaşılan distributed cache (L2) — tek bir API ile.

Bugün bunu derinlemesine inceleyeceğiz. Niye lazım, hangi sorunları çözüyor, ne zaman kullanılmalı, ne zaman aşırı.

---

## L1 ve L2 Cache Kavramı

CPU dünyasından gelen bir kavram. Bilgisayarda RAM yavaş, CPU çok hızlı. Aradaki uçurumu kapatmak için **çok katmanlı cache** kullanılıyor:
- L1 cache: CPU'nun içinde, küçük ama mikrosaniyenin altında
- L2 cache: CPU'nun yakınında, biraz daha büyük, biraz daha yavaş
- L3 cache: Daha geniş, daha yavaş
- RAM: En büyük, en yavaş

Aynı mantık uygulama cache'inde de:

**L1 cache (in-memory, uygulama içinde):**
- `IMemoryCache` veya yerel hashmap
- Erişim süresi: ~0ms (RAM'den okuma)
- Sınır: Her uygulama instance'ının kendi belleği
- Sorun: 3 instance varsa 3 ayrı cache, tutarsız olabilir

**L2 cache (distributed, paylaşılan):**
- Redis veya benzeri merkezi cache
- Erişim süresi: ~1-2ms (network gidip gelmesi)
- Avantaj: Tüm instance'lar aynı veriyi görür, tutarlı
- Sorun: L1'den 100x daha yavaş

**İki katman birden:** L1'e bak — yoksa L2'ye bak — yoksa veriyi üret (DB), her iki katmana yaz.

Bu pattern'i elle yazmak karmaşık. HybridCache otomatik yapıyor.

---

## HybridCache'in Çözdüğü Asıl Sorun: Cache Stampede

Cache pattern'inin bilinen bir problemi var: **cache stampede** (cache çiğnemesi).

### Stampede Nasıl Oluşur?

Senaryo:
- "Top 100 kitap" listesi cache'lenmiş, TTL 10 dakika
- 10. dakikada cache expire oluyor
- Tam o anda 500 istek geliyor
- Hepsi cache'i kontrol ediyor → boş
- **Hepsi aynı anda DB'ye gidiyor** → 500 paralel DB sorgusu
- DB'nin başı dönüyor, yavaşlıyor, belki çöküyor
- Sorgular tamamlanınca hepsi cache'e aynı veriyi yazıyor (gereksiz iş)

Cache'in amacı DB yükünü azaltmaktı — stampede sırasında tam tersi oluyor.

### Klasik Çözüm: Lock + Double-Check

```csharp
public async Task<List<Kitap>> GetTopAsync()
{
    var cached = await _cache.GetStringAsync("top-kitaplar");
    if (cached is not null) return Deserialize(cached);

    await _semaphore.WaitAsync();
    try
    {
        // Tekrar kontrol et — bekledikçen başka biri yazmış olabilir:
        cached = await _cache.GetStringAsync("top-kitaplar");
        if (cached is not null) return Deserialize(cached);

        var data = await _repo.GetTopAsync();   // sadece bir kez DB'ye gider
        await _cache.SetStringAsync("top-kitaplar", Serialize(data));
        return data;
    }
    finally { _semaphore.Release(); }
}
```

Bu kod stampede'i çözüyor ama:
- Her cache çağrısında elle yazmak zorundasın
- Lock yönetimi hataya açık
- Distributed senaryoda (3 instance) lock yetmiyor — distributed lock gerekiyor
- Test etmesi zor

İşte HybridCache bunu tek satıra indirgiyor.

---

## HybridCache API

### Temel Kullanım

```csharp
// NuGet: Microsoft.Extensions.Caching.Hybrid
builder.Services.AddHybridCache();

// Servisinde:
public class KitapService
{
    private readonly HybridCache _cache;
    private readonly IKitapRepo _repo;

    public KitapService(HybridCache cache, IKitapRepo repo)
    {
        _cache = cache;
        _repo = repo;
    }

    public async Task<Kitap?> GetKitapAsync(int id, CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(
            $"kitap:{id}",
            async (cancel) => await _repo.GetAsync(id),
            cancellationToken: ct);
    }
}
```

Tek metot çağrısı. Arka planda:
1. L1 (memory) kontrol → varsa anında döndür
2. L1 miss → L2 (Redis varsa) kontrol → varsa hem L1'e hem caller'a dönder
3. L2 miss → factory delegate çağrılır (DB'den getir) → hem L1 hem L2'ye yazar
4. **Aynı anda 500 istek geldi → factory sadece 1 kez çağrılır** (stampede protection)

### TTL ve Diğer Ayarlar

```csharp
var options = new HybridCacheEntryOptions
{
    Expiration = TimeSpan.FromMinutes(10),
    // ne yapar → L2 (distributed) cache'te 10 dk yaşar

    LocalCacheExpiration = TimeSpan.FromMinutes(2),
    // ne yapar → L1 (memory) cache'te 2 dk yaşar
    // neden daha kısa → L1 daha az "taze" olabilir, L2 daha güncel olsun

    Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
    // ne yapar → L1'e yazma, sadece L2'ye yaz (bazı senaryolarda lazım)
};

var data = await _cache.GetOrCreateAsync(
    "key",
    factory: async ct => await GetFromDbAsync(ct),
    options: options,
    cancellationToken: ct);
```

### Tag ile Gruplama ve Invalidation

```csharp
var data = await _cache.GetOrCreateAsync(
    $"kitap:{id}",
    async ct => await _repo.GetAsync(id),
    tags: new[] { "kitaplar", $"yazar:{yazarId}" });
// ne yapar → bu cache girişi iki tag altında gruplanır

// Daha sonra tag bazlı temizleme:
await _cache.RemoveByTagAsync("kitaplar");
// ne yapar → "kitaplar" tag'li tüm cache girişleri silinir
// kitap güncellendiğinde: tüm liste cache'leri otomatik invalidate

await _cache.RemoveByTagAsync($"yazar:{yazarId}");
// ne yapar → sadece o yazara ait kitap cache'leri silinir
```

Tag-based invalidation, distributed cache'te en güçlü pattern'lerden biri. Tek tek key silmek yerine grup halinde temizliyorsun.

### Tekil Silme

```csharp
await _cache.RemoveAsync($"kitap:{id}");
// hem L1 hem L2'den siler
// 3 instance varsa hepsindeki L1'lerden silinir mi? — Versiyona bağlı
// L2'de silinir, L1'lerde TTL ile expire olur (eventual consistency)
```

---

## Konfigürasyon — L2'yi Bağlama

HybridCache varsayılan olarak sadece L1 kullanır. Redis (veya başka distributed cache) eklemek için:

```csharp
// Redis bağlantısı:
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});

// HybridCache otomatik olarak IDistributedCache'i L2 olarak kullanır:
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});
```

Bu kadar. HybridCache `IDistributedCache`'in DI'da kayıtlı olduğunu görüyor, L2 olarak otomatik kullanıyor. Yoksa sadece L1 modu çalışıyor.

---

## HybridCache vs Mevcut Çözümler

Sürekli "ne zaman ne kullanmalı?" sorusu var. Karşılaştıralım.

### IMemoryCache (klasik L1)

```csharp
public Kitap? GetKitap(int id)
{
    return _memoryCache.GetOrCreate($"kitap:{id}", entry =>
    {
        entry.AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(10);
        return _repo.Get(id);
    });
}
```

**Avantaj:** Çok basit, çok hızlı (0ms).
**Dezavantaj:**
- Stampede koruması yok
- Multi-instance'ta tutarsız
- Distributed senaryoya çıkartmak için kod yeniden yazılmalı

**Ne zaman:** Tek instance, hızlı geliştirme, kritik olmayan veri.

### IDistributedCache (klasik L2)

```csharp
public async Task<Kitap?> GetKitapAsync(int id)
{
    var json = await _distributedCache.GetStringAsync($"kitap:{id}");
    if (json is not null) return JsonSerializer.Deserialize<Kitap>(json);

    var kitap = await _repo.GetAsync(id);
    await _distributedCache.SetStringAsync($"kitap:{id}", JsonSerializer.Serialize(kitap));
    return kitap;
}
```

**Avantaj:** Multi-instance tutarlılığı, kolay setup.
**Dezavantaj:**
- L1 yok → her erişim 1-2ms network
- Stampede koruması yok
- Serialization elle yönetilir
- Tag-based invalidation yok

**Ne zaman:** Tek katman distributed cache yeterliyse, basit senaryolarda.

### HybridCache

**Avantaj:**
- L1 + L2 otomatik
- Stampede protection dahili
- Tag-based invalidation
- Serialization otomatik
- Tek API

**Dezavantaj:**
- .NET 9+ gerekli
- Yeni — production'da uzun süre kanıtlanmış değil
- Daha fazla soyutlama, debug zor olabilir

**Ne zaman:** Modern proje, yüksek RPS endpoint'ler, çoklu instance, kritik veri.

### Karar Tablosu

| Senaryo | Tercih |
|---------|--------|
| Tek instance, basit cache | IMemoryCache |
| Multi-instance, basit | IDistributedCache |
| Yüksek RPS, stampede koruması lazım | HybridCache |
| L1 hızı + L2 tutarlılığı ikisi de gerekli | HybridCache |
| Output cache (full HTTP response) | OutputCache (Gün 88) |
| Çok karmaşık cache logic | Manuel — kendi yaz |

---

## Stampede Protection — Detay

HybridCache'in en değerli özelliği. Nasıl çalıştığına bakalım.

500 paralel istek aynı key'i istiyor, cache miss yaşanıyor. Naif implementasyonda 500 paralel factory çağrısı olur. HybridCache içeride bir koordinasyon yapıyor:

1. İlk istek factory'yi başlatıyor (DB'ye gidiyor)
2. 2-500. istekler aynı key için bekliyor (lock veya semaphore benzeri)
3. İlk istek DB'den döner, cache'e yazar
4. 2-500. istekler **cache'ten alıyorlar** — DB'ye gitmiyorlar
5. Sadece 1 DB sorgusu, 500 yanıt

Bu koordinasyon **per-instance** (her uygulama sunucusunda ayrı) yapılıyor. Distributed coordination yok — yani 3 instance varsa toplamda 3 DB sorgusu olabilir, ama her instance kendi içinde 1.

Üç paralel DB sorgusu kabul edilebilir bir maliyet. 500 değil.

---

## Serialization

HybridCache verileri JSON olarak serialize ediyor varsayılan olarak. Performans için MessagePack veya custom serializer kullanmak istersen:

```csharp
builder.Services.AddHybridCache()
    .AddSerializerFactory<MessagePackSerializerFactory>();
```

JSON çoğu durumda yeterli. MessagePack daha hızlı ve daha küçük payload üretir — yüksek RPS'de fark eder.

**Önemli:** Cache'lediğin nesneler serializable olmalı. Karmaşık nesneler (entity, circular reference) problem olabilir. DTO'ları cache'le, entity'leri değil.

---

## Pratik Senaryolar

### Kategori Listesi

Nadiren değişen, sık okunan veri. Tipik HybridCache senaryosu:

```csharp
public async Task<List<Kategori>> GetAllAsync(CancellationToken ct = default)
{
    return await _cache.GetOrCreateAsync(
        "kategoriler",
        async cancel => await _repo.GetAllAsync(cancel),
        tags: new[] { "kategoriler" },
        cancellationToken: ct);
}

// Yeni kategori eklenince:
public async Task EkleAsync(KategoriDto dto)
{
    await _repo.AddAsync(dto);
    await _cache.RemoveByTagAsync("kategoriler");
    // ne yapar → tüm kategori listesi cache'i temizlenir
}
```

### Kullanıcıya Özel Veri

Per-user cache. L1'de tutmak özellikle değerli — aynı kullanıcının sonraki istekleri aynı instance'a gelirse L1'den anında döner.

```csharp
public async Task<KullaniciProfil> GetProfilAsync(int userId)
{
    return await _cache.GetOrCreateAsync(
        $"profil:{userId}",
        async ct => await _repo.GetProfilAsync(userId, ct),
        tags: new[] { $"user:{userId}" });
}

// Kullanıcı profili güncellendiğinde:
public async Task GuncelleAsync(int userId, ProfilDto dto)
{
    await _repo.UpdateAsync(userId, dto);
    await _cache.RemoveByTagAsync($"user:{userId}");
}
```

### Cache'lememesi Gereken Şeyler

**Sürekli değişen veri:** Stok sayısı, canlı fiyat, gerçek zamanlı veri. Cache'te eski göstermenin maliyeti yüksek.

**Kullanıcıya özel kritik veri:** Bakiye gibi para işlemleri. Cache hatası para kaybına yol açar — direkt DB'den oku.

**Çok küçük veri:** 200 byte'lık veri için cache overhead'i (serialization, network) anlamlı tasarruf sağlamaz.

**Tek seferlik veri:** Bir kullanıcının yalnızca bir kez erişeceği veri — cache'in amacı tekrarlı erişim.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de cache yok — her istek DB'ye gidiyor. 500 kullanıcıda sorun değil ama:
- Kategori listesi gibi nadiren değişen veri her sayfa görüntülemede DB'ye gidiyor
- Aynı veri saniyede yüzlerce kez sorgulanıyor — gereksiz yük

50K kullanıcıda HybridCache ile:
- Kategori listesi 30 dakika cache'te → DB yükü %99 azalır
- L1 (memory) yerel hız (0ms)
- L2 (Redis) instance'lar arası tutarlılık
- Stampede protection — kategoriler tablosu cache miss'te 1000 paralel sorgu yemiyor

---

## 500 vs 50K Kullanıcı

| Cache Stratejisi | 500 kullanıcı/ay | 50K kullanıcı/ay |
|------------------|-------------------|-------------------|
| IMemoryCache | Yeterli (tek instance) | Tutarsız — geçilmeli |
| IDistributedCache (Redis) | Gereksiz | Temel ihtiyaç için yeterli |
| HybridCache | Overengineering | Yüksek RPS endpoint'lerde ideal |
| Output Cache (HTTP) | Yeterli (static endpoint) | API endpoint'lerde değerli |
| Manuel stampede protection | Gereksiz | HybridCache kullanırsan otomatik |
| Tag-based invalidation | Az veri, manuel kolay | Çok cache key — tag zorunlu |

---

## Kontrol Soruları

1. L1 ve L2 cache arasındaki temel fark nedir? Hangisi daha hızlı, hangisi daha tutarlı?
2. Cache stampede ne demek? HybridCache bunu nasıl önlüyor?
3. HybridCache'te `Expiration` ile `LocalCacheExpiration` arasındaki fark nedir? Neden L1 daha kısa olmalı?
4. Tag-based invalidation hangi senaryoda kritik? Tek key silmekten neden üstün?
5. HybridCache hangi durumlarda IMemoryCache yerine tercih edilmeli?
6. Cache'lenmemesi gereken veri türleri nelerdir? Neden?
7. Multi-instance ortamda L1 cache nasıl tutarsızlık yaratabilir? HybridCache bunu nasıl yönetir?
