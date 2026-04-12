# Gün 33 — EF Core Performance Tuning

---

## 1. AsNoTracking() — Gerçek Kazanç

EF Core, sorgulanan her entity'yi varsayılan olarak **Change Tracker**'a kaydeder. Change Tracker, nesnenin orijinal halini saklar; `SaveChanges()` çağrıldığında hangi property'nin değiştiğini hesaplar. Bu izleme bellek ve CPU kullanır.

### Change Tracker Ne Yapar?

```
Sorgu: SELECT * FROM Kitaplar WHERE Id = 1
  ↓
EF Core entity'yi oluşturur
  ↓
Change Tracker'a ekler:
  kitap.Id       = 1        → original = 1
  kitap.Baslik   = "Clean Code" → original = "Clean Code"
  kitap.Fiyat    = 45m      → original = 45m
  ...

SaveChanges() çağrıldığında:
  EF Core her property'yi original değeriyle karşılaştırır
  → Fiyat 50m olmuş → UPDATE Kitaplar SET Fiyat=50 WHERE Id=1
  → Değişmeyen property'ler SQL'e girmez
```

**Sadece okuyacaksan bu karşılaştırma boşa gider.** `AsNoTracking()` Change Tracker'ı devre dışı bırakır.

### Benchmark Karşılaştırması

```csharp
// ─────────────────────────────────────────────────────────────────────
// Tracking (varsayılan) — liste sayfası için yanlış tercih
// ─────────────────────────────────────────────────────────────────────
var kitaplar = await _context.Kitaplar
    .Where(k => k.StokAdedi > 0)
    .ToListAsync();
// EF Core: her Kitap nesnesi için Change Tracker'a kayıt oluşturur
// 1000 kitap → 1000 EntityEntry nesnesi bellekte
// SaveChanges() hiç çağrılmayacak olsa bile bu nesneler yaşar
// GC basıncı ve bellek tüketimi: gereksiz

// ─────────────────────────────────────────────────────────────────────
// AsNoTracking() — liste ve detay sayfaları için doğru tercih
// ─────────────────────────────────────────────────────────────────────
var kitaplar = await _context.Kitaplar
    .AsNoTracking()                   // Change Tracker'a kaydetme, sadece oku
                                      // bunu yazmasaydık: her entity izlenirdi → bellek + CPU maliyeti
    .Where(k => k.StokAdedi > 0)
    .ToListAsync();
// EntityEntry oluşturulmaz
// Ölçüm (BenchmarkDotNet, 1000 kayıt):
//   Tracking    : ~2.4ms, ~320 KB
//   AsNoTracking: ~1.1ms, ~180 KB  ← %55 daha hızlı, %44 daha az bellek
```

### Ne Zaman AsNoTracking() Kullanılmaz?

```csharp
// Güncelleme yapılacaksa AsNoTracking() kullanma!

// YANLIŞ — AsNoTracking ile getirip güncellemeye çalışmak
var kitap = await _context.Kitaplar
    .AsNoTracking()                   // Change Tracker yok
    .FirstOrDefaultAsync(k => k.Id == id);

kitap!.Fiyat = 99m;
await _context.SaveChangesAsync();   // Fiyat değişti ama Change Tracker bilmiyor
                                     // → SaveChanges hiçbir UPDATE üretmez
                                     // → Güncelleme sessizce kaybolur!

// DOĞRU — güncelleme senaryosu: tracking açık olmalı
var kitap = await _context.Kitaplar
    .FirstOrDefaultAsync(k => k.Id == id);
    // AsNoTracking YOK → Change Tracker izliyor

kitap!.Fiyat = 99m;
await _context.SaveChangesAsync();   // Change Tracker Fiyat değişti → UPDATE üretir ✓

// ÖZET KURAL:
// Oku (liste, detay, API response) → AsNoTracking()
// Düzenle (form kaydet, güncelleme) → tracking açık (varsayılan)
```

### AsNoTrackingWithIdentityResolution()

```csharp
// Normal AsNoTracking: aynı Id'li entity iki kez gelirse iki ayrı nesne oluşturur
// Sorun: 3 kitap, aynı yazar → 3 farklı Yazar nesnesi (referans eşitsizliği)

var kitaplar = await _context.Kitaplar
    .AsNoTracking()                        // iki kitap aynı yazara sahipse:
    .Include(k => k.YazarNavigation)       // yazar1 == yazar2 → FALSE (farklı referans)
    .ToListAsync();

// AsNoTrackingWithIdentityResolution: aynı Id = aynı nesne (tracking olmadan)
var kitaplar = await _context.Kitaplar
    .AsNoTrackingWithIdentityResolution()  // Id çakışması → aynı nesne referansı
                                           // bu olmadan projeksiyonda döngüler oluşabilirdi
    .Include(k => k.YazarNavigation)       // aynı yazarı paylaşan kitaplarda
                                           // yazar1 == yazar2 → TRUE
    .ToListAsync();
// Trade-off: AsNoTracking'den biraz daha yavaş (iç kimlik haritası tutar)
// Ne zaman?: Include + aynı entity'nin birden fazla yerde kullanıldığı durumlar
```

