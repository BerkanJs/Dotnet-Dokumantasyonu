# Gün 91 — SQL ve Index Stratejisi: .NET Geliştiricisi Perspektifinden

---

## Neden Bu Ders Var?

EF Core sorguyu senin yerine yazıyor — `.Where(k => k.YazarId == 5)` diyorsun, o `SELECT ... WHERE YazarId = 5` üretiyor. Sorun şu: bu SQL veritabanında **nasıl çalışır?** Tablo 1 milyon satırsa, YazarId kolonunda index yoksa — veritabanı **her satırı tek tek okuyup** kontrol eder. Bu "full table scan" denen şey.

1000 sayfalık kitapta bir kelimeyi aramak gibi düşün. İki yol var:
- **Full scan:** Her sayfayı aç, oku, kelime var mı bak → 1000 sayfa okumalısın
- **Index (dizin):** Arkadaki kavram dizinine bak → "sayfa 342" yazar, direkt git

SQL index'i tam olarak budur — veritabanının "kavram dizini."

---

## 1. Clustered Index — Tablonun Fiziksel Düzeni

### Ne demek bu?

Bir tablodaki veriler disk'te bir sıraya göre fiziksel olarak dizilir. Bu sırayı belirleyen şey **clustered index**'tir. Bir tabloda **yalnızca 1 tane** olabilir çünkü veri fiziksel olarak yalnızca bir şekilde sıralanabilir.

**Analoji:** Bir sözlük düşün. Kelimeler A'dan Z'ye sıralı dizilmiş. "Performans" kelimesini aramak istiyorsan P harfine gidersin — çünkü fiziksel sıra alfabetik. İşte sözlüğün kendisi = clustered index. Veriyi başka bir sıraya göre dizmeye kalkarsan tüm kitabı yeniden basman lazım.

SQL Server'da primary key otomatik olarak clustered index olur:
```sql
CREATE TABLE Kitaplar (
    Id INT PRIMARY KEY,       -- bu otomatik clustered index
    Ad NVARCHAR(200),
    YazarId INT,
    Fiyat DECIMAL(10,2)
);
-- Disk'teki satır sırası: Id=1, Id=2, Id=3... şeklinde fiziksel dizilir
-- WHERE Id = 42 dediğinde → direkt o noktaya gider (çok hızlı)
```

### Peki neden bilmem gerekiyor?

`WHERE Id = 42` çok hızlı çünkü veri zaten Id sıralı. Ama `WHERE YazarId = 5` dersen — YazarId'ye göre bir sıralama yok, tüm tabloyu taramak zorunda. İşte burada non-clustered index devreye girer.

---

## 2. Non-Clustered Index — Ayrı Bir Arama Tablosu

### Ne demek bu?

Clustered index tablonun **kendisi** iken, non-clustered index tablonun **yanına eklenen ayrı bir liste.** Bu listede sadece aradığın kolon + "asıl satır nerede" bilgisi var.

**Analoji:** Kitabın arkasındaki "kavram dizini." Kitabın kendisi konu sırasıyla yazılmış (clustered), ama arkadaki dizinde "cache → sayfa 88, 142, 201" yazıyor. Kelimeyi dizinden bulursun, sayfa numarasıyla asıl sayfaya gidersin.

```sql
-- YazarId'ye göre sık sorgulama yapıyoruz (kitapları yazara göre listeliyoruz)
CREATE NONCLUSTERED INDEX IX_Kitaplar_YazarId ON Kitaplar(YazarId);
```

**Bu index olmadan ne olur?**
```
SELECT * FROM Kitaplar WHERE YazarId = 5;

Index YOKKEN (full table scan):
  → 1.000.000 satırın HEPSİNİ oku
  → her birinde YazarId=5 mi diye kontrol et
  → 200ms+ sürer

Index VARKEN (index seek):
  → Index'te YazarId=5 olan satırları bul (sıralı olduğu için çok hızlı)
  → sadece o satırların adresine git
  → 2ms sürer
```

### Ne zaman index eklemeliyim?

**Ekle:**
- WHERE'de sık kullandığın kolonlar (`WHERE KategoriId = 3`)
- JOIN kolonları (`JOIN Yazarlar ON k.YazarId = y.Id`)
- ORDER BY kolonları (`ORDER BY YayinTarihi DESC`)
- Çok farklı değer alan kolonlar (Email — her satır farklı = iyi aday)

