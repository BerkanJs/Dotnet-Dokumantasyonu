# Gün 31 — N+1 Problemi ve Çözümleri

---

## 1. N+1 Query Problemi Nedir?

N+1, her ana kayıt için ayrı bir sorgu çalıştırıldığında ortaya çıkan bir performans sorunudur. Toplam sorgu sayısı: **1** (ana liste) **+** **N** (her satır için ayrı sorgu) = N+1.

```
Senaryo: 100 kitabı ve her kitabın yazarını göster

N+1 DAVRANıŞ:
  Sorgu 1: SELECT * FROM Kitaplar                           ← 1 sorgu
  Sorgu 2: SELECT * FROM Yazarlar WHERE Id = 5              ← kitap #1
  Sorgu 3: SELECT * FROM Yazarlar WHERE Id = 12             ← kitap #2
  Sorgu 4: SELECT * FROM Yazarlar WHERE Id = 5              ← kitap #3 (tekrar!)
  ...
  Sorgu 101: SELECT * FROM Yazarlar WHERE Id = 7            ← kitap #100

Toplam: 101 sorgu — 100 kitap için 100 ayrı DB roundtrip!

DOĞRU DAVRANıŞ (Eager Loading):
  Sorgu 1: SELECT k.*, y.* FROM Kitaplar k LEFT JOIN Yazarlar y ON k.YazarId = y.Id

Toplam: 1 sorgu — tek DB roundtrip
```

**Java/Hibernate benzerliği:** JPA'da `@ManyToOne` ilişkisi `FetchType.LAZY` ile tanımlanmışsa, `kitap.getYazar()` her çağrıldığında Hibernate ayrı bir `SELECT` çalıştırır. EF Core'da lazy loading etkinse aynı sorun ortaya çıkar.

---

## 2. Loading Stratejileri

EF Core'da üç farklı yükleme stratejisi vardır:

| Strateji | Ne Zaman Yüklenir? | SQL Sayısı | Konfigürasyon |
|---|---|---|---|
| Eager Loading | Ana sorgu ile birlikte (`Include`) | 1 (veya split) | Varsayılan desteklenir |
| Lazy Loading | Navigation property'e ilk erişimde | N+1 riski | `UseLazyLoadingProxies()` gerekir |
| Explicit Loading | Geliştirici açıkça istediğinde (`LoadAsync`) | Kontrollü | Her zaman kullanılabilir |

---

## 3. Eager Loading — Include()

Navigation property'yi ana sorgu ile birlikte tek SQL'de yükler. Gün 30'da kısmen gördük; burada daha derine inelim.

```csharp
// ─────────────────────────────────────────────────────────────────────
// Temel eager loading
// ─────────────────────────────────────────────────────────────────────
var kitaplar = await _context.Kitaplar   // Kitaplar tablosunu hedef al — henüz SQL yok
    .AsNoTracking()                       // Change Tracker'a kaydetme, sadece oku
                                          // bunu yazmasaydık EF her nesneyi izlerdi → bellek + CPU maliyeti
    .Include(k => k.YazarNavigation)      // Yazarlar tablosunu JOIN et
                                          // bunu yazmasaydık YazarNavigation her kitapta null gelirdi
    .ToListAsync();                       // SQL üret ve çalıştır
// Üretilen SQL:
// SELECT k.*, y.*
// FROM Kitaplar k
// LEFT JOIN Yazarlar y ON k.YazarId = y.Id

// ─────────────────────────────────────────────────────────────────────
// Conditional Include — filtrelenmiş yükleme (EF Core 5+)
// ─────────────────────────────────────────────────────────────────────
var kitaplar = await _context.Kitaplar
    .AsNoTracking()
    .Include(k => k.Yorumlar              // Yorumlar navigation'ını yükle
        .Where(y => y.Onaylandi))         // ama sadece onaylı olanları al
                                          // bunu yazmasaydık silinmiş/spam yorumlar da gelirdi
    .ToListAsync();
// Üretilen SQL:
// LEFT JOIN Yorumlar y ON k.Id = y.KitapId AND y.Onaylandi = 1

// ─────────────────────────────────────────────────────────────────────
// Çoklu Include
// ─────────────────────────────────────────────────────────────────────
var kitaplar = await _context.Kitaplar
    .AsNoTracking()
    .Include(k => k.YazarNavigation)      // 1. JOIN: Yazarlar tablosu
    .Include(k => k.Yorumlar)             // 2. JOIN: Yorumlar tablosu
                                          // iki collection Include yan yana → Cartesian Explosion riski
                                          // bunu yazmasaydık yazar ve yorumlar null gelirdi
                                          // bunu yazıp AsSplitQuery eklemesek 3×100×50 = 15.000 satır dönebilir
    .ToListAsync();
```