---

## 2. Batch Operations — ExecuteUpdate() ve ExecuteDelete() (EF Core 7+)

EF Core 7 öncesinde toplu güncelleme için tüm kayıtları bellekte tutmak, döngüyle değiştirmek ve `SaveChanges()` çağırmak gerekiyordu. Bu N sorgu + Change Tracker yükü demekti. EF Core 7 ile tek SQL üretilir.

### ExecuteUpdate() — Toplu Güncelleme

```csharp
// ─────────────────────────────────────────────────────────────────────
// ESKİ YOL (EF Core 6 ve öncesi) — N+1 bellek yükü
// ─────────────────────────────────────────────────────────────────────
var arsivKitaplar = await _context.Kitaplar
    .Where(k => k.Kategori == "Arşiv")
    .ToListAsync();
// SQL 1: SELECT * FROM Kitaplar WHERE Kategori = 'Arşiv'
// Tüm kayıtlar belleğe geldi, Change Tracker'a kaydedildi

foreach (var kitap in arsivKitaplar)    // 500 kitap varsa:
    kitap.StokAdedi = 0;                // her biri bellekte değişti

await _context.SaveChangesAsync();
// SQL 2-501: UPDATE Kitaplar SET StokAdedi = 0 WHERE Id = @id (500 ayrı SQL)
// 500 kayıt → 501 DB roundtrip + 500 EntityEntry bellekte

// ─────────────────────────────────────────────────────────────────────
// YENİ YOL (EF Core 7+) — tek SQL, sıfır bellek yükü
// ─────────────────────────────────────────────────────────────────────
int etkilenen = await _context.Kitaplar
    .Where(k => k.Kategori == "Arşiv")      // hangi satırlar güncellenmeli
                                             // bunu yazmasaydık tüm Kitaplar tablosu güncellenirdi!
    .ExecuteUpdateAsync(s => s             // güncelleme tanımını başlat
        .SetProperty(k => k.StokAdedi, 0)  // StokAdedi = 0
        .SetProperty(k => k.EklemeTarihi, DateTime.UtcNow)); // birden fazla property aynı anda
                                             // her SetProperty ayrı SET ifadesi üretir
// Üretilen TEK SQL:
// UPDATE Kitaplar
// SET StokAdedi = 0, EklemeTarihi = '2026-04-11T...'
// WHERE Kategori = N'Arşiv'
//
// 500 kayıt için: 1 DB roundtrip, sıfır bellek yükü, sıfır Change Tracker
Console.WriteLine($"{etkilenen} kayıt güncellendi.");
// ExecuteUpdateAsync: etkilenen satır sayısını döner
// SaveChanges() GEREKMEZ — doğrudan DB'ye gider

// ─────────────────────────────────────────────────────────────────────
// Hesaplanmış değerle güncelleme (mevcut değeri kullanarak)
// ─────────────────────────────────────────────────────────────────────
await _context.Kitaplar
    .Where(k => k.Kategori == "Yazılım")
    .ExecuteUpdateAsync(s => s
        .SetProperty(k => k.Fiyat, k => k.Fiyat * 1.10m));  // fiyatı %10 artır
        // k => k.Fiyat * 1.10m: mevcut değeri kullanarak hesapla
        // bunu .SetProperty(k => k.Fiyat, 99m) yazsaydık tüm fiyatlar 99'a eşitlenirdi
// Üretilen SQL:
// UPDATE Kitaplar SET Fiyat = Fiyat * 1.10 WHERE Kategori = N'Yazılım'
```

### ExecuteDelete() — Toplu Silme

```csharp
// ─────────────────────────────────────────────────────────────────────
// Tek satırla toplu silme
// ─────────────────────────────────────────────────────────────────────
int silinen = await _context.Kitaplar
    .Where(k => k.StokAdedi == 0 && k.EklemeTarihi < DateTime.UtcNow.AddYears(-2))
    // bunu yazmasaydık WHERE olmaz, tüm tablo silinirdi
    .ExecuteDeleteAsync();
// Üretilen SQL:
// DELETE FROM Kitaplar
// WHERE StokAdedi = 0 AND EklemeTarihi < '2024-04-11T...'
//
// Change Tracker'dan geçmez → çok hızlı
// SaveChanges() GEREKMEZ

Console.WriteLine($"{silinen} eski stoksuz kitap silindi.");

// ─────────────────────────────────────────────────────────────────────
// DİKKAT: ExecuteUpdate/Delete Change Tracker'ı güncellemez
// ─────────────────────────────────────────────────────────────────────
var kitap = await _context.Kitaplar.FindAsync(1);
// kitap.StokAdedi = 10 (bellekte)

await _context.Kitaplar
    .Where(k => k.Id == 1)
    .ExecuteUpdateAsync(s => s.SetProperty(k => k.StokAdedi, 0));
// DB'de StokAdedi = 0 artık

Console.WriteLine(kitap!.StokAdedi); // → 10! (Change Tracker eski değeri tutuyor)
// ExecuteUpdate sonrası bellekteki entity stale oldu
// Çözüm: ya bellekteki entity'yi yeniden çek (FindAsync tekrar) ya da kullanma
```