**Ekleme:**
- Çok küçük tablolar (100 satır) — full scan zaten 0.1ms, index gereksiz
- Çok sık yazılan tablolar — her INSERT/UPDATE index'i de günceller (yazma yavaşlar)
- Az farklı değer alan kolonlar (`IsActive` — sadece true/false, index işe yaramaz)

---

## 3. Covering Index — Tabloya Hiç Gitmeden Cevap Ver

### Sorun ne?

Non-clustered index'te sadece key kolon var. Sorgun başka kolonlar da istiyorsa → index'ten satırı bul, sonra **tabloya geri dön** o kolonları oku. Bu geri dönüşe "Key Lookup" denir ve yavaştır.

**Analoji:** Kitabın dizininde "cache → sayfa 88" yazıyor. Sayfaya gittin. Ama aynı zamanda yazarın kim olduğunu da öğrenmek istiyorsun — dizinde yazar yazmıyor, sayfaya gitmek zorundasın. **Eğer dizinde "cache → sayfa 88, yazar: Fowler" yazsaydı** — sayfaya gitmene gerek kalmazdı.

```sql
-- Sık çalışan sorgu:
SELECT Ad, Fiyat FROM Kitaplar WHERE YazarId = 5;

-- Sadece YazarId index'i varsa ne olur:
-- 1. Index'te YazarId=5 olan 50 satırı bul ✓
-- 2. Her satır için tabloya git, Ad ve Fiyat'ı oku ✗ (50 Key Lookup = yavaş)

-- Covering index ile:
CREATE NONCLUSTERED INDEX IX_Kitaplar_YazarId_Cover
ON Kitaplar(YazarId)
INCLUDE (Ad, Fiyat);
-- INCLUDE ne yapar → Ad ve Fiyat değerlerini index yapısının içine kopyalar
-- artık tabloya GİTMEDEN cevap verebilir (index-only scan)
-- Key Lookup: 0 → çok daha hızlı
```

**Ne zaman kullanırsın?** Execution plan'da "Key Lookup" görüyorsun ve o sorgu çok sık çalışıyor. INCLUDE ile ihtiyaç duyulan kolonları index'e eklersin.

**Trade-off:** Index boyutu büyür (daha fazla disk). Ama okuma hızı artar. Sık okunan, nadir güncellenen tablolar için ideal.

---

## 4. Filtered Index — Tablonun Sadece Bir Kısmı İçin Index

### Ne demek?

1 milyon kitap var ama 990.000'i soft-delete edilmiş. Sadece aktif 10.000 kitabı sorguluyorsun. Neden 1M satırlık dev bir index tutasın? Sadece aktif olanlar için küçük bir index yeterli.

```sql
CREATE NONCLUSTERED INDEX IX_Kitaplar_Aktif
ON Kitaplar(Ad, YazarId)
WHERE IsDeleted = 0;
-- ne yapar → sadece IsDeleted=0 olan 10K satır index'te
-- 1M satırlık index yerine 10K satırlık index — 100x küçük, 100x hızlı
```

**Ne zaman kullanırsın?**
- Soft-delete var ve hep `WHERE IsDeleted = 0` ekliyorsun
- Status kolonu var ve hep `WHERE Status = 'Active'` sorguluyorsun
- Tablonun %90'ını hiç sorgulamıyorsun

```csharp
// EF Core'da:
modelBuilder.Entity<Kitap>()
    .HasIndex(k => new { k.Ad, k.YazarId })
    .HasFilter("[IsDeleted] = 0");
// migration çalışınca bu filtered index'i oluşturur
```

---

## 5. Composite Index — Kolon Sırası Neden ÇOK Önemli?

### Ne demek?

Birden fazla kolonu tek index'te birleştiriyorsun. Ama sıra çok önemli — index **soldan sağa** kullanılır.

**Analoji:** Telefon rehberi "Soyadı, Adı" sırasıyla dizilmiş. "Özçelik" soyadını kolayca bulursun. Ama sadece "Berkan" adını ararsan — rehber ada göre sıralı değil, hepsine bakman lazım.

```sql
CREATE INDEX IX_Kitaplar_Yazar_Fiyat ON Kitaplar(YazarId, Fiyat);
-- Sıra: önce YazarId, sonra Fiyat
```