**Eager loading ne zaman tercih edilir?**
- Navigation property'nin her zaman gerekli olduğu durumlarda
- Tek bir istek içinde tüm verinin hazır olması gereken durumlarda (API endpoint, view render)

---

## 4. Lazy Loading

Navigation property'ye erişildiği anda EF Core otomatik olarak bir `SELECT` çalıştırır. Kulağa pratik gelir; üretimde ciddi sorunlara yol açar.

### Aktivasyon

```csharp
// Program.cs
// dotnet add package Microsoft.EntityFrameworkCore.Proxies
// bu paketi eklemeseydin UseLazyLoadingProxies() çağrısı derleme hatası verirdi

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)  // veritabanı bağlantısı
           .UseLazyLoadingProxies());        // proxy sınıfları üret
                                             // bunu yazmasaydık navigation'a erişince null gelirdi
                                             // (eager loading olmadan)

// Entity'lerde navigation property virtual olmalı:
public class Kitap
{
    public int Id { get; set; }
    public string Baslik { get; set; } = null!;
    public virtual Yazar? YazarNavigation { get; set; }  // virtual: proxy override edebilsin
                                                          // virtual yazmasaydık lazy loading çalışmazdı,
                                                          // EF Core uyarı fırlatırdı
}
```

### N+1 Nasıl Tetiklenir?

```csharp
// Controller'da — lazy loading aktif, Include YOK
var kitaplar = await _context.Kitaplar.ToListAsync();
// SQL 1: SELECT * FROM Kitaplar  ← tüm liste tek sorguda geldi, iyi görünüyor

foreach (var kitap in kitaplar)                    // 100 kitap var diyelim
{
    var yazarAdi = kitap.YazarNavigation?.Ad;       // navigation property'e ilk erişim
                                                    // EF Core proxy devreye girer, yeni SQL çalıştırır
                                                    // SQL 2:  SELECT * FROM Yazarlar WHERE Id = 5
                                                    // SQL 3:  SELECT * FROM Yazarlar WHERE Id = 12
                                                    // SQL 4:  SELECT * FROM Yazarlar WHERE Id = 5  (aynı yazar tekrar!)
                                                    // ...
                                                    // SQL 101: SELECT * FROM Yazarlar WHERE Id = 7
                                                    // döngü bittiğinde 101 sorgu çalışmış oldu
}

// View'da daha sinsi: Razor template içinde
// @kitap.YazarNavigation.Ad yazıldığında ne kadar sorgu üretildiği
// görünmez, profiler olmadan fark edilmez!
```

### Lazy Loading Neden Üretimde Tehlikelidir?

```
1. Görünmez performans: Sorgu sayısı koda bakarak anlaşılmaz,
   profiler olmadan fark edilmez.

2. DbContext dispose riski:
   var kitap = await _servis.GetKitapAsync(id);
   // ...
   var yazar = kitap.YazarNavigation?.Ad;  // DbContext Scoped → dispose olmuş olabilir
   // → InvalidOperationException veya ObjectDisposedException

3. Async uyumsuzluğu:
   Lazy loading senkronizedir. async/await kodunun içinde
   synchronous DB çağrısı → thread pool tükenmesi riski.

4. Ölçeklenme sorunu:
   10 kullanıcı → fark edilmez
   1.000 kullanıcı → yavaşlama
   10.000 kullanıcı → DB connection pool tükenir
```

**Sonuç:** Lazy loading'i geliştirme/keşif aşamasında kullanabilirsin; üretim kodunda kapalı tut ve her zaman `Include` ile açıkça belirt.

---

## 5. Explicit Loading

Lazy loading'in kontrollü versiyonudur: geliştirici ne zaman, hangi navigation'ın yükleneceğine karar verir.

```csharp
// Ana entity'yi önce getir
var kitap = await _context.Kitaplar.FindAsync(id);
// SQL: SELECT * FROM Kitaplar WHERE Id = @id
// YazarNavigation henüz null — Include kullanılmadı

// ─────────────────────────────────────────────────────────────────────
// Reference navigation yükleme (tekil: Kitap → Yazar)
// ─────────────────────────────────────────────────────────────────────
await _context.Entry(kitap!)              // Change Tracker'dan bu entity'nin kaydını al
    .Reference(k => k.YazarNavigation)    // yüklemek istediğin tekil navigation'ı belirt
                                          // Collection() yazmasaydık derleme hatası alırdık
                                          // (Yazar bir liste değil, tekil nesne)
    .LoadAsync();                         // SQL üret ve çalıştır
// SQL: SELECT * FROM Yazarlar WHERE Id = @yazarId
// Artık kitap.YazarNavigation dolu

// ─────────────────────────────────────────────────────────────────────
// Collection navigation yükleme (çoklu: Kitap → Yorumlar)
// ─────────────────────────────────────────────────────────────────────
await _context.Entry(kitap!)
    .Collection(k => k.Yorumlar)          // çoklu ilişkiyi hedef al (ICollection<Yorum>)
    .Query()                              // IQueryable'a çevir — filtre ekleyebilmek için
                                          // bunu yazmasaydık tüm yorumlar (onaylı/onaysız) yüklenirdi
    .Where(y => y.Onaylandi)             // sadece onaylı yorumları filtrele
    .LoadAsync();                         // SQL üret ve çalıştır
// SQL: SELECT * FROM Yorumlar WHERE KitapId = @id AND Onaylandi = 1

// ─────────────────────────────────────────────────────────────────────
// IsLoaded kontrolü — aynı navigation'ı iki kez yüklemeyi engelle
// ─────────────────────────────────────────────────────────────────────
var entry = _context.Entry(kitap!);

if (!entry.Reference(k => k.YazarNavigation).IsLoaded)  // daha önce yüklendi mi?
    await entry.Reference(k => k.YazarNavigation).LoadAsync();
// bu kontrolü yapmasaydık ve metod iki kez çağrılsaydı
// gereksiz yere aynı SQL iki kez çalışırdı
```

**Explicit loading ne zaman kullanılır?**
- Ana entity yüklendikten sonra **koşullu olarak** navigation'a ihtiyaç duyulduğunda
- Navigation'a filtre uygulamak istediğinde (`Query()` zinciri)
- Lazy loading proxy'si yokken ve `Include` yetersiz kaldığında

---

## 6. AsSplitQuery() — Sınırlar ve Doğru Kullanım

Gün 30'da `AsSplitQuery()`'yi Cartesian Explosion bağlamında gördük. Burada ne zaman işe yaramadığını detaylıca inceleyelim.

### Ne Zaman Yardımcı Olur?

```csharp
var yazarlar = await _context.Yazarlar
    .Include(y => y.Kitaplar)             // 1. collection: Kitaplar
    .Include(y => y.Yorumlar)             // 2. collection: Yorumlar
                                          // iki collection tek JOIN'de birleşirse:
                                          // 3 yazar × 50 kitap × 200 yorum = 30.000 satır ağdan geçer
    .AsSplitQuery()                       // tek büyük JOIN yerine 3 ayrı SQL üret
                                          // bunu yazmasaydık 30.000 satırlık tek sonuç kümesi dönebilirdi
    .ToListAsync();
// Üretilen SQL'ler:
// SQL 1: SELECT * FROM Yazarlar
// SQL 2: SELECT * FROM Kitaplar    WHERE YazarId IN (1, 2, 3)
// SQL 3: SELECT * FROM Yorumlar   WHERE YazarId IN (1, 2, 3)
// Toplam veri: 3 + 50 + 200 = 253 satır
```

### Ne Zaman Yardımcı Olmaz?

```csharp
// ❌ Durum 1: Sayfalama + AsSplitQuery — tutarsız sonuç riski
var kitaplar = await _context.Kitaplar
    .Include(k => k.Yorumlar)
    .AsSplitQuery()                       // 2 ayrı SQL çalışacak
    .Skip(20)                             // SQL 1'de sayfalama uygulanır (10 kitap döner)
    .Take(10)
    .ToListAsync();
// SQL 2 (Yorumlar): hangi 10 kitaba karşılık geleceği belirsiz
// iki sorgu arasında yeni bir satır eklenirse sayfa kayabilir

// ❌ Durum 2: Tek reference navigation — gereksiz ek roundtrip
var kitap = await _context.Kitaplar
    .Include(k => k.YazarNavigation)      // reference (tekil): collection değil
    .AsSplitQuery()                       // 2 ayrı SQL üretir ama kazanç yok
                                          // JOIN tek satır döndürür, Cartesian Explosion olmaz
                                          // AsSplitQuery eklememek daha hızlı olurdu
    .FirstOrDefaultAsync(k => k.Id == id);
```

**Özet kural:**
- `AsSplitQuery()` → **birden fazla collection** navigation include ediliyorsa
- Sayfalama + split query → dikkatli ol, önce test et
- Reference navigation için split query kullanma

---

## 7. Compiled Queries — EF.CompileQuery()

EF Core her sorguyu ilk çalışmada parse eder, expression tree'yi SQL'e çevirir ve önbelleğe alır. Bu işlem milisaniyeler alır. Çok sık çalışan kritik sorgularda bu overhead'i tamamen ortadan kaldırmak için `EF.CompileQuery()` kullanılır.

```csharp
// ─────────────────────────────────────────────────────────────────────
// Normal sorgu — her çağrıda translation overhead var
// ─────────────────────────────────────────────────────────────────────
public async Task<Kitap?> GetirAsync(int id)
{
    return await _context.Kitaplar
        .AsNoTracking()
        .FirstOrDefaultAsync(k => k.Id == id);
    // her çağrıda: expression parse → SQL translation → cache lookup
    // saniyede 1.000 çağrı olursa bu overhead birikir
}

// ─────────────────────────────────────────────────────────────────────
// Compiled query — translation yalnızca bir kez, uygulama başlarken
// ─────────────────────────────────────────────────────────────────────
private static readonly Func<KitabeviDbContext, int, Task<Kitap?>>
    _idIleGetirQuery = EF.CompileAsyncQuery(   // derleme anında SQL üret, sakla
        (KitabeviDbContext ctx, int id) =>      // ctx: hangi DbContext, id: parametre
            ctx.Kitaplar
               .AsNoTracking()                  // Change Tracker yükü olmasın
               .FirstOrDefault(k => k.Id == id));
//              ↑ async compile'da FirstOrDefault kullanılır (FirstOrDefaultAsync değil)
//              bunu FirstOrDefaultAsync yazsaydık derleme hatası alırdık

// static readonly: uygulama boyunca tek instance
// bunu static yapmasaydık her servis instance'ı yeniden derleme yapardı → avantaj sıfırlanır

public async Task<Kitap?> IdIleGetirAsync(int id)
    => await _idIleGetirQuery(_context, id);
//   translation yok — doğrudan SQL parametresi bağlanır ve çalışır

// ─────────────────────────────────────────────────────────────────────
// Compiled query ile liste — IAsyncEnumerable döner
// ─────────────────────────────────────────────────────────────────────
private static readonly Func<KitabeviDbContext, string, IAsyncEnumerable<KitapListeViewModel>>
    _kategoriSorgusu = EF.CompileAsyncQuery(
        (KitabeviDbContext ctx, string kategori) =>
            ctx.Kitaplar
               .AsNoTracking()
               .Where(k => k.Kategori == kategori && k.StokAdedi > 0)  // sadece stoktaki
               .OrderBy(k => k.Baslik)                                  // alfabetik sırala
               .Select(k => new KitapListeViewModel(                    // projeksiyon: entity değil ViewModel
                   k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi)));
//  ToListAsync yok — compiled query IAsyncEnumerable döndürür, döngüyle tüketilir

public async Task<List<KitapListeViewModel>> KategoriGetirAsync(string kategori)
{
    var liste = new List<KitapListeViewModel>();
    await foreach (var item in _kategoriSorgusu(_context, kategori))  // stream olarak oku
        liste.Add(item);                                               // biriktir
    return liste;
    // bunu ToListAsync() ile yapmasaydık mümkün değildi çünkü
    // compiled query doğrudan List döndüremez, IAsyncEnumerable döndürür
}
```

### Ne Zaman Compiled Query Kullanılır?

```
EVET:
  ✓ Saniyede yüzlerce/binlerce kez çağrılan hot-path sorgular
  ✓ Parametreleri sabit olan (dinamik filtre yok) basit sorgular
  ✓ CPU profiler'ında EF Core translation süresinin belirgin olduğu durumlar

HAYIR:
  ✗ Dinamik filtre (koşullara göre değişen WHERE) olan sorgular
  ✗ Include() içeren karmaşık sorgular (derleme avantajı azalır)
  ✗ Nadiren çağrılan sorgular (erken optimizasyon)
```

---

## 8. Raw SQL — FromSqlRaw() ve ExecuteSqlRaw()

EF Core'un LINQ çeviremediği veya verimsiz çevirdiği durumlarda doğrudan SQL yazılır.

### FromSqlRaw() — Sorgu (SELECT)

```csharp
// ─────────────────────────────────────────────────────────────────────
// Temel kullanım
// ─────────────────────────────────────────────────────────────────────
var kitaplar = await _context.Kitaplar
    .FromSqlRaw(                             // ham SQL ile sorgu başlat
        "SELECT * FROM Kitaplar WHERE Fiyat > {0}",  // {0}: parametreli yer tutucu
        100)                                 // 100 → {0} pozisyonuna bağlanır
                                             // string interpolation kullansaydık SQL Injection açığı olurdu
    .AsNoTracking()                          // sadece okuma, izleme yok
    .ToListAsync();                          // SQL'i çalıştır

// ─────────────────────────────────────────────────────────────────────
// FromSqlRaw + LINQ zincirleme
// ─────────────────────────────────────────────────────────────────────
var sorgu = _context.Kitaplar
    .FromSqlRaw("SELECT * FROM Kitaplar")    // ham SQL başlangıç noktası olarak çalışır
    .Where(k => k.Kategori == "Roman")       // EF Core bunu SQL'e ekler: AND Kategori = N'Roman'
    .OrderBy(k => k.Baslik)                  // ORDER BY Baslik
    .Take(20);                               // TOP(20)
// bunu LINQ ile yazmak mümkündü ama stored proc veya view çağırınca bu yöntem şart

// ─────────────────────────────────────────────────────────────────────
// Stored Procedure çağırma
// ─────────────────────────────────────────────────────────────────────
var kitaplar = await _context.Kitaplar
    .FromSqlRaw(
        "EXEC sp_KitaplariGetir @KategoriId = {0}",  // stored proc parametresi
        kategoriId)                                   // değer buradan bağlanır
    .AsNoTracking()
    .ToListAsync();
// EF Core stored proc sonucunu Kitap entity'lerine map eder
// stored proc SELECT kolonları Kitap property'leriyle eşleşmeli, yoksa map başarısız olur
```

### SQL Injection Tuzağı

```csharp
// ─────────────────────────────────────────────────────────────────────
// YANLIŞ — string interpolation SQL'e doğrudan eklenir
// ─────────────────────────────────────────────────────────────────────
string kategori = Request.Query["kategori"];   // kullanıcıdan gelen değer, güvenilmez

var kitaplar = _context.Kitaplar
    .FromSqlRaw($"SELECT * FROM Kitaplar WHERE Kategori = '{kategori}'")
//              ↑ kategori = "'; DROP TABLE Kitaplar; --" yazılırsa
//              SQL: SELECT * FROM Kitaplar WHERE Kategori = ''; DROP TABLE Kitaplar; --'
//              → tablo silinir!
    .ToList();

// ─────────────────────────────────────────────────────────────────────
// DOĞRU Yöntem 1: FromSqlRaw + indeksli parametre
// ─────────────────────────────────────────────────────────────────────
var kitaplar = await _context.Kitaplar
    .FromSqlRaw(
        "SELECT * FROM Kitaplar WHERE Kategori = {0}",  // {0}: parametre yuvası
        kategori)                                        // EF Core bunu SqlParameter'a çevirir
    .ToListAsync();
// kategori ne olursa olsun SQL parametresi olarak gönderilir, komutu bozamaz

// ─────────────────────────────────────────────────────────────────────
// DOĞRU Yöntem 2: FromSqlInterpolated — daha okunabilir, aynı güvenlik
// ─────────────────────────────────────────────────────────────────────
var kitaplar = await _context.Kitaplar
    .FromSqlInterpolated($"SELECT * FROM Kitaplar WHERE Kategori = {kategori}")
//                        ↑ görünüşte normal string interpolation
//                          EF Core bu overload'ı özel olarak işler:
//                          {kategori} → SqlParameter'a çevirir, SQL'e gömmez
    .ToListAsync();
// bunu FromSqlRaw ile kullansaydık ({ }) parametre koruması çalışmazdı
```

### ExecuteSqlRaw() — Değiştirme (INSERT / UPDATE / DELETE)

```csharp
// ─────────────────────────────────────────────────────────────────────
// Toplu güncelleme — Change Tracker olmadan doğrudan SQL
// ─────────────────────────────────────────────────────────────────────
int etkilenenSatir = await _context.Database
    .ExecuteSqlRawAsync(                              // SELECT değil, komut çalıştır
        "UPDATE Kitaplar SET StokAdedi = 0 WHERE Kategori = {0}",
        "Arşiv");                                     // parametreli — güvenli
// EF Core üzerinden update yapmak zorunda olsaydık:
// 1) ToListAsync ile tüm "Arşiv" kitaplarını çek
// 2) foreach içinde StokAdedi = 0 yap
// 3) SaveChangesAsync çağır
// → N satır için N+1 sorgu + her satır Change Tracker'da izleniyor
// ExecuteSqlRaw: tek SQL, sıfır Change Tracker yükü

// ─────────────────────────────────────────────────────────────────────
// EF Core 7+ alternatifi: ExecuteUpdateAsync — LINQ tabanlı, daha güvenli
// ─────────────────────────────────────────────────────────────────────
int etkilenen = await _context.Kitaplar
    .Where(k => k.Kategori == "Arşiv")               // hangi satırlar güncellenmeli
    .ExecuteUpdateAsync(s =>                          // güncelleme işlemini tanımla
        s.SetProperty(k => k.StokAdedi, 0));          // StokAdedi = 0
// Üretilen SQL: UPDATE Kitaplar SET StokAdedi = 0 WHERE Kategori = N'Arşiv'
// ham SQL yazmadan aynı sonuç, üstelik refactor güvenli
// (kolon adı değişirse derleyici hata verir, raw SQL'de vermez)
```

### FromSqlRaw vs ExecuteSqlRaw Özeti

| | `FromSqlRaw` | `ExecuteSqlRaw` |
|---|---|---|
| Amaç | SELECT — veri okuma | INSERT/UPDATE/DELETE |
| Dönüş | `IQueryable<T>` | `int` (etkilenen satır) |
| LINQ zinciri | Evet (`Where`, `OrderBy` eklenebilir) | Hayır |
| Change Tracker | İsteğe bağlı (`AsNoTracking`) | Yok (doğrudan SQL) |

---

## 9. N+1'i Tespit Etmek

Kod bakarak N+1'i bulmak zordur. İki pratik yöntem:

### MiniProfiler ile SQL Sayısını İzle

```csharp
// dotnet add package MiniProfiler.AspNetCore.Mvc
// dotnet add package MiniProfiler.EntityFrameworkCore

// Program.cs
builder.Services.AddMiniProfiler(options =>
    options.RouteBasePath = "/profiler")  // /profiler/results-index adresi aktif olur
    .AddEntityFramework();                // her EF sorgusunu yakala ve say
// bunu eklemeseydin kaç sorgu çalıştığını görmek için log'a bakman gerekirdi
// geliştirme ortamında /profiler/results-index → sayfada kaç SQL çalıştığı görünür
```

### EF Core Logging ile Sorguları Gör

```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
                                                         // sadece SQL komutlarını logla
                                                         // "Warning" yazsaydık SQL'ler görünmezdi
                                                         // "Debug" yazsaydık çok fazla gürültü olurdu
    }
  }
}
```

```
// Konsol çıktısında her SQL görünür:
// info: Microsoft.EntityFrameworkCore.Database.Command[20101]
//       Executed DbCommand (2ms) [Parameters=[@__id_0='5']]
//       SELECT [y].[Id], [y].[Ad] FROM [Yazarlar] AS [y] WHERE [y].[Id] = @__id_0
//
// Aynı sorgunun 100 kez tekrarlandığını görürsen → N+1 var demektir
```

---

## 10. Kitabevi Uygulamasına Uygulama

```csharp
// EfKitapServisi.cs — N+1 korumalı metodlar

// ─────────────────────────────────────────────────────────────────────
// Compiled query — sık çağrılan ID'ye göre getir
// ─────────────────────────────────────────────────────────────────────
private static readonly Func<KitabeviDbContext, int, Task<Kitap?>>
//  ↑ static: sınıf yüklendiğinde bir kez derlenir, her instance tekrar derleme yapmaz
    _idIleGetirQuery = EF.CompileAsyncQuery(
        (KitabeviDbContext ctx, int id) =>   // ctx ve id dışarıdan verilecek
            ctx.Kitaplar
               .AsNoTracking()               // detay sayfası: değişiklik yapılmayacak
               .FirstOrDefault(k => k.Id == id));
//             ↑ FirstOrDefault (async versiyonu değil) — compile API'nin kuralı

public async Task<Kitap?> IdIleGetirAsync(int id)
    => await _idIleGetirQuery(_context, id);  // _context bu request'e özgü, id dışarıdan gelir

// ─────────────────────────────────────────────────────────────────────
// Eager loading — N+1 olmadan yazar bilgisi dahil
// ─────────────────────────────────────────────────────────────────────
public async Task<IReadOnlyList<KitapListeViewModel>> YazarlariyleHepsiniGetirAsync()
{
    return await _context.Kitaplar
        .AsNoTracking()                           // liste sayfası: sadece okuma
        .Include(k => k.YazarNavigation)          // yazarı JOIN ile getir
                                                  // bunu yazmasaydık aşağıdaki null-coalescing
                                                  // her zaman k.Yazar fallback'e düşerdi
        .Where(k => k.StokAdedi > 0)             // sadece stoktaki kitaplar
        .OrderBy(k => k.Baslik)                   // alfabetik sırala
        .Select(k => new KitapListeViewModel(
            k.Id,
            k.Baslik,
            k.YazarNavigation != null             // Yazarlar tablosundan JOIN'lenen veri var mı?
                ? k.YazarNavigation.Ad + " " + k.YazarNavigation.Soyad  // evet → tam ad
                : k.Yazar,                        // hayır → entity'deki eski string alanı (fallback)
            k.Fiyat,
            k.Kategori,
            k.StokAdedi))
        .ToListAsync();
    // tek SQL: SELECT + LEFT JOIN, N+1 yok
}

// ─────────────────────────────────────────────────────────────────────
// Raw SQL — EF LINQ ile ifade edilmesi güç olan serbest metin araması
// ─────────────────────────────────────────────────────────────────────
public async Task<IReadOnlyList<KitapListeViewModel>> FullTextAraAsync(string aramaMetni)
{
    // SQL Server Full-Text Search fonksiyonu CONTAINS()
    // EF Core bunu LINQ olarak desteklemez, FromSqlInterpolated şart
    return await _context.Kitaplar
        .FromSqlInterpolated(
            $"SELECT * FROM Kitaplar WHERE CONTAINS((Baslik, Yazar), {aramaMetni})")
        //                                                             ↑ parametreize edilir
        //  FromSqlRaw kullansaydık {aramaMetni} SQL'e gömülürdü → SQL Injection açığı
        .AsNoTracking()                           // arama sonucu: izlemeye gerek yok
        .Select(k => new KitapListeViewModel(
            k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
        .ToListAsync();
}
```

---

## 11. Özet

```
N+1 Problemi
  Her ana kayıt için ayrı SQL → 1 + N sorgu
  Çözüm: Include() ile eager loading veya projection

Loading Stratejileri
  Eager (Include)    → her zaman gerekli → varsayılan tercih
  Lazy               → navigation'a erişince otomatik SQL → üretimde kapalı tut
  Explicit (LoadAsync) → koşullu yükleme, kontrollü roundtrip

AsSplitQuery()
  Kullan  : birden fazla collection navigation include ediliyorsa
  Kullanma: reference navigation, sayfalama ile birlikte dikkatli

EF.CompileQuery()
  Hot-path sorgularda translation overhead'ini sıfırlar
  Dinamik filtrelere uygun değil; static readonly field olarak tanımla

Raw SQL
  FromSqlRaw / FromSqlInterpolated → SELECT, LINQ zinciri eklenebilir
  ExecuteSqlRaw / ExecuteSqlInterpolated → INSERT/UPDATE/DELETE
  DAIMA parametreli kullan → SQL Injection önlemi
  EF Core 7+: ExecuteUpdateAsync / ExecuteDeleteAsync → daha güvenli alternatif
```

---

## Sonraki Gün

Gün 32'de Migration Stratejileri: code-first migration sistemi, production'da güvenli migration, migration bundle ve seed data yönetimi ele alınacak.