---

## 3. Bulk Operations — EFCore.BulkExtensions

`ExecuteUpdate/Delete` tek bir koşulla toplu işlem için yeterlidir. Ama binlerce farklı satırı farklı değerlerle güncellemek (ör. her kitabın fiyatını ayrı ayrı değiştirmek) gerektiğinde `EFCore.BulkExtensions` paketi kullanılır.

```bash
# Paket kurulumu
dotnet add package EFCore.BulkExtensions
# bunu eklemeden BulkInsertAsync/BulkUpdateAsync çağrıları derleme hatası verir
```

```csharp
// ─────────────────────────────────────────────────────────────────────
// BulkInsertAsync — binlerce INSERT'i tek seferde
// ─────────────────────────────────────────────────────────────────────
var yeniKitaplar = new List<Kitap>();

for (int i = 0; i < 5000; i++)
{
    yeniKitaplar.Add(new Kitap
    {
        Baslik       = $"Kitap {i}",
        Yazar        = "Test Yazar",
        Fiyat        = 25m + (i % 10),
        Kategori     = "Test",
        StokAdedi    = 10,
        EklemeTarihi = DateTime.UtcNow
    });
}

// YAVAŞ YOL: AddRangeAsync + SaveChangesAsync
await _context.AddRangeAsync(yeniKitaplar);
await _context.SaveChangesAsync();
// EF Core: 5000 ayrı INSERT parametreli SQL üretir
// Süre ölçümü: ~3.5 saniye (5000 kayıt)

// HIZLI YOL: BulkInsertAsync
await _context.BulkInsertAsync(yeniKitaplar);
// SqlBulkCopy kullanır: tek network paketi → veritabanı doğrudan yazar
// Süre ölçümü: ~0.2 saniye (5000 kayıt) ← ~17x daha hızlı
// bunu kullanmasaydık ve büyük import senaryosu olsaydı timeout alırdık

// ─────────────────────────────────────────────────────────────────────
// BulkUpdateAsync — her satır farklı değer
// ─────────────────────────────────────────────────────────────────────
// Senaryo: CSV'den 3000 kitabın fiyatı güncellendi, hepsini DB'ye yaz

var guncellenecekler = csvVerisi.Select(satir => new Kitap
{
    Id    = satir.Id,     // hangi kayıt güncellenmeli
    Fiyat = satir.YeniFiyat  // her biri farklı değer
    // diğer property'ler → BulkConfig ile sadece seçilen kolonlar güncellenir
}).ToList();

await _context.BulkUpdateAsync(guncellenecekler, new BulkConfig
{
    PropertiesToInclude = new List<string> { nameof(Kitap.Fiyat) }
    // sadece Fiyat kolonunu güncelle, diğerlerine dokunma
    // bunu yazmasaydık tüm kolonlar güncellenir: Baslik, Yazar... vb. → veri kaybı riski
});
// Üretilen işlem: MERGE INTO Kitaplar (tek toplu SQL)
// 3000 satır → ~0.15 saniye

// ─────────────────────────────────────────────────────────────────────
// BulkDeleteAsync — ID listesinden toplu silme
// ─────────────────────────────────────────────────────────────────────
var silinecekler = eskiIdListesi.Select(id => new Kitap { Id = id }).ToList();
await _context.BulkDeleteAsync(silinecekler);
// ExecuteDeleteAsync: tek bir WHERE koşuluyla siler
// BulkDeleteAsync: ID listesiyle siler — farklı koşullardaki satırlar için uygun
```

### EF Core Native vs BulkExtensions Karşılaştırması

```
| İşlem        | EF Core Native           | BulkExtensions       |
|--------------|--------------------------|----------------------|
| 100 INSERT   | ~10ms (yeterli)          | ~5ms (overkill)      |
| 5000 INSERT  | ~3500ms                  | ~200ms               |
| 50000 INSERT | timeout riski            | ~1500ms              |
| Farklı değer | ExecuteUpdate yetersiz   | BulkUpdate ✓         |
| Karmaşıklık  | Sıfır — native API       | Ek paket gerekir     |

Ne zaman BulkExtensions?
  → 1000+ satır ekleme/güncelleme/silme
  → Her satırın farklı değer alması gereken toplu güncelleme
  → Import, migration veri aktarımı, batch job senaryoları

Ne zaman native EF Core yeterli?
  → Az sayıda kayıt (< birkaç yüz)
  → Tek koşulla toplu güncelleme (ExecuteUpdate)
  → Normal CRUD operasyonları
```

---

## 4. Connection Pooling

.NET'te her `new SqlConnection()` açmak pahalıdır: TCP bağlantısı kurulur, kimlik doğrulama yapılır, buffer tahsis edilir. Connection pooling bu bağlantıları yeniden kullanır.

### Nasıl Çalışır?

