# Gün 78 — Memory Leak Tespiti ve Çözümü

.NET'te GC olduğu için memory leak olmaz sanılır. Olmaz değil — GC yalnızca erişilemeyen nesneleri temizler. Erişilebilir ama bir daha kullanılmayacak nesneler bellekte sonsuza dek kalır.

---

## Senaryo 1 — Static Koleksiyona Ekleme, Çıkarma Yok

**Sorun:** Static alan uygulama ömründe yaşar. Buraya eklenen her nesne asla temizlenmez.

```csharp
public static class IslemGecmisi
{
    // static list → uygulama kapanana kadar bellekte
    private static readonly List<string> _loglar = new();

    public static void Ekle(string mesaj)
    {
        _loglar.Add(mesaj);     // ekleniyor ama hiç silinmiyor
        // 10k istek/sn → 10k string/sn → liste sonsuza büyür
    }
}
```

**Çözüm:** Sabit boyutlu yapı kullan.

```csharp
// Son 1000 logu tut, eskiyi at
private static readonly Queue<string> _loglar = new();

public static void Ekle(string mesaj)
{
    if (_loglar.Count >= 1000)
        _loglar.Dequeue();      // eskiyi çıkar
    _loglar.Enqueue(mesaj);
}
```

---

## Senaryo 2 — Event Handler Unsubscribe Edilmemiş

**Sorun:** Event'e subscribe olan nesne, event sahibi yaşadığı sürece GC tarafından temizlenemez. Event sahibi uzun yaşıyorsa (static, singleton) subscriber sonsuza kalır.

```csharp
public class SiparisServisi
{
    public event EventHandler? SiparisOlusturuldu;  // long-lived servis
}

public class BildirimGonderici
{
    public BildirimGonderici(SiparisServisi servis)
    {
        servis.SiparisOlusturuldu += OnSiparisOlusturuldu;
        // SiparisServisi → BildirimGonderici'yi referansla tutar
        // BildirimGonderici dispose edilse bile GC onu temizleyemez
    }

    private void OnSiparisOlusturuldu(object? sender, EventArgs e) { }

    // Çözüm: IDisposable implement et, Dispose'da unsubscribe yap
    public void Dispose()
    {
        _servis.SiparisOlusturuldu -= OnSiparisOlusturuldu;  // referansı kes
    }
}
```

---

## Senaryo 3 — IDisposable Dispose Edilmemiş

**Sorun:** `DbConnection`, `HttpClient`, `FileStream` gibi unmanaged kaynak tutan nesneler `Dispose` edilmezse kaynak sızıntısı olur.

```csharp
// Kötü — her istekte yeni connection, hiç kapatılmıyor
public async Task<Kitap?> GetAsync(int id)
{
    var connection = new SqlConnection(_connStr);   // açıldı
    await connection.OpenAsync();
    // ... sorgu ...
    return kitap;
    // connection.Dispose() çağrılmadı → connection pool tükenir
}

// İyi — using ile garantili Dispose
public async Task<Kitap?> GetAsync(int id)
{
    await using var connection = new SqlConnection(_connStr);
    // bunu yazmasaydık → exception olsa bile Dispose çağrılmazdı
    await connection.OpenAsync();
    // ... sorgu ...
    return kitap;
}   // blok sonunda otomatik Dispose → connection pool'a döner
```

---

## Senaryo 4 — Closure Uzun Yaşayan Nesneyi Yakalar

**Sorun:** Lambda veya delegate, dışarıdaki bir değişkeni "kapar" (closure). O değişken büyük bir nesneye referans içeriyorsa GC temizleyemez.

```csharp
public void BuyukVeriIsle()
{
    var buyukListe = new List<byte[]>(Enumerable.Range(0, 10000)
        .Select(_ => new byte[1024]));  // ~10 MB

    // buyukListe closure içinde yakalandı
    Timer timer = new Timer(_ =>
    {
        Console.WriteLine(buyukListe.Count);    // buyukListe buradan erişiliyor
    }, null, 0, 1000);

    // timer long-lived → buyukListe hiç serbest bırakılmaz
}

// Çözüm: sadece ihtiyacın olan veriyi yakala
public void BuyukVeriIsle()
{
    var buyukListe = new List<byte[]>(...);
    int adet = buyukListe.Count;            // sadece sayıyı al
    buyukListe = null;                      // referansı kes, GC temizleyebilir

    Timer timer = new Timer(_ =>
    {
        Console.WriteLine(adet);            // closure'da büyük nesne yok
    }, null, 0, 1000);
}
```

