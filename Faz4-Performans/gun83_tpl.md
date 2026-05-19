# Gün 83 — Task Parallel Library (TPL) Derinlemesine

---

## TPL Nedir?

Task Parallel Library, .NET'in paralel ve eşzamanlı iş yürütme altyapısıdır. `Task`, `async/await`, `Parallel.For` gibi tanıdık yapıların hepsi TPL üzerine kuruludur.

Bu gün iki farklı kavramı birbirinden ayırt ederek başlayalım:

- **Concurrency (eşzamanlılık):** Birden fazla iş aynı anda *başlatılır* ama aynı anda *çalışmak zorunda değildir*. Async/await buna örnek — thread beklerken başkasına geçilir.
- **Parallelism (paralellik):** Birden fazla iş gerçekten *aynı anda* farklı CPU çekirdeklerinde çalışır. `Parallel.For` buna örnek.

---

## Parallel.For ve Parallel.ForEach — CPU Bound İşler

CPU'yu yoğun kullanan bir işlemi döngüyle yapıyorsan — görüntü işleme, hesaplama, büyük veri dönüşümü — `Parallel.For` bu işi tüm CPU çekirdeklerine dağıtır.

Gerçek dünya benzetmesi: 1000 kitabın fiyatını tek kasiyerle güncellemek yerine 8 kasiyer aynı anda 8 kitaba bakar.

```csharp
var kitaplar = new List<Kitap>(/* 10000 kitap */);

// Normal döngü — tek CPU çekirdeği
foreach (var kitap in kitaplar)
{
    kitap.Fiyat = HesaplaYeniFiyat(kitap);  // sırayla, birer birer
}

// Parallel.ForEach — tüm çekirdekler kullanılır
Parallel.ForEach(kitaplar, kitap =>
{
    kitap.Fiyat = HesaplaYeniFiyat(kitap);
    // her iterasyon farklı thread'de çalışabilir — sıra garantisi yok
    // bunu yazmasaydık → tek thread, 8 çekirdekli makinede 7 çekirdek boşta bekler
});
```

**Önemli kısıt:** Döngü içinde ortak bir koleksiyona yazma yapıyorsan race condition olur. Her iterasyon bağımsız çalışmalı.

```csharp
// Yanlış — List thread-safe değil
var sonuclar = new List<decimal>();
Parallel.ForEach(kitaplar, k => sonuclar.Add(k.Fiyat));   // race condition

// Doğru — ConcurrentBag thread-safe
var sonuclar = new ConcurrentBag<decimal>();
Parallel.ForEach(kitaplar, k => sonuclar.Add(k.Fiyat));
```

**Ne zaman Parallel.ForEach, ne zaman async foreach?**  
Hesaplama ağırlıklı (CPU bound) → `Parallel.ForEach`  
Bekleme ağırlıklı (I/O bound, DB, HTTP) → `async/await` ile `Task.WhenAll`

---

## PLINQ — Paralel LINQ

PLINQ, normal LINQ sorgusuna `.AsParallel()` ekleyerek sorguyu otomatik olarak paralel çalıştırır.

```csharp
var kitaplar = Enumerable.Range(1, 100000).Select(i => new Kitap { Id = i, Fiyat = i * 1.5m });

// Normal LINQ — tek thread
var pahalılar = kitaplar
    .Where(k => k.Fiyat > 1000)
    .Select(k => k.Id)
    .ToList();

// PLINQ — paralel
var pahalılar = kitaplar
    .AsParallel()                   // buradan itibaren paralel çalışır
    .Where(k => k.Fiyat > 1000)
    .Select(k => k.Id)
    .ToList();
    // bunu küçük listede kullansaydın → paralel kurulum overhead'i işin kendisinden uzun sürer
```

**Ne zaman PLINQ?**  
Büyük koleksiyonda CPU ağırlıklı filtreleme/dönüşüm varsa ve sıra önemli değilse.  
Küçük listede veya I/O bound işlemde → overhead yüzünden normal LINQ'dan yavaş olabilir.

Sırayı korumak istersen:
```csharp
.AsParallel().AsOrdered()   // sıra korunur ama performans düşer
```

---

## Task.WhenAll ve Task.WhenAny

`Task.WhenAll`: birden fazla async işi aynı anda başlat, hepsi bitince devam et.  
`Task.WhenAny`: birden fazla async işi aynı anda başlat, ilk biten yeterliyse devam et.

```csharp
// Yanlış — sırayla bekleme, her istek öncekini bekliyor
var kitap    = await _kitapRepo.GetAsync(id);       // 200ms bekle
var yorumlar = await _yorumRepo.GetAsync(id);       // sonra 150ms bekle
var stok     = await _stokRepo.GetAsync(id);        // sonra 100ms bekle
// toplam: 450ms

// Doğru — hepsini aynı anda başlat
var kitapTask    = _kitapRepo.GetAsync(id);
var yorumlarTask = _yorumRepo.GetAsync(id);
var stokTask     = _stokRepo.GetAsync(id);

await Task.WhenAll(kitapTask, yorumlarTask, stokTask);
// toplam: ~200ms (en uzun olan kadar)
// bunu WhenAll ile yazmasaydık → 450ms beklenirdi

var kitap    = await kitapTask;
var yorumlar = await yorumlarTask;
var stok     = await stokTask;
```