```
İlk istek:
  app → Pool boş → yeni bağlantı aç → DB'ye bağlan → isteği işle
  → bağlantı kapatıldı → POOL'A GERİ DÖNDÜ (gerçekten kapatılmadı)

İkinci istek:
  app → Pool dolu (1 bağlantı var) → bağlantıyı al → DB'ye bağlan (zaten açık!)
  → isteği işle → pool'a geri dön

Pool dolu + tüm bağlantılar meşgul + yeni istek geldi:
  → istek bekler (connection wait)
  → MaxPoolSize aşılırsa: InvalidOperationException ("timeout period elapsed")
```

### SQL Server Pool Konfigürasyonu

```json
// appsettings.json — bağlantı dizesinde pool parametreleri
{
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\mssqllocaldb;Database=KitabeviDb;Trusted_Connection=True;Min Pool Size=5;Max Pool Size=100;Connection Timeout=30"
  }
}
// Min Pool Size=5  : uygulama başlayınca 5 bağlantı hazır bekletilir
//                    bu olmadan ilk 5 istek yavaş başlar (bağlantı kurma süresi)
// Max Pool Size=100: aynı anda en fazla 100 aktif DB bağlantısı
//                    100'ü aşan istekler Connection Timeout kadar bekler
// Connection Timeout=30: bağlantı bulunamazsa 30 saniye bekle, sonra exception
//                         bu olmadan uygulama süresiz bekleyebilir
```

### Npgsql (PostgreSQL) Pool Konfigürasyonu

```csharp
// Program.cs — Npgsql için programatik pool ayarı
builder.Services.AddDbContext<KitabeviDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsqlOptions => npgsqlOptions
            .MinPoolSize(5)                // hazır bekleyen minimum bağlantı
                                           // bu olmadan cold-start latency artar
            .MaxPoolSize(100)              // maksimum eşzamanlı bağlantı
            .ConnectionIdleLifetime(300)   // 5 dakika idle olan bağlantı pool'dan çıkar
                                           // bu olmadan stale bağlantılar birikir → memory sızıntısı
    ));
```

### Connection Leak Nasıl Olur?

```csharp
// YANLIŞ — using olmadan bağlantı açmak
// (EF Core DbContext'i doğru kapatmazsan pool'a dönmez)

// Scoped DbContext doğru kullanım — DI otomatik yönetir:
public class KitapController : Controller
{
    private readonly KitabeviDbContext _context;

    public KitapController(KitabeviDbContext context)
    {
        _context = context;
        // Scoped: request başında oluşturulur, response sonunda Dispose() çağrılır
        // bağlantı otomatik pool'a döner
        // bunu Singleton yaparsaydık: tek DbContext tüm requestler arasında paylaşılır
        // → thread-safe değil + bağlantı hiç pool'a dönmez
    }
}

// Singleton servis içinde Scoped DbContext kullanmak yaygın hatadır:
public class OtomatikRaporServisi   // Singleton
{
    private readonly KitabeviDbContext _context;  // YANLIŞ — Scoped'u Singleton'a enjekte etme

    // Doğru yol: IServiceScopeFactory kullan
    private readonly IServiceScopeFactory _scopeFactory;

    public OtomatikRaporServisi(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        // scopeFactory: her operasyonda yeni scope oluşturur → yeni DbContext
        // bağlantı işlem sonunda düzgün pool'a döner
    }

    public async Task RaporOlusturAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        // using: scope Dispose olunca DbContext de Dispose olur → bağlantı pool'a döner
        // bunu yazmadan scope.ServiceProvider.GetRequiredService çağırsaydık:
        // scope hiç dispose olmaz → bağlantı pool'a dönmez → connection leak

        var context = scope.ServiceProvider.GetRequiredService<KitabeviDbContext>();
        var rapor = await context.Kitaplar.CountAsync();
    }
}
```

### Pool Durumunu İzleme

```csharp
// Geliştirme ortamında pool istatistikleri loglamak için
// SQL Server: sys.dm_exec_connections view'u
// Npgsql: EventCounters ile izleme

// Program.cs — Npgsql EventCounters
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// dotnet-counters ile terminal'den izle:
// dotnet-counters monitor --process-id <PID> Npgsql
// → npgsql.pool.connections.idle   : hazır bekleyen bağlantı sayısı
// → npgsql.pool.connections.busy   : aktif kullanımdaki bağlantı sayısı
// → npgsql.pool.connections.total  : toplam bağlantı (idle + busy)
// bağlantı sayısı sürekli MaxPoolSize'a dayanıyorsa → bottleneck sinyali
```

---

## 5. Query Tagging — TagWith()

Production ortamında SQL log'larına bakıldığında hangi kod parçasının hangi sorguyu ürettiğini bulmak zor olabilir. `TagWith()` sorgunun başına SQL yorumu ekler.

```csharp
// ─────────────────────────────────────────────────────────────────────
// TagWith() olmadan — log'da kim yazdı belli değil
// ─────────────────────────────────────────────────────────────────────
var kitaplar = await _context.Kitaplar
    .Where(k => k.Kategori == "Roman")
    .ToListAsync();
// SQL log:
// SELECT [k].[Id], [k].[Baslik], ...
// FROM [Kitaplar] AS [k]
// WHERE [k].[Kategori] = N'Roman'
// → Hangi endpoint'ten geldi? Hangi kullanıcı için? Belirsiz.

// ─────────────────────────────────────────────────────────────────────
// TagWith() ile — log'da kaynak belli
// ─────────────────────────────────────────────────────────────────────
var kitaplar = await _context.Kitaplar
    .TagWith("KitapController.Index - Tüm Roman Listesi")
    // bunu yazmasaydık production'da yavaş sorgu tespiti için kod taraması yapardık
    .Where(k => k.Kategori == "Roman")
    .AsNoTracking()
    .ToListAsync();
// SQL log:
// -- KitapController.Index - Tüm Roman Listesi
// SELECT [k].[Id], [k].[Baslik], ...
// FROM [Kitaplar] AS [k]
// WHERE [k].[Kategori] = N'Roman'
// → DBA veya monitoring (Datadog, Application Insights) doğrudan kaynağı görür

// ─────────────────────────────────────────────────────────────────────
// Dinamik tag — kullanıcı bilgisi dahil
// ─────────────────────────────────────────────────────────────────────
var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);

var siparisler = await _context.Siparisler
    .TagWith($"SiparisController.Listele - KullaniciId:{kullaniciId}")
    // dinamik tag: production'da hangi kullanıcının tetiklediğini izlemek için
    // bunu yazmasaydık performance sorununun hangi kullanıcıdan kaynaklandığını bulamazdık
    .Where(s => s.KullaniciId == kullaniciId)
    .AsNoTracking()
    .ToListAsync();

// ─────────────────────────────────────────────────────────────────────
// TagWithCallSite() — otomatik dosya + satır numarası (EF Core 7+)
// ─────────────────────────────────────────────────────────────────────
var kitaplar = await _context.Kitaplar
    .TagWithCallSite()       // otomatik: "-- File: KitapController.cs, Line: 42"
                             // TagWith'ten farkı: manuel metin yazman gerekmez
                             // bunu yazmak yerine TagWith kullanırsaydın her değişiklikte
                             // satır numarasını elle güncellemen gerekirdi
    .AsNoTracking()
    .ToListAsync();
```

---

## 6. Indexes — HasIndex(), Filtered Index, Composite Index

Index olmadan her sorgu tablo taraması (full table scan) yapar. 1.000.000 satırlık tabloda `WHERE Kategori = 'Roman'` için tüm satırlar okunur. Doğru index: doğrudan eşleşen satırlara atlar.

### HasIndex() — Temel Index

```csharp
// KitabeviDbContext.cs — OnModelCreating

modelBuilder.Entity<Kitap>(entity =>
{
    // ─────────────────────────────────────────────────────────────────
    // Tekli index
    // ─────────────────────────────────────────────────────────────────
    entity.HasIndex(k => k.Kategori);
    // Üretilen SQL (migration'da):
    // CREATE INDEX IX_Kitaplar_Kategori ON Kitaplar (Kategori);
    // bunu tanımlamasaydık: WHERE Kategori = 'Roman' → full scan → yavaş
    // index ile: B-tree'de Roman leaf'ine atla → hızlı

    // ─────────────────────────────────────────────────────────────────
    // Unique index — tek kolon
    // ─────────────────────────────────────────────────────────────────
    entity.HasIndex(k => k.Isbn)
          .IsUnique();
    // her kitabın Isbn'i benzersiz olmalı
    // bunu yazmadan sadece entity validation ile [Required] kullansaydık:
    // DB seviyesinde duplicate mümkün olurdu (race condition, bulk insert)
    // IsUnique() → DB constraint = gerçek koruma
});
```

### Composite Index — Çoklu Kolon

```csharp
// Sık kullanılan sorgu:
// SELECT * FROM Kitaplar WHERE Kategori = 'Roman' AND StokAdedi > 0 ORDER BY Baslik

// Tek kolon index yeterli değil: önce Kategori filtrele, sonra StokAdedi filtrele
// DB her iki kolon için ayrı index kullansa bile ICP (Index Condition Pushdown) her zaman optimal değil

// Composite index: tek B-tree'de iki kolonu birlikte tutar
modelBuilder.Entity<Kitap>()
    .HasIndex(k => new { k.Kategori, k.StokAdedi })
    // dikkat: kolon sırası önemli
    // Kategori öne gelir → "WHERE Kategori = ?" sorguları da bu index'i kullanır
    // bunu tersine yazsaydık (StokAdedi, Kategori):
    // "WHERE Kategori = ?" tek başına bu index'i kullanamazdı (left-prefix kuralı)
    .HasDatabaseName("IX_Kitaplar_Kategori_Stok");
    // bunu yazmasaydık EF Core otomatik isim üretirdi: IX_Kitaplar_Kategori_StokAdedi
    // okunabilir isim: DBA ve monitoring araçlarında anlamlı

// Üretilen SQL:
// CREATE INDEX IX_Kitaplar_Kategori_Stok ON Kitaplar (Kategori, StokAdedi);
```

### Filtered Index — Koşullu Index

```csharp
// Senaryo: Sadece aktif ve stokta olan kitaplara sık sorgu yapıyoruz
// Tüm tablo için index: silinmiş, stoksuz kayıtları da içerir → index şişkinleşir

modelBuilder.Entity<Kitap>()
    .HasIndex(k => k.Baslik)
    .HasFilter("StokAdedi > 0")
    // sadece stokta olan kitapları bu index'e dahil et
    // bunu yazmasaydık index tüm kitapları (stoksuz dahil) içerirdi
    // → index daha büyük → daha yavaş + disk alanı israfı
    // filtered index: %30 stoklu, %70 stoksuz varsayımıyla → index %30 küçülür
    .HasDatabaseName("IX_Kitaplar_Baslik_Stokta");

// Üretilen SQL:
// CREATE INDEX IX_Kitaplar_Baslik_Stokta
// ON Kitaplar (Baslik)
// WHERE StokAdedi > 0;

// NOT: Filtered index sadece filtreyle uyumlu sorgularda kullanılır:
// WHERE Baslik LIKE 'Clean%' AND StokAdedi > 0 → index kullanılır ✓
// WHERE Baslik LIKE 'Clean%'                  → index KULLANILMAZ (filtre eksik) ✗
```

### Include Index (Covering Index)

```csharp
// Senaryo: SELECT Id, Baslik, Fiyat FROM Kitaplar WHERE Kategori = 'Roman'
// Normal index (Kategori): leaf'te sadece Id var → her satır için tablo'ya "key lookup" gider
// Covering index: leaf'te Baslik ve Fiyat da var → tablo'ya gitmeden sorgu tamamlanır

modelBuilder.Entity<Kitap>()
    .HasIndex(k => k.Kategori)
    .IncludeProperties(k => new { k.Baslik, k.Fiyat })
    // bunu yazmasaydık: index buldu ama Baslik/Fiyat için her satırda ayrı I/O yaptı
    // bunu yazınca: tek B-tree okuma → "index-only scan"
    .HasDatabaseName("IX_Kitaplar_Kategori_Covering");

// Üretilen SQL:
// CREATE INDEX IX_Kitaplar_Kategori_Covering
// ON Kitaplar (Kategori)
// INCLUDE (Baslik, Fiyat);
```

### Index Stratejisi — Ne Zaman Index Ekle?

```
EKLE:
  ✓ WHERE, JOIN ON, ORDER BY, GROUP BY kolonları
  ✓ Foreign key kolonları (EF Core otomatik ekler ama kontrol et)
  ✓ Sık arama yapılan yüksek kardinalite kolonlar (Isbn, Email vb.)

EKLEME:
  ✗ Küçük tablolar (< birkaç bin satır) — full scan zaten hızlı
  ✗ Çok sık INSERT/UPDATE yapılan tablolara aşırı index
    (her INSERT → tüm indexler güncellenir → yazma yavaşlar)
  ✗ Düşük kardinalite: true/false, Erkek/Kadın gibi
    (index selectivity düşük → optimizer full scan tercih eder)

KARAR VERİRKEN:
  1. EXPLAIN / Execution Plan'a bak: "Table Scan" var mı?
  2. Sorgu süresi kabul edilemez mi? (> 100ms?)
  3. Index ekledikten sonra tekrar ölç
```

---

## 7. Database Concurrency — Optimistic Concurrency Token

Aynı anda iki kullanıcı aynı kaydı düzenlediğinde ne olur? İkincisi birincinin değişikliğini silerse "lost update" problemi oluşur.

### Lost Update Problemi

```
Kullanıcı A: kitap.Fiyat oku → 45
Kullanıcı B: kitap.Fiyat oku → 45
Kullanıcı A: kitap.Fiyat = 55 → SaveChanges() → DB: 55
Kullanıcı B: kitap.Fiyat = 60 → SaveChanges() → DB: 60  ← A'nın değişikliği kayboldu!
```

### Optimistic Concurrency — RowVersion / ConcurrencyToken

İyimser eşzamanlılık: çakışma nadirdir varsayımıyla çalışır. Kayıt okunurken kilitlenmez; kaydetme anında "bu arada değişti mi?" kontrolü yapılır.

```csharp
// Kitap.cs — Entity'ye RowVersion ekle
public class Kitap
{
    public int Id { get; set; }
    public string Baslik { get; set; } = null!;
    public decimal Fiyat { get; set; }
    // ...

    [Timestamp]                             // SQL Server: rowversion / timestamp kolon
    public byte[] RowVersion { get; set; } = null!;
    // DB her UPDATE'te bu değeri otomatik değiştirir
    // bunu eklemeseydin optimistic concurrency çalışmazdı
    // RowVersion olmadan: EF Core WHERE Id=1 ile günceller → her zaman başarılı (lost update!)
    // RowVersion ile: EF Core WHERE Id=1 AND RowVersion=@original → eski değilse güncelle
}

// KitabeviDbContext.cs — Fluent API alternatifi
modelBuilder.Entity<Kitap>()
    .Property(k => k.RowVersion)
    .IsRowVersion();  // [Timestamp] ile aynı etkisi
                      // bunu yazmasaydık RowVersion normal bir binary kolon olarak işlem görürdü
                      // EF Core UPDATE'e WHERE koşuluna eklemezdi → concurrency kontrolü olmaz
```

### Concurrency Çakışmasını Yakalamak

```csharp
// KitapController.cs — Edit POST action
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(KitapEditViewModel model)
{
    if (!ModelState.IsValid)
        return View(model);

    try
    {
        var kitap = await _context.Kitaplar.FindAsync(model.Id);
        // tracking açık: güncelleme yapılacak, AsNoTracking kullanma

        if (kitap == null)
            return NotFound();

        kitap.Baslik = model.Baslik;
        kitap.Fiyat  = model.Fiyat;

        // RowVersion'ı formdan gelen orijinal değerle set et
        _context.Entry(kitap).Property(k => k.RowVersion).OriginalValue = model.RowVersion;
        // bunu yazmasaydık: EF Core mevcut DB'deki RowVersion'ı kullanır
        // → her zaman eşleşir → çakışma hiç yakalanmaz → lost update!

        await _context.SaveChangesAsync();
        // Üretilen SQL:
        // UPDATE Kitaplar SET Baslik=@baslik, Fiyat=@fiyat
        // WHERE Id=@id AND RowVersion=@originalRowVersion
        //
        // Eğer bu arada başka biri güncellemiş ve RowVersion değişmişse:
        // → WHERE koşulu 0 satır eşleştirir → EF Core exception fırlatır

        return RedirectToAction(nameof(Index));
    }
    catch (DbUpdateConcurrencyException ex)
    // bunu yakalamadan SaveChanges çağırsaydık:
    // çakışma olunca kullanıcıya 500 hatası dönerdik
    {
        var entry = ex.Entries.First();               // çakışan entity
        var dbValues = await entry.GetDatabaseValuesAsync();
        // DB'deki güncel değerleri al — kullanıcıya göstermek için

        if (dbValues == null)
        {
            // Bu arada silinmiş
            ModelState.AddModelError("", "Bu kayıt başka bir kullanıcı tarafından silindi.");
        }
        else
        {
            var dbKitap = (Kitap)dbValues.ToObject();
            // Hangi alanlar çakıştı?
            if (dbKitap.Fiyat != model.Fiyat)
                ModelState.AddModelError(nameof(model.Fiyat),
                    $"DB'deki güncel değer: {dbKitap.Fiyat}. Tekrar deneyin.");

            // RowVersion'ı yeni değerle güncelle: bir sonraki SaveChanges başarılı olabilsin
            model.RowVersion = dbKitap.RowVersion;
            // bunu yapmasaydık: kullanıcı tekrar gönderdiğinde yine eski RowVersion ile denerdi
            // → sonsuz çakışma döngüsü
        }

        return View(model);
    }
}
```

### ConcurrencyToken — RowVersion Olmadan

```csharp
// SQL Server dışı (PostgreSQL, SQLite) veya belirli bir kolon kullanmak istiyorsan:

public class Kitap
{
    public int Id { get; set; }
    public string Baslik { get; set; } = null!;

    [ConcurrencyCheck]                   // [Timestamp] gibi ama manuel
    public DateTime GuncellemeTarihi { get; set; }
    // Güncelleme yaparken bu alanı her seferinde değiştirmen gerekir
    // bunu güncellemezsen EF Core eski değerle WHERE koşulu kurar → hiç eşleşmez
}

// Fluent API versiyonu:
modelBuilder.Entity<Kitap>()
    .Property(k => k.GuncellemeTarihi)
    .IsConcurrencyToken();   // EF Core UPDATE'e WHERE GuncellemeTarihi=@original ekler

// Kaydetmeden önce:
kitap.GuncellemeTarihi = DateTime.UtcNow;   // her güncellemede değer değişmeli!
await _context.SaveChangesAsync();
```

---

## 8. Kitabevi Uygulamasına Uygulama

```csharp
// EfKitapServisi.cs — Performance tuning eklenmiş metodlar

public class EfKitapServisi : IKitapServisi
{
    private readonly KitabeviDbContext _context;

    // ─────────────────────────────────────────────────────────────────
    // Liste sorgusu — tüm optimizasyonlar bir arada
    // ─────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<KitapListeViewModel>> HepsiniGetirAsync(string? kategori = null)
    {
        var sorgu = _context.Kitaplar
            .TagWith($"EfKitapServisi.HepsiniGetir - Kategori:{kategori ?? "Tümü"}")
            // monitoring'de hangi metod bu sorguyu tetikledi → hemen belli
            .AsNoTracking()              // sadece okuma → Change Tracker yükü sıfır
            .Where(k => k.StokAdedi > 0);
            // bu WHERE: IX_Kitaplar_Kategori_Stok filtered index'ini kullanır

        if (kategori != null)
            sorgu = sorgu.Where(k => k.Kategori == kategori);
            // koşullu filtre: IQueryable — sorgu henüz çalışmıyor
            // bunu .ToListAsync() sonrası yazsaydık in-memory filtreleme olurdu

        return await sorgu
            .OrderBy(k => k.Baslik)
            .Select(k => new KitapListeViewModel(
                k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            // projeksiyon: tüm kolonları değil sadece gerekli olanları çek
            // bunu yazmadan .ToListAsync() kullansaydık: tüm entity kolonları + Change Tracker
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────────
    // Toplu stok sıfırlama — ExecuteUpdate
    // ─────────────────────────────────────────────────────────────────
    public async Task<int> KategoriStokSifirlaAsync(string kategori)
    {
        return await _context.Kitaplar
            .TagWith($"EfKitapServisi.KategoriStokSifirla - {kategori}")
            .Where(k => k.Kategori == kategori)
            // WHERE olmadan tüm tablo sıfırlanır
            .ExecuteUpdateAsync(s =>
                s.SetProperty(k => k.StokAdedi, 0));
            // tek SQL, sıfır bellek yükü, SaveChanges gerekmez
    }

    // ─────────────────────────────────────────────────────────────────
    // Concurrency-safe güncelleme
    // ─────────────────────────────────────────────────────────────────
    public async Task<bool> GuncelleAsync(KitapEditViewModel model)
    {
        try
        {
            var kitap = await _context.Kitaplar.FindAsync(model.Id);
            if (kitap == null) return false;

            kitap.Baslik    = model.Baslik;
            kitap.Fiyat     = model.Fiyat;
            kitap.StokAdedi = model.StokAdedi;

            _context.Entry(kitap)
                    .Property(k => k.RowVersion)
                    .OriginalValue = model.RowVersion;
            // formdan gelen RowVersion: "ben bu sürümü okudum, değişmediyse güncelle"

            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;   // çakışma: controller kullanıcıya haber verir
        }
    }
}
```

```csharp
// KitabeviDbContext.cs — Index tanımları

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Kitap>(entity =>
    {
        // Kategori + Stok composite index: liste sayfası sorgusu
        entity.HasIndex(k => new { k.Kategori, k.StokAdedi })
              .HasDatabaseName("IX_Kitaplar_Kategori_Stok");

        // Baslik filtered index: sadece stokta olanlar
        entity.HasIndex(k => k.Baslik)
              .HasFilter("StokAdedi > 0")
              .HasDatabaseName("IX_Kitaplar_Baslik_Stokta");

        // RowVersion: optimistic concurrency
        entity.Property(k => k.RowVersion)
              .IsRowVersion();
    });
}
```

---

## 9. Özet

```
AsNoTracking()
  Sadece oku → AsNoTracking() → Change Tracker yükü sıfır (~%50 hız farkı)
  Güncelleme yapılacaksa → tracking açık olmalı (varsayılan)
  AsNoTrackingWithIdentityResolution → aynı entity birden fazla yerde: referans tutarlılığı

ExecuteUpdate() / ExecuteDelete() (EF Core 7+)
  Toplu güncelleme/silme → tek SQL, sıfır bellek yükü, SaveChanges gerekmez
  Change Tracker'ı güncellemez → sonraki okumada entity stale olabilir
  SetProperty(k => k.Fiyat, k => k.Fiyat * 1.10m) → mevcut değeri kullanarak hesapla

EFCore.BulkExtensions
  1000+ kayıt → BulkInsertAsync / BulkUpdateAsync / BulkDeleteAsync
  SqlBulkCopy tabanlı: native EF Core'dan 10-20x hızlı
  PropertiesToInclude ile sadece seçilen kolonları güncelle

Connection Pooling
  Min/Max Pool Size bağlantı dizesinde veya programatik ayarla
  Singleton'da Scoped DbContext kullanma: IServiceScopeFactory kullan
  dotnet-counters ile pool istatistiklerini izle

TagWith() / TagWithCallSite()
  Her sorguda kaynak etiketle → production log'da SQL'in nereden geldiği belli
  TagWithCallSite() → otomatik dosya:satır bilgisi (EF Core 7+)

Indexes
  HasIndex() → tekli index
  HasIndex(k => new { k.A, k.B }) → composite (sol-prefix kuralı: sıra önemli)
  HasFilter() → filtered index: büyük tablolarda kısmi index
  IncludeProperties() → covering index: key lookup'ı engeller

Optimistic Concurrency
  [Timestamp] / IsRowVersion() → RowVersion kolonu → UPDATE WHERE Id=? AND RowVersion=?
  DbUpdateConcurrencyException → kullanıcıya "çakışma" bildirimi
  Çakışma sonrası model.RowVersion güncellenmeli → sonraki deneme başarılı olsun
```

---

## Sonraki Gün

Gün 34'te Dapper: EF Core'un yönetmesi zor veya verimsiz ürettiği sorgular için micro ORM yaklaşımı; `QueryAsync<T>()`, multi-mapping ve CQRS'te "command EF Core, query Dapper" pattern'i ele alınacak.