```sql
-- ✓ Bu sorgular index'i KULLANIR:
WHERE YazarId = 5                     -- sol kolon var → OK
WHERE YazarId = 5 AND Fiyat > 50     -- her iki kolon soldan sa��a → OK
WHERE YazarId = 5 ORDER BY Fiyat     -- filtre sol kolon, sıralama sağ → OK

-- ✗ Bu sorgular index'i KULLANAMAZ:
WHERE Fiyat > 50                     -- sol kolon (YazarId) atlanmış!
ORDER BY Fiyat                       -- yine YazarId yok → full scan
```

**Neden?** Rehber analojisi: rehber "Soyadı → Adı" sıralı. "Adı = Berkan" dersen rehber sana yardımcı olamaz çünkü Berkan'lar her soyadının altında dağınık.

**Sırayı nasıl belirlerim?** En çok filtrelediğin kolon sola, sıraladığın sağa:
- `WHERE KategoriId = X ORDER BY YayinTarihi DESC` → Index(KategoriId, YayinTarihi DESC)

---

## 6. SARGability — Index'i Kör Eden Hatalar

SARG = Search ARGumentable. Sorgu koşulunda kolona fonksiyon/hesaplama uygularsan, veritabanı index'i **kullanamaz** çünkü her satırda hesaplama yapması gerekir.

```sql
-- ✗ NON-SARGABLE — index kör:
WHERE YEAR(YayinTarihi) = 2024
-- neden → veritabanı her satırda YEAR() hesaplar, index kullanamaz

-- ✓ SARGABLE — index çalışır:
WHERE YayinTarihi >= '2024-01-01' AND YayinTarihi < '2025-01-01'
-- neden → kolon saf halde karşılaştırılıyor, index seek yapabilir
```

```sql
-- ✗ Hesaplama kolonda:
WHERE Fiyat * 1.18 > 100

-- ✓ Hesaplama sabit tarafta:
WHERE Fiyat > 100 / 1.18
-- aynı mantık ama kolon saf kaldı → index kullanılır
```

```sql
-- ✗ String birleştirme:
WHERE Ad + ' ' + Soyad = 'Orhan Pamuk'

-- ✓ Ayrı karşılaştır:
WHERE Ad = 'Orhan' AND Soyad = 'Pamuk'
```

**Kural:** Kolona dokunma. Fonksiyon, hesaplama, birleştirme — hepsi index'i kör eder. Hesaplamayı hep "diğer tarafa" taşı.

---

## 7. Implicit Conversion — Gizli Tip Dönüşümü Tuzağı

Kolon tipi `VARCHAR`, sen `NVARCHAR` parametre gönderdin. SQL Server otomatik dönüştürür ama bunu **her satırda** yapar → index kullanılamaz.

```sql
-- Kolon: ISBN VARCHAR(13)
-- C# string → SQL'de NVARCHAR olarak gider

WHERE ISBN = N'978-123'   -- N prefix = NVARCHAR
-- SQL Server: "VARCHAR kolonunu NVARCHAR'a çevirmem lazım... her satırda"
-- → full table scan, index kör
```

**Bu EF Core'da nasıl olur?**
```csharp
// C#'ta string = NVARCHAR olarak gönderilir
.Where(k => k.ISBN == myVariable)
// Eğer DB'de ISBN kolonu VARCHAR ise → implicit conversion → index kör!
```

**Çözüm:** Kolon tipini C# tipiyle eşle:
```csharp
modelBuilder.Entity<Kitap>()
    .Property(k => k.ISBN)
    .HasColumnType("nvarchar(13)");   // DB'de de nvarchar yap → uyumsuzluk kalmaz
// veya tersi: column type varchar ise, parametre de varchar gitmeli
```

---

## 8. Query Plan Okuma — Sorgunun Röntgeni

Sorgunun yavaş olduğunu biliyorsun ama **neden** yavaş? Execution plan sana adım adım gösterir.

### SQL Server'da

SSMS'de sorguyu seç → Ctrl+L (tahmini plan) veya Ctrl+M (gerçek plan).

### PostgreSQL'de

```sql
EXPLAIN ANALYZE SELECT * FROM kitaplar WHERE yazar_id = 5;
```

### Plan'da ne arıyorsun?

