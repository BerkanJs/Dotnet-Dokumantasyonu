# Gün 77 — Async Performans: ValueTask ve IAsyncEnumerable

Async kodun doğru yazılması hem allocation'ı hem thread kullanımını etkiler.

---

## ValueTask\<T\> — Synchronous Path'te Allocation Sıfır

**Ne işe yarar?** `Task<T>` her zaman heap'e bir nesne yazar. Ama sonuç zaten hazırsa (cache hit gibi) bu nesneyi oluşturmak gereksizdir. `ValueTask<T>` bir struct'tır — sonuç hazırsa heap'e gitmez.

```
Task<T>      → her zaman heap'e gider, GC takip eder
ValueTask<T> → sonuç hazırsa stack'te kalır, heap yok
               sonuç hazır değilse → Task'a döner, normal async
```

```csharp
private readonly Dictionary<int, Kitap> _cache = new();

// Task<T> — cache'te olsa bile her çağrıda heap allocation
public async Task<Kitap?> GetByIdAsync(int id)
{
    if (_cache.TryGetValue(id, out var kitap))
        return kitap;                               // Task nesnesi yine de oluştu
    return await _db.Kitaplar.FindAsync(id);
}

// ValueTask<T> — cache hit'te sıfır allocation
public ValueTask<Kitap?> GetByIdAsync(int id)
{
    if (_cache.TryGetValue(id, out var kitap))
        return ValueTask.FromResult(kitap);         // struct — heap allocation yok
        // bunu Task.FromResult yapsaydık → cache hit'te bile heap nesne oluşur

    return new ValueTask<Kitap?>(_db.Kitaplar.FindAsync(id).AsTask());
    // async path → normal Task'a sarılır
}
```

**Kritik kısıt:** `ValueTask<T>` yalnızca bir kez await edilebilir.

```csharp
var vt = GetByIdAsync(1);
var k1 = await vt;      // ✓
var k2 = await vt;      // ✗ — tanımsız davranış, ikinci await yapma
```

**Ne zaman ValueTask?** Metodun çoğunlukla cache'ten veya sync olarak döndüğü hot path'lerde. Her zaman async olan metodlarda gereksiz karmaşıklık — orada `Task<T>` yeterli.

---

## IAsyncEnumerable\<T\> — Streaming

**Ne işe yarar?** Normalde `ToListAsync()` tüm veriyi belleğe yükler. 100k kayıt varsa 100k nesne aynı anda heap'te. `IAsyncEnumerable<T>` ise kayıtları birer birer, hazır olunca verir — bellekte her an sadece bir kayıt bulunur.

```
ToListAsync()          → [k1, k2, k3, ... k100000] hepsi bellekte
IAsyncEnumerable<T>    → k1 geldi → işle → k2 geldi → işle → ...
```

```csharp
// Kötü — 100k kayıt tek seferde belleğe yükleniyor
public async Task<List<Kitap>> GetHepsiniAsync()
{
    return await _db.Kitaplar.ToListAsync();    // 100k nesne aynı anda heap'te
}

// İyi — her kayıt gelince işle
public async IAsyncEnumerable<Kitap> StreamAsync(
    [EnumeratorCancellation] CancellationToken ct)
    // [EnumeratorCancellation] → await foreach içindeki ct buraya iletilir
    // bunu yazmasaydık → iptal sinyali stream'e ulaşmaz
{
    await foreach (var kitap in _db.Kitaplar.AsAsyncEnumerable().WithCancellation(ct))
    {
        yield return kitap;     // caller hazır olunca bir sonrakini ver
    }
}

// Tüketim
await foreach (var kitap in repo.StreamAsync(ct))
{
    await RaporaEkleAsync(kitap);   // her kitap gelince işle — hepsi bitmesini bekleme
}
```

**Kitabevi senaryosu:** Tüm siparişleri Excel'e export etmek. 200k satır varsa `ToListAsync()` sunucuyu çökertir — `IAsyncEnumerable` ile satır satır yazar, bellek sabit kalır.

---

## ConfigureAwait(false) — Library Kodu İçin

**Ne işe yarar?** Bir `await` tamamlandıktan sonra devam eden kod, varsayılan olarak orijinal thread context'ine (SynchronizationContext) döner. Bu ASP.NET Core'da sorun çıkarmaz ama kütüphane yazarken deadlock riskine yol açabilir.