---

## Senaryo 5 — Cache Sınırsız Büyür

**Sorun:** `MemoryCache` veya `ConcurrentDictionary` bazlı cache'e TTL veya boyut sınırı koymadan ekleme yapılırsa bellek sonsuza büyür.

```csharp
// Kötü — sınırsız büyür
private static readonly ConcurrentDictionary<int, Kitap> _cache = new();

public async Task<Kitap?> GetAsync(int id)
{
    return _cache.GetOrAdd(id, async _ => await _db.Kitaplar.FindAsync(id));
    // her yeni id → cache'e eklenir, hiç çıkarılmaz
    // 1M farklı kitap sorgusu → 1M cache girişi
}

// İyi — IMemoryCache ile boyut ve TTL sınırı
services.AddMemoryCache(opt => opt.SizeLimit = 1000);   // max 1000 giriş

_cache.Set(id, kitap, new MemoryCacheEntryOptions
{
    Size = 1,                                            // bu giriş 1 birim
    SlidingExpiration = TimeSpan.FromMinutes(5)          // 5 dk kullanılmazsa çıkar
});
```

---

## WeakReference\<T\> — GC'nin Temizleyebileceği Referans

**Ne işe yarar?** Normal referans (`Kitap k = ...`) GC'nin nesneyi temizlemesini engeller. `WeakReference<T>` ise engel olmaz — GC bellek gerekince nesneyi temizleyebilir.

```csharp
// Büyük nesneyi WeakReference ile tut — bellek baskısında GC temizler
private readonly WeakReference<byte[]> _bufferRef = new(new byte[1024 * 1024]);

public byte[] GetBuffer()
{
    if (_bufferRef.TryGetTarget(out var buffer))
        return buffer;          // hâlâ hayatta

    // GC temizlemiş → yeniden oluştur
    var yeni = new byte[1024 * 1024];
    _bufferRef.SetTarget(yeni);
    return yeni;
}
```

Büyük ama yeniden oluşturulabilir nesneleri (resim buffer'ı, önbellek) saklamak için idealdir.

---

## GC.GetTotalMemory ve GetGCMemoryInfo

Uygulama içinden anlık bellek durumunu okumak için:

```csharp
// Yaklaşık heap boyutu
long bytes = GC.GetTotalMemory(forceFullCollection: false);
// forceFullCollection: true → önce GC çalıştırır, daha doğru ama pahalı
// Production'da false kullan

Console.WriteLine($"Heap: {bytes / 1024 / 1024} MB");

// Daha detaylı — Gen boyutları
var info = GC.GetGCMemoryInfo();
Console.WriteLine($"Gen0: {info.GenerationInfo[0].SizeAfterBytes / 1024} KB");
Console.WriteLine($"Gen2: {info.GenerationInfo[2].SizeAfterBytes / 1024 / 1024} MB");
// Gen2 büyüyorsa → uzun yaşayan nesneler var, leak şüphesi
```

---

## dotMemory ile Leak Bulma Adımları

1. Uygulamayı başlat, dotMemory'yi bağla
2. İlk snapshot al → başlangıç referansı
3. Leak şüpheli işlemi birkaç kez tekrarla (sipariş oluştur, sil, tekrar oluştur)
4. İkinci snapshot al
5. **"Snapshot comparison"** → iki snapshot arasında artan nesne türlerine bak
6. En çok artan türe tıkla → **"Retention path"** → nesneyi kim tutuyor?
7. Tutanı bul → kodu düzelt

---

## Özet — Leak Senaryoları

| Senaryo | Belirti | Çözüm |
|---|---|---|
| Static koleksiyon | Bellek sürekli artar | Boyut sınırı koy, eski girişi çıkar |
| Event unsubscribe | Dispose edilen nesne bellekte kalır | `Dispose`'da `-=` ile unsubscribe |
| IDisposable | Connection pool tükenir | `using` / `await using` |
| Closure | Büyük nesne beklenmedik süre yaşar | Yalnızca gerekli veriyi yakala |
| Cache | Bellek giderek artar | TTL ve boyut sınırı ekle |

---

## Kontrol Soruları

1. .NET'te GC varken neden memory leak olabilir?
2. Event handler unsubscribe edilmezse neden nesne bellekte kalır?
3. `WeakReference<T>` normal referanstan ne zaman tercih edilir?
4. `GC.GetGCMemoryInfo()` içinde Gen2 boyutu sürekli artıyorsa bu ne anlama gelir?