| Gördüğün şey | Ne demek | Ne yapmalısın |
|--------------|----------|---------------|
| **Table Scan** / **Seq Scan** | Tüm tablo baştan sona okunuyor | O kolona index ekle |
| **Key Lookup** | Index buldu ama tabloya geri dönüyor | INCLUDE ile covering index yap |
| **Sort** (yüksek cost) | Bellekte sıralama yapıyor | ORDER BY kolonunu index'e ekle |
| **Estimated: 1, Actual: 50.000** | İstatistikler yanlış | `UPDATE STATISTICS` çalıştır |

**Pratik tavsiye:** Yavaş sorguyu buldun → plan'a bak → Table Scan varsa index ekle → Key Lookup varsa INCLUDE ekle → tekrar plan'a bak. Bu döngüyü performans kabul edilene kadar tekrarla.

---

## 9. Pagination — Sayfalama Stratejisi

### OFFSET/FETCH (Skip/Take) — Basit Ama Ölçeklenmez

```csharp
// EF Core'da:
var sayfa50 = await _context.Kitaplar
    .OrderBy(k => k.Id)
    .Skip(490)      // 490 satırı oku ve ÇÖP'e at
    .Take(10)       // sadece bu 10'u döndür
    .ToListAsync();
```

**Sorun ne?** Sayfa 500'desin — veritabanı 4990 satırı okuyor, atıyor, sadece 10'unu veriyor. Sayfa derinleştikçe:
- Sayfa 1 → 0 satır okunup atılır
- Sayfa 100 → 990 satır okunup atılır
- Sayfa 5000 → 49.990 satır okunup atılır ← **çok yavaş**

### Keyset (Cursor-Based) — Her Sayfada Aynı Hız

```csharp
// "En son gördüğüm Id'den sonrakileri getir"
var sonrakiSayfa = await _context.Kitaplar
    .Where(k => k.Id > sonGorulenId)   // direkt o noktadan başla
    .OrderBy(k => k.Id)
    .Take(10)
    .ToListAsync();
// Index seek: Id > 490 olan ilk 10 → her zaman aynı hız
// sayfa 5000 olsa bile 2ms
```

**Analoji:** 
- OFFSET = kitapta "sayfa 500'ü bul" demek — 499 sayfa çevirmen lazım
- Keyset = kitaba ayraç koymuşsun — direkt ayracın olduğu yere açarsın

**Ne zaman hangisi?**
- Küçük tablo, admin panel, "sayfa 3'e atla" lazım → Skip/Take yeterli
- Büyük tablo, sonsuz scroll, mobil API → Keyset kullan

---

## 10. EF.CompileQuery — Sıcak Sorgular İçin

EF Core her `.Where(...).ToListAsync()` çağrısında: C# expression tree → SQL string çevirisi yapar. Bu çevirim ~0.1-0.5ms sürer. Saniyede 5 kez çalışan sorguda fark etmezsin. Saniyede 5000 kez çalı��an sorguda toplanır.

```csharp
// Compiled query — çevirim bir kez yapılır, cache'lenir:
private static readonly Func<AppDbContext, int, Task<Kitap?>> _getById =
    EF.CompileAsyncQuery((AppDbContext ctx, int id) =>
        ctx.Kitaplar.FirstOrDefault(k => k.Id == id));

// Kullanım:
var kitap = await _getById(_context, 42);
// ne yapar → SQL çevirisi ilk çağrıda yapılıp saklanır, sonraki çağrılarda 0 overhead
// ne zaman kullan → "hot path" sorgular (saniyede yüzlerce/binlerce kez çalışan)
// ne zaman gereksiz → nadir çalışan sorgular, karmaşık dynamic sorgular
```

---

## 11. Connection Pooling

### Ne demek?

Her veritabanı bağlantısı açmak pahalı: TCP bağlantısı + kimlik doğrulama + TLS → 20-50ms. Connection pool, kullanılan bağlantıları kapatmak yerine **havuzda tutar** ve sonraki istek gelince yeniden kullanır.

**Analoji:** Restoran mutfağı. Her sipariş için yeni şef almıyorsun — 5 şef hazır bekliyor. Sipariş gelince müsait şef alıyor. Tüm şefler doluysa sipariş kuyrukta bekliyor.

```csharp
// Connection string'de:
"Server=localhost;Database=KitapDb;Min Pool Size=5;Max Pool Size=100;"
// Min Pool Size=5 → uygulama başladığında 5 bağlantı hazır (cold start yok)
// Max Pool Size=100 → en fazla 100 eşzamanlı bağlantı
// 101. istek gelirse → kuyrukta bekler → timeout olursa hata fırlatır
```

