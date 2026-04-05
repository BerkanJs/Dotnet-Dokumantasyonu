# Gün 6 — Async/Await ve Task

---

## 1. Neden Async?

Bir web API yazdın. İstek geldi, veritabanından veri çekiyorsun. Bu işlem 50ms sürüyor.

**Sync (senkron) durumda:** O thread 50ms boyunca veritabanından cevap bekliyor. Beklerken başka hiçbir şey yapamıyor. 100 eş zamanlı istek gelirse 100 thread bekler — bu thread'lerin hepsi RAM ve CPU tüketir.

**Async durumda:** Thread veritabanına isteği gönderir, "cevap gelince beni haberdar et" der ve serbest kalır. Cevap gelene kadar başka isteklere hizmet verir. 100 eş zamanlı istek çok daha az thread ile karşılanır.

İşte `async/await` bu mekanizmanın C#'taki yazımı.

---

## 2. async/await Nasıl Çalışır?

```csharp
// Sync versiyon — thread bekler
public KitapDto KitapGetir(int id)
{
    var kitap = db.Kitaplar.Find(id);  // 50ms bekliyor, thread bloklu
    return new KitapDto(kitap.Baslik, kitap.Fiyat);
}

// Async versiyon — thread serbest kalır
public async Task<KitapDto> KitapGetirAsync(int id)
{
    var kitap = await db.Kitaplar.FindAsync(id);  // beklerken thread serbest
    return new KitapDto(kitap.Baslik, kitap.Fiyat);
}
```

`await` kelimesi "bu işlem bitene kadar burada dur, ama thread'i bloklamadan" demek. Thread serbest kalır, başka iş yapar, iş bitince buradan devam eder.

**Kurallar:**
- `await` kullanmak istiyorsan metot `async` olmalı
- `async` metot `Task`, `Task<T>` veya `ValueTask<T>` döndürür
- `async void` sadece event handler için — neden tehlikeli aşağıda

---

## 3. Task Nedir?

`Task`, "devam eden veya tamamlanmış bir işi" temsil eder. JavaScript'teki `Promise` gibi düşün.

```csharp
Task        →  sonuç döndürmeyen async işlem  (void gibi)
Task<T>     →  T tipinde sonuç döndüren async işlem
```

```csharp
// Task<string> — string döndürecek bir iş
Task<string> gorev = HttpClient.GetStringAsync("https://api.example.com/kitaplar");

// await ile sonucu al
string json = await gorev;
```

---

## 4. Go ile Karşılaştırma

Go'da `goroutine` kullandın. Fark şu:

- **Go goroutine:** Çok lightweight, runtime tarafından yönetilen "green thread". Başlatmak neredeyse bedava.
- **C# Task:** ThreadPool üzerinde çalışır. Goroutine kadar lightweight değil ama async I/O ile thread sayısını düşük tutarsın.

Go'da `channel` ile goroutine'ler arası veri taşırsın. C#'ta benzer iş için `Channel<T>` var. Pratikte web API yazarken buna ihtiyaç duymuyorsun — `async/await` yeterli.

---

## 5. Deadlock — .Result ve .Wait() Neden Yasak?

Bu en kritik noktalardan biri. Yanlış kullanım uygulamayı kilitler.

```csharp
// YANLIŞ — deadlock riski
public string KitapGetir(int id)
{
    return KitapGetirAsync(id).Result;  // async metodu sync bekliyorsun
}
```

Ne olur?

1. `KitapGetirAsync` başladı, `await` noktasında durdu
2. `.Result` mevcut thread'i blokluyor — "bitti mi?" diye bekliyor
3. `await` tamamlanınca devam etmek için o thread'e dönmek istiyor
4. Ama o thread `.Result` ile bloklu — iki taraf birbirini bekliyor → **deadlock**

**Kural:** Async metodu çağırıyorsan `await` kullan. `.Result` veya `.Wait()` kullanma.

```csharp
// DOĞRU
public async Task<string> KitapGetir(int id)
{
    return await KitapGetirAsync(id);
}
```

ASP.NET Core'da tüm pipeline async. Zinciri kırma.

---

## 6. async void — Neden Tehlikeli?

```csharp
// TEHLİKELİ
public async void KitapSil(int id)
{
    await db.Kitaplar.FindAsync(id);
    // ...
}
```

`async void` şunları yapamaz:
- `await` edilemez — kim beklediğini bilemez
- İçinden fırlayan exception yakalanmaz, uygulama çöker

**Ne zaman kullanılır?** Sadece WinForms/WPF event handler'larında. Web geliştirmede hiç kullanma.

```csharp
// DOĞRU
public async Task KitapSilAsync(int id) { ... }
```

---

## 7. CancellationToken — İptal Mekanizması

Kullanıcı bir istek attı, sonra bağlantıyı kesti. Sunucu hâlâ veritabanını sorguluyor — gereksiz iş yapıyor.

`CancellationToken` bunu çözer. "İptal sinyali gelirse dur" mekanizması.