```
await sonrası:
  ConfigureAwait(true)  [varsayılan] → orijinal context'e dön (UI thread, ASP context)
  ConfigureAwait(false)              → herhangi bir thread'de devam et, context'e dönme
```

```csharp
// Uygulama kodu (ASP.NET Controller, Blazor) → yazman gerekmez
public async Task<IActionResult> Get()
{
    var kitap = await _repo.GetByIdAsync(1);    // context otomatik yönetilir
    return Ok(kitap);
}

// Kütüphane / NuGet paketi kodu → her await'e ekle
public async Task<string> DosyaOkuAsync(string path)
{
    var icerik = await File.ReadAllTextAsync(path).ConfigureAwait(false);
    // false → kütüphane hangi context'te çalıştırılırsa çalıştırılsın deadlock olmaz
    // bunu yazmasaydık → WPF veya Windows Forms uygulamasında kullanılınca deadlock riski
    return icerik;
}
```

**Kural:** Kütüphane yazıyorsan her `await`'e `.ConfigureAwait(false)` ekle. Uygulama yazıyorsan gerek yok.

---

## ConcurrentDictionary — Thread-Safe In-Memory Cache

**Ne işe yarar?** Normal `Dictionary<K,V>` aynı anda birden fazla thread tarafından yazılırsa veri bozulur. `ConcurrentDictionary` bunu kilitsiz (lock-free) algoritmalarla çözer.

```csharp
// Dictionary → thread-safe değil
private static readonly Dictionary<int, Kitap> _cache = new();
// iki thread aynı anda yazarsa → exception veya bozuk veri

// ConcurrentDictionary → thread-safe
private static readonly ConcurrentDictionary<int, Kitap> _cache = new();

public Kitap GetOrAdd(int id, Func<int, Kitap> factory)
{
    return _cache.GetOrAdd(id, factory);
    // varsa döner, yoksa factory çağırıp ekler — thread-safe
    // bunu lock + Dictionary ile yazsaydık → aynı sonuç ama daha yavaş, manuel kilit
}
```

**Dikkat:** `GetOrAdd`'in factory'si eş zamanlı birden fazla kez çağrılabilir. Pahalı işlemler (DB sorgusu) için `Lazy<T>` ile sarmallanmalı.

---

## Channel\<T\> — Producer/Consumer Kuyruğu

**Ne işe yarar?** Bir taraf veri üretir (producer), diğer taraf işler (consumer). `ConcurrentQueue` ile yapılabilir ama `Channel<T>` buna async bekleme ve backpressure (dolunca dur) ekler.

```
Producer → [Channel buffer] → Consumer
            kapasite dolunca
            producer bekler  ← backpressure
```

```csharp
// Bounded channel — maksimum 100 eleman, dolunca producer bekler
var channel = Channel.CreateBounded<Siparis>(capacity: 100);
// Unbounded yazsaydık → producer durdurulamaz, bellek sonsuza büyür

// Producer — siparişleri kuyruğa ekle
await channel.Writer.WriteAsync(yeniSiparis, ct);
// dolu ise → WriteAsync burada bekler (backpressure)

// Consumer — ayrı bir task'ta çalışır
await foreach (var siparis in channel.Reader.ReadAllAsync(ct))
{
    await IsleAsync(siparis);
}
// channel.Writer.Complete() çağrılınca ReadAllAsync biter
```

**Kitabevi senaryosu:** Sipariş gelince önce kuyruğa at, ayrı bir worker hızına göre işlesin. Peak saatinde sipariş servisini patlatmadan yükü düzenlemiş olursun.

---

## Özet Tablo

| Araç | Ne Zaman |
|---|---|
| `ValueTask<T>` | Çoğunlukla cache'ten dönen, sync tamamlanan metotlar |
| `IAsyncEnumerable<T>` | Büyük veri seti — belleğe sığmaz veya sığmamalı |
| `ConfigureAwait(false)` | Kütüphane / NuGet paketi yazarken |
| `ConcurrentDictionary` | Birden fazla thread'in okuduğu/yazdığı cache |
| `Channel<T>` | Producer/consumer, backpressure kontrolü gerektiğinde |

---

## Kontrol Soruları

1. `ValueTask<T>` birden fazla kez await edilirse ne olur?
2. `IAsyncEnumerable` yerine `List` döndürmek ne zaman daha mantıklıdır?
3. ASP.NET Core'da `ConfigureAwait(false)` yazmazsan ne olur?
4. `Channel.CreateBounded` ile `CreateUnbounded` arasında neden bounded tercih edilir?