`Task.WhenAny` — timeout senaryosu:

```csharp
var islemTask  = UzunSurenIslemAsync();
var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

var biten = await Task.WhenAny(islemTask, timeoutTask);

if (biten == timeoutTask)
    throw new TimeoutException("İşlem 5 saniyeyi aştı");

var sonuc = await islemTask;    // zaten bitmişti, hemen döner
```

---

## SemaphoreSlim — Eşzamanlı İstek Sınırı

Semaphore, "aynı anda kaç thread içeri girebilir" sorusunu cevaplar. `lock` sadece 1'e izin verirken `SemaphoreSlim` istediğin sayıya izin verir.

Gerçek dünya benzetmesi: otopark girişi — kapasitesi 10 araç. 11. araç kapıda bekler, içeriden biri çıkınca içeri alınır.

```csharp
// Dış API'ye aynı anda en fazla 5 istek gönder — rate limit aşma
private readonly SemaphoreSlim _limit = new SemaphoreSlim(5, 5);
// (initialCount: 5, maxCount: 5) → 5 slot açık başlıyor

public async Task<string> ApiCagirAsync(string url, CancellationToken ct)
{
    await _limit.WaitAsync(ct);
    // slot varsa hemen geçer, yoksa slot açılana kadar bekler
    // bunu yazmasaydık → aynı anda 1000 istek → API rate limit hatası
    try
    {
        return await _httpClient.GetStringAsync(url, ct);
    }
    finally
    {
        _limit.Release();   // slot'u serbest bırak — sıradaki bekleyen girebilir
        // bunu yazmasaydık → slot hiç geri verilmez, zamanla tüm slotlar dolar
    }
}
```

---

## TaskCompletionSource — Event'i Task'a Çevir

Bazı kütüphaneler callback veya event tabanlı çalışır — async/await ile kullanmak için `TaskCompletionSource` ile sarmallanır.

```csharp
// Callback tabanlı eski bir kütüphane
public class EskiSistem
{
    public event Action<string>? VeriGeldi;
    public void VeriTalep(int id) { /* async başlatır, VeriGeldi event'i tetikler */ }
}

// TaskCompletionSource ile async/await uyumlu hale getir
public Task<string> VeriGetirAsync(int id)
{
    var tcs = new TaskCompletionSource<string>();
    // tcs.Task → henüz tamamlanmamış bir Task — dışarıya bunu veriyoruz

    _eskiSistem.VeriGeldi += sonuc =>
    {
        tcs.SetResult(sonuc);   // event tetiklenince Task tamamlanır
        // SetResult → await eden taraf buradan devam eder
    };

    _eskiSistem.VeriTalep(id);
    return tcs.Task;            // caller bunu await eder
}

// Kullanım — artık async/await ile:
var veri = await VeriGetirAsync(42);
```

---

## CancellationTokenSource.CreateLinkedTokenSource

Birden fazla iptal sinyalini birleştirir — herhangi biri iptal edince tüm işlemler iptal olur.

```csharp
// Kullanıcı isteği iptal etti (HTTP bağlantısı kesildi)
// ve uygulama kapanıyor — her iki durumda da işlemi iptal et
public async Task IsleAsync(CancellationToken httpCt)
{
    using var appCts = new CancellationTokenSource();
    // uygulama kapanınca tetiklenecek token

    using var linked = CancellationTokenSource.CreateLinkedTokenSource(httpCt, appCts.Token);
    // linked.Token → httpCt VEYA appCts iptal edince tetiklenir

    await UzunIslemAsync(linked.Token);
    // bunu sadece httpCt ile yapsaydık → uygulama kapanırken işlem devam ederdi
}
```

---

## Özet — Hangi Araç Ne Zaman?

| Araç | Ne Zaman |
|---|---|
| `Parallel.ForEach` | CPU bound, büyük koleksiyon, bağımsız iterasyonlar |
| `PLINQ` | CPU bound LINQ sorgusu, büyük veri seti |
| `Task.WhenAll` | Birden fazla async işi paralel başlat, hepsi bitsin |
| `Task.WhenAny` | İlk biten yeterli (timeout, yarış) |
| `SemaphoreSlim` | Eşzamanlı erişimi sayıyla sınırla |
| `TaskCompletionSource` | Callback/event tabanlı kodu async'e çevir |
| `CreateLinkedTokenSource` | Birden fazla iptal sinyalini birleştir |

---

## Kontrol Soruları

1. `Parallel.ForEach` içinde `List<T>`'ye `.Add()` yapmak neden tehlikeli?
2. `Task.WhenAll` ile sırayla await arasındaki süre farkını hangi senaryoda önemli?
3. `SemaphoreSlim(5, 5)`'te `finally` bloğunda `Release()` çağrılmazsa ne olur?
4. `TaskCompletionSource` hangi tür problemleri çözer?