**Pool tükenmesi (exhaustion) nasıl olur?**
```csharp
// ✗ Bağlantıyı kapatmayı unuttun:
var conn = new SqlConnection(connStr);
conn.Open();
// ... uzun işlem ...
// conn.Close() yok! → bu bağlantı havuza dönmez → havuz tükenir

// ✓ using ile otomatik dönüş:
await using var conn = new SqlConnection(connStr);
await conn.OpenAsync();
// blok bitince ba��lantı otomatik havuza döner
```

---

## 12. Batch Insert/Update — Toplu İşlemler

### Sorun: Tek tek güncelleme = N adet SQL

```csharp
// ✗ 1000 kitaba %10 zam — 1000 ayrı UPDATE:
var kitaplar = await _context.Kitaplar.Where(k => k.YazarId == 5).ToListAsync();
foreach (var k in kitaplar)
    k.Fiyat *= 1.10m;
await _context.SaveChangesAsync();
// ne olur → EF 1000 satırı belleğe çeker + 1000 UPDATE SQL gönderir
// 1000 round-trip = yavaş
```

### Çözüm: EF Core 7+ ExecuteUpdate (tek SQL)

```csharp
await _context.Kitaplar
    .Where(k => k.YazarId == 5)
    .ExecuteUpdateAsync(s => s.SetProperty(k => k.Fiyat, k => k.Fiyat * 1.10m));
// ne yapar → UPDATE Kitaplar SET Fiyat = Fiyat * 1.10 WHERE YazarId = 5
// tek SQL, tek round-trip, bellekte entity yok
// 1000 satır g��ncelleme: SaveChanges ~2 sn vs ExecuteUpdate ~20ms
```

```csharp
// Toplu silme:
await _context.Kitaplar
    .Where(k => k.IsDeleted && k.SilinmeTarihi < DateTime.UtcNow.AddYears(-1))
    .ExecuteDeleteAsync();
// tek DELETE SQL — change tracker'a yüklemeye gerek yok
```

### Bulk Insert — Binlerce Satır Ekleme

```csharp
// ✗ AddRange — her satır ayrı INSERT:
_context.Kitaplar.AddRange(onBinKitap);   // 10K entity track'lenir
await _context.SaveChangesAsync();         // 10K INSERT (çok yavaş ~8 sn)

// ✓ BulkExtensions paketi — SqlBulkCopy kullanır:
await _context.BulkInsertAsync(onBinKitap);
// 10K satır: ~0.3 sn (25x hızlı)
// ne zaman kullan → veri import, seed, migration
```

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de tablo küçük (100 kitap), index düşünülmedi, Skip/Take yeterli. 50K kullanıcıda aynı yaklaşım:
- Index yok → her sorgu full scan → 200ms+
- Skip(5000) → 5000 satır okunup atılıyor → timeout
- Tek tek güncelleme → toplu fiyat değişikliğinde 30 saniye

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| Non-Clustered Index | Sık filtrelenen 2-3 kolona ekle | Execution plan bazlı sistematik analiz |
| Covering Index | Gereksiz | Key Lookup gördükçe ekle |
| Keyset pagination | Skip/Take yeterli | Büyük tablo + API varsa zorunlu |
| EF.CompileQuery | Gereksiz | Sıcak sorgularda faydalı |
| ExecuteUpdate/Bulk | Nadir | Import/batch job'larda zorunlu |
| Connection Pool tuning | Varsayılan yeterli | Monitoring + Min/Max ayarı |

---

## Kontrol Soruları

1. Clustered ve Non-Clustered index arasındaki fiziksel fark nedir?
2. Covering index ne zaman gerekir? INCLUDE ne yapar?
3. Composite index'te kolon sırası neden önemlidir? (telefon rehberi analojisiyle açıkla)
4. `WHERE YEAR(Tarih) = 2024` neden index kullanamaz? Nasıl düzeltirsin?
5. OFFSET pagination büyük tablolarda neden yavaşlar? Keyset nasıl çözer?
6. Implicit conversion nedir ve index'i nasıl kör eder?
7. ExecuteUpdateAsync ne zaman SaveChangesAsync'ten üstündür?