```csharp
public async Task<List<KitapDto>> KitaplariGetirAsync(CancellationToken cancellationToken)
{
    // cancellationToken veritabanı sorgusuna geçildi
    // bağlantı kesilirse sorgu iptal edilir
    var kitaplar = await db.Kitaplar
        .Select(k => new KitapDto(k.Baslik, k.Fiyat))
        .ToListAsync(cancellationToken);

    return kitaplar;
}
```

ASP.NET Core controller'da `CancellationToken` parametresini eklersen framework onu otomatik doldurur:

```csharp
[HttpGet]
public async Task<IActionResult> GetKitaplar(CancellationToken cancellationToken)
{
    var kitaplar = await _kitapServis.KitaplariGetirAsync(cancellationToken);
    return Ok(kitaplar);
}
```

Kullanıcı browser'ı kapatırsa token iptal sinyali alır, sorgu durur. Veritabanı ve CPU boşuna çalışmaz.

---

## 8. ConfigureAwait(false) — Ne Zaman Lazım?

`await` tamamlandığında CLR "nereden devam edeyim?" diye sorar. ASP.NET Core'da bu genellikle önemli değil — SynchronizationContext yok.

```csharp
// Kütüphane kodu yazıyorsan — ConfigureAwait(false) ekle
var data = await HttpClient.GetStringAsync(url).ConfigureAwait(false);

// ASP.NET Core uygulama kodunda — genellikle gerekli değil
var data = await HttpClient.GetStringAsync(url);
```

**Kural:**
- Kütüphane yazıyorsan → `ConfigureAwait(false)` ekle
- ASP.NET Core uygulama kodunda → gerekli değil, ekleme zorunlu değil

---

## 9. ValueTask\<T\> — Kısaca

`Task<T>` her zaman heap'te bir nesne oluşturur. Çok sık çağrılan metodlarda bu birikerek GC baskısı yaratır.

`ValueTask<T>` bunu önler — sonuç zaten hazırsa allocation yapmaz.

```csharp
// Çok sık çağrılan, genellikle cache'den dönen metot
public ValueTask<KitapDto> KitapGetirAsync(int id)
{
    if (_cache.TryGetValue(id, out var kitap))
        return ValueTask.FromResult(kitap);  // allocation yok

    return new ValueTask<KitapDto>(VeritabanindenGetirAsync(id));
}
```

**Ne zaman kullanırsın?** Servis kodu yazarken genellikle `Task<T>` yeterli. Performans kritik, çok sık çağrılan metodlarda `ValueTask<T>` düşün. Faz 4'te ölçeceğiz.

---

## 10. Birden Fazla İşi Paralel Başlatmak

Bazen birden fazla bağımsız async işi aynı anda başlatmak istersin:

```csharp
// YAVAŞ — sırayla: 300ms + 200ms = 500ms
var kitaplar = await _kitapServis.GetirAsync();    // 300ms
var kategoriler = await _kategoriServis.GetirAsync(); // 200ms

// HIZLI — paralel: max(300ms, 200ms) = 300ms
var kitaplarGorev = _kitapServis.GetirAsync();
var kategorilerGorev = _kategoriServis.GetirAsync();

await Task.WhenAll(kitaplarGorev, kategorilerGorev);

var kitaplar = kitaplarGorev.Result;     // burada .Result güvenli — await tamamlandı
var kategoriler = kategorilerGorev.Result;
```

`Task.WhenAll` her iki işi paralel başlatır, ikisi de bitince devam eder. İki bağımsız veritabanı sorgusu varsa toplam süre uzun olanın süresi kadar olur.

---

## 11. Web Geliştirmede Özet

- **Her veritabanı, HTTP, dosya işlemi** → `async/await` kullan
- **Metot imzası** → `async Task<T>` veya `async Task`
- **.Result / .Wait()** → hiç kullanma, deadlock riski
- **async void** → hiç kullanma
- **CancellationToken** → controller'dan servise, servisten repository'e geçir
- **Paralel bağımsız işler** → `Task.WhenAll`

---

## 12. Kontrol Soruları

1. Async/await olmadan 100 eş zamanlı istek geldiğinde ne olur?

ilk thread gerçeklesene kadar 99 tanesi bekler (zincirlikuyu metrobüs saat 17:30)

2. `.Result` neden deadlock'a yol açabilir?

await edilmesi gerekiyor async method kullanıldıysa 
.Result, async kodu senkron blokladığı için özellikle context olan ortamlarda deadlock’a yol açabilir.
3. `async void` neden tehlikelidir? Fark ne?

error handling yapılamaz async döngüyü kırarsın
Exception yakalanamaz
try/catch işlemez
Await edilemez
kontrol kaybı
Fire-and-forget
lifecycle yönetilemez

4. `CancellationToken` olmadan kullanıcı bağlantıyı keserse ne olur?

CancellationToken yoksa, client bağlantıyı kesse bile server işlemi çalışmaya devam eder.

5. İki bağımsız veritabanı sorgusu var, ikisi de 200ms sürüyor. Sırayla çalıştırırsan toplam kaç ms? `Task.WhenAll` ile çalıştırırsan?

400 sürer whenall ile 200 ms sürer