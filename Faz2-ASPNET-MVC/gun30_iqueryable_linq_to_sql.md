# Gün 30 — IQueryable ve LINQ-to-SQL

---

## 1. IQueryable\<T\> Nedir?

`IQueryable<T>`, bir sorguyu **temsil eden** arayüzdür — sorguyu hemen çalıştırmaz. Arkasında bir **expression tree** (ifade ağacı) tutar; EF Core bu ağacı okuyarak SQL üretir.

```
LINQ ifadesi yazılır
     ↓
Expression tree oluşturulur (sorgu henüz çalışmadı)
     ↓
ToListAsync() / FirstOrDefaultAsync() gibi "terminal" metod çağrılır
     ↓
EF Core expression tree'yi SQL'e çevirir
     ↓
SQL veritabanına gönderilir
     ↓
Sonuç C# nesnelerine dönüştürülür
```

**Temel kural:** `IQueryable` üzerindeki her `Where`, `OrderBy`, `Select` — bunlar SQL'e eklenecek **tarif**tir. Veritabanına henüz gidilmedi.

---

## 2. IEnumerable\<T\> vs IQueryable\<T\>

Bu iki arayüz arasındaki fark, .NET'te en sık karşılaşılan performans tuzağıdır.

| | `IQueryable<T>` | `IEnumerable<T>` |
|---|---|---|
| Çalıştığı yer | Veritabanı (SQL) | Uygulama belleği (C#) |
| Filtre nerede? | `WHERE` SQL'de | `ToList()` sonrası C#'ta |
| Veri miktarı | Sadece gerekli satırlar | Tüm tablo çekilir |
| Kullanım | EF Core sorguları | Bellekteki koleksiyonlar |

### Client-Side Evaluation Tuzağı

```csharp
// ──────────────────────────────────────────────
// YANLIŞ: IEnumerable'a dönüşüm erken yapıldı
// ──────────────────────────────────────────────

// AsEnumerable() → veritabanından TÜM kitaplar çekilir (ör. 100.000 satır)
// Where() → 100.000 satırın hepsi belleğe alınıp C#'ta filtrelendi
// Üretilen SQL: SELECT * FROM Kitaplar  ← WHERE yok!
var kitaplar = _context.Kitaplar
    .AsEnumerable()                       // ← tehlike: client-side'a geçiş
    .Where(k => k.Fiyat > 100)
    .ToList();

// ──────────────────────────────────────────────
// DOĞRU: IQueryable üzerinde filtrele
// ──────────────────────────────────────────────

// Where() → expression tree'ye eklendi (SQL henüz yok)
// ToListAsync() → SQL üretildi ve çalıştırıldı
// Üretilen SQL: SELECT * FROM Kitaplar WHERE Fiyat > 100
var kitaplar = await _context.Kitaplar
    .Where(k => k.Fiyat > 100)           // server-side
    .ToListAsync();
```

### IQueryable Nasıl Bozulur?

```csharp
// Aşağıdaki metodlar IQueryable → IEnumerable'a dönüştürür:
// AsEnumerable(), ToList(), ToArray(), foreach, First() (terminal)

// Bu dönüşümden SONRA yapılan işlemler bellekte çalışır:
var sorgu = _context.Kitaplar.AsEnumerable();  // DB'den tüm veri çekildi
var sonuc = sorgu.Where(k => k.Fiyat > 100);   // bellek filtrelemesi
```

---

## 3. Deferred Execution (Ertelenmiş Yürütme)

`IQueryable` ertelenmiş yürütme kullanır: sorgu tanımlanırken değil, sonuç **talep edildiğinde** çalışır.

```csharp
// 1. Sorgu tanımlandı — SQL henüz yok
var sorgu = _context.Kitaplar.Where(k => k.Fiyat > 100);

// 2. Sorguya ek koşul eklendi — hâlâ SQL yok
sorgu = sorgu.OrderBy(k => k.Baslik);

// 3. Terminal metod → SQL üretildi ve DB'ye gönderildi
var liste = await sorgu.ToListAsync();
// Üretilen SQL:
// SELECT * FROM Kitaplar WHERE Fiyat > 100 ORDER BY Baslik
```

**Terminal metodlar** (sorguyu çalıştıranlar): `ToListAsync`, `FirstOrDefaultAsync`, `SingleAsync`, `CountAsync`, `AnyAsync`, `SumAsync`, `foreach`.

Bu sayede koşulları birleştirebilirsin:

```csharp
// Dinamik filtre — tüm kombinasyonlar tek SQL sorgusu üretir
var sorgu = _context.Kitaplar.AsQueryable();

if (!string.IsNullOrEmpty(kategori))
    sorgu = sorgu.Where(k => k.Kategori == kategori);

if (minFiyat.HasValue)
    sorgu = sorgu.Where(k => k.Fiyat >= minFiyat.Value);

if (maxFiyat.HasValue)
    sorgu = sorgu.Where(k => k.Fiyat <= maxFiyat.Value);

var sonuc = await sorgu.OrderBy(k => k.Baslik).ToListAsync();
// Tek SQL, sadece seçilen filtreler WHERE'de
```

---

## 4. Projection — Select ile SQL'i Küçült

Projection, veritabanından sadece ihtiyaç duyulan kolonları çekmektir. `Select()` EF Core tarafından `SELECT col1, col2` SQL'ine dönüştürülür.

```csharp
// ──────────────────────────────────────────────
// YANLIŞ: Tüm entity çekiliyor
// ──────────────────────────────────────────────
// SELECT Id, Baslik, Yazar, Fiyat, Kategori, StokAdedi, EklemeTarihi, YazarId FROM Kitaplar
var kitaplar = await _context.Kitaplar
    .AsNoTracking()
    .ToListAsync();

var liste = kitaplar.Select(k => new KitapListeViewModel(
    k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi));

// ──────────────────────────────────────────────
// DOĞRU: Sadece gerekli kolonlar
// ──────────────────────────────────────────────
// SELECT Id, Baslik, Yazar, Fiyat, Kategori, StokAdedi FROM Kitaplar
var liste = await _context.Kitaplar
    .AsNoTracking()
    .Select(k => new KitapListeViewModel(
        k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
    .ToListAsync();
```

**Projection'ın faydaları:**
- Gereksiz kolonlar ağdan taşınmaz
- Büyük `nvarchar(max)` veya binary alanlar istemeden çekilmez
- `AsNoTracking()` ile birleşince Change Tracker overhead'i de kalkar
- DTO/ViewModel doğrudan veritabanından gelir, mapping adımı ortadan kalkar

---

## 5. Include() — Eager Loading

İlişkili entity'leri ana entity ile birlikte **tek sorguda** yüklemek için kullanılır.

```csharp
// Kitap + Yazar birlikte — tek SQL (JOIN)
var kitaplar = await _context.Kitaplar
    .AsNoTracking()
    .Include(k => k.YazarNavigation)   // Yazar tablosunu JOIN et
    .Where(k => k.Fiyat > 50)
    .ToListAsync();

// Üretilen SQL:
// SELECT k.*, y.*
// FROM Kitaplar k
// LEFT JOIN Yazarlar y ON k.YazarId = y.Id
// WHERE k.Fiyat > 50
```

**Include olmadan ne olur?**

```csharp
var kitap = await _context.Kitaplar.FindAsync(1);
var yazarAdi = kitap.YazarNavigation?.Ad; // null! — yüklenmedi
```

Lazy loading kapalıysa (varsayılan) `Include` olmadan navigation property `null` gelir.

---

## 6. ThenInclude() — Nested Navigation

İlişkinin ilişkisini yüklemek için kullanılır.

```csharp
// Senaryo: Sipariş → SiparisKalemleri → Kitap → Yazar
// (Bu projede tam model yok; kavramı göstermek için)

var siparisler = await _context.Siparisler
    .AsNoTracking()
    .Include(s => s.Kalemler)                    // 1. seviye
        .ThenInclude(k => k.Kitap)               // 2. seviye
            .ThenInclude(kitap => kitap.YazarNavigation) // 3. seviye
    .Where(s => s.Tarih >= DateTime.UtcNow.AddDays(-30))
    .ToListAsync();
```

**Dikkat:** Her `Include` ve `ThenInclude` SQL'e JOIN ekler. Derinleştikçe sorgu karmaşıklaşır → `AsSplitQuery()` gerekebilir.

---

## 7. AsSplitQuery() — Karmaşık JOIN'leri Böl

Birden fazla `Include` olduğunda EF Core tek büyük JOIN üretir. Bu, **Cartesian Explosion** sorununa yol açabilir.

### Cartesian Explosion Nedir?

```
Kitap tablosu:      3 satır
Yorumlar tablosu:   100 satır (kitap başına ~33)
Etiketler tablosu:  50 satır  (kitap başına ~17)

Tek JOIN SQL:  3 × 100 × 50 = 15.000 satır ağdan geçer!
Split Query:   3 + 100 + 50 = 153 satır (3 ayrı sorgu)
```

```csharp
// Tek JOIN → potansiyel Cartesian Explosion
var kitaplar = await _context.Kitaplar
    .Include(k => k.Yorumlar)
    .Include(k => k.Etiketler)
    .ToListAsync();

// Split Query → 3 ayrı SQL, sonuçlar bellekte birleştirilir
var kitaplar = await _context.Kitaplar
    .Include(k => k.Yorumlar)
    .Include(k => k.Etiketler)
    .AsSplitQuery()   // ← EF Core 5+
    .ToListAsync();

// Üretilen SQL'ler:
// 1) SELECT * FROM Kitaplar
// 2) SELECT * FROM Yorumlar WHERE KitapId IN (1, 2, 3)
// 3) SELECT * FROM Etiketler WHERE KitapId IN (1, 2, 3)
```

**Ne zaman `AsSplitQuery()`?**
- Birden fazla collection navigation (`ICollection<T>`) include ediyorsan
- Tek JOIN sonucu çok büyük veri dönüyorsa
- Profiler'da beklenmedik büyük resultset görüyorsan

**Ne zaman kullanMA?**
- Tek include varsa — split query 3 DB roundtrip = daha yavaş olabilir
- Transaction içindeysen — split sorgular ayrı snapshot okuyabilir

---

## 8. Kitabevi Uygulamasına Uygulama

Gün 29'da yazdığımız `EfKitapServisi`'ni projection ve Include ile güncelleyelim:

```csharp
// EfKitapServisi.cs — Gün 30 iyileştirmeleri

// ─────────────────────────────────────────────────────────────────────
// HepsiniGetirAsync — Projection + AsNoTracking
// ─────────────────────────────────────────────────────────────────────
public async Task<IReadOnlyList<KitapListeViewModel>> HepsiniGetirAsync()
{
    // Select() → SQL'de sadece 6 kolon seçilir (entity'nin tüm alanları değil)
    // AsNoTracking() → Change Tracker'a eklenmez
    // Birlikte: verimli okuma sorgusu
    return await _context.Kitaplar
        .AsNoTracking()
        .Where(k => k.StokAdedi > 0)
        .OrderBy(k => k.Baslik)
        .Select(k => new KitapListeViewModel(
            k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
        .ToListAsync();
    // SQL: SELECT Id, Baslik, Yazar, Fiyat, Kategori, StokAdedi
    //      FROM Kitaplar WHERE StokAdedi > 0 ORDER BY Baslik
}

// ─────────────────────────────────────────────────────────────────────
// YazarlaGetirAsync — Include ile Yazar navigation yükleniyor
// ─────────────────────────────────────────────────────────────────────
public async Task<KitapDetayViewModel?> DetayYazarlaGetirAsync(int id)
{
    // Include → Yazar tablosu JOIN'leniyor
    // Projection → sadece gerekli alanlar seçiliyor
    return await _context.Kitaplar
        .AsNoTracking()
        .Include(k => k.YazarNavigation)
        .Where(k => k.Id == id)
        .Select(k => new KitapDetayViewModel
        {
            Id        = k.Id,
            Baslik    = k.Baslik,
            Fiyat     = k.Fiyat,
            Kategori  = k.Kategori,
            StokAdedi = k.StokAdedi,
            // YazarNavigation null olabilir (YazarId nullable)
            YazarAdi  = k.YazarNavigation != null
                            ? k.YazarNavigation.Ad + " " + k.YazarNavigation.Soyad
                            : k.Yazar  // fallback: eski string alanı
        })
        .FirstOrDefaultAsync();
    // SQL:
    // SELECT k.Id, k.Baslik, k.Fiyat, k.Kategori, k.StokAdedi,
    //        k.Yazar, y.Ad, y.Soyad
    // FROM Kitaplar k
    // LEFT JOIN Yazarlar y ON k.YazarId = y.Id
    // WHERE k.Id = @id
}

// ─────────────────────────────────────────────────────────────────────
// AyniKategoridekilerAsync — IQueryable dinamik filtre
// ─────────────────────────────────────────────────────────────────────
public async Task<IReadOnlyList<KitapListeViewModel>> AyniKategoridekilerAsync(
    string kategori, int haricId)
{
    // IQueryable zinciri: her adım SQL'e ekleniyor
    var sorgu = _context.Kitaplar
        .AsNoTracking()
        .Where(k => k.Kategori == kategori && k.Id != haricId);

    // Dinamik sıralama: önce stok durumu (stokta olanlar önce), sonra fiyat
    sorgu = sorgu
        .OrderByDescending(k => k.StokAdedi > 0) // boolean: true > false
        .ThenBy(k => k.Fiyat);

    return await sorgu
        .Select(k => new KitapListeViewModel(
            k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
        .Take(10) // en fazla 10 öneri
        .ToListAsync();
}
```

---

## 9. Sık Yapılan Hatalar

### 1. ToList() Çok Erken Çağrıldı

```csharp
// YANLIŞ — tüm tablo belleğe alındı, filtre C#'ta
var tumKitaplar = await _context.Kitaplar.ToListAsync(); // ← burada SQL bitti
var roman = tumKitaplar.Where(k => k.Kategori == "Roman"); // bellekte

// DOĞRU
var roman = await _context.Kitaplar
    .Where(k => k.Kategori == "Roman")
    .ToListAsync();
```

### 2. Select Sonrası Tekrar DB Sorgusu

```csharp
// YANLIŞ — Select sonrası Include çalışmaz (projection entity değil)
var kitaplar = _context.Kitaplar
    .Select(k => new { k.Id, k.Baslik })  // projection yapıldı
    .Include(k => k.YazarNavigation)       // hata veya görmezden gelinir
    .ToList();

// DOĞRU — Include önce, Select sonra
var kitaplar = await _context.Kitaplar
    .Include(k => k.YazarNavigation)
    .Select(k => new { k.Id, k.Baslik, YazarAdi = k.YazarNavigation!.Ad })
    .ToListAsync();
```

### 3. IQueryable'ı Metod Sınırlarından Geçirmek

```csharp
// YANLIŞ — DbContext scope'u dışında sorgu çalıştırılıyor
public IQueryable<Kitap> GetirSorgu() =>
    _context.Kitaplar.Where(k => k.Aktif); // context dispose olabilir

// Controller'da:
var sorgu = _servis.GetirSorgu();
// ... context Scoped → request bitti, context dispose edildi
var liste = sorgu.ToList(); // ObjectDisposedException!

// DOĞRU — somut tipi servis içinde resolve et
public async Task<List<KitapDto>> GetirAsync() =>
    await _context.Kitaplar
        .Where(k => k.Aktif)
        .Select(k => new KitapDto { ... })
        .ToListAsync();
```

---

## 10. Özet

```
IQueryable<T>     → expression tree, SQL server-side üretilir
IEnumerable<T>    → in-memory, client-side (dikkatli kullan)

.Where()          → SQL'de WHERE
.Select()         → SQL'de SELECT (projection — sadece gerekli kolonlar)
.OrderBy()        → SQL'de ORDER BY
.Take() / .Skip() → SQL'de TOP / OFFSET-FETCH

Include()         → JOIN ile navigation property yükle
ThenInclude()     → nested JOIN (ilişkinin ilişkisi)
AsSplitQuery()    → multiple collection include → Cartesian Explosion önle

ToListAsync()     → sorguyu çalıştır (terminal)
FirstOrDefault()  → ilk veya null (terminal)
AnyAsync()        → EXISTS kullanır, çok verimli (terminal)
CountAsync()      → COUNT(*) (terminal)

Altın kural: ToList()'i mümkün olduğu kadar geç çağır — filtreler SQL'de kalsın.
```

---

## Sonraki Gün

Gün 31'de N+1 query problemi derinlemesine incelenecek: eager vs lazy vs explicit loading farkları, `AsSplitQuery()` sınırları ve `EF.CompileQuery()` ile tekrar kullanılan sorgu optimizasyonu.
