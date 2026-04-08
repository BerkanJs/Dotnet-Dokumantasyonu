# Gün 29 — EF Core Mimarisi: DbContext ve Change Tracker

---

## 1. EF Core Nedir?

Entity Framework Core, .NET için bir ORM (Object-Relational Mapper). Veritabanı tablolarını C# sınıflarıyla eşleştirir; sen SQL yazmak yerine LINQ yazarsın, EF Core SQL'e çevirir.

```
C# LINQ sorgusu → EF Core → SQL → Veritabanı → C# nesneleri
```

**Günlük hayat benzetmesi:** Google Translate gibi düşün. Sen Türkçe konuşursun (C# / LINQ), Google Translate İngilizceye çevirir (SQL), karşı taraf (veritabanı) anlar ve cevabı geri çevirir. EF Core bu çeviri katmanıdır.

**Neden öğrenmesi önemli?**
- ASP.NET Core uygulamalarında %80+ veri erişimi EF Core üzerinden gider
- Yanlış kullanılırsa N+1 query, full table scan, memory taşması gibi sorunlara yol açar
- Doğru kullanılırsa verimli, okunabilir, test edilebilir kod üretir

---

## 2. DbContext — Alışveriş Sepeti Gibi Düşün

`DbContext`, EF Core'un kalbidir. Bunu bir **alışveriş sepeti** gibi düşün:

- Markete giriyorsun → `DbContext` oluşturuluyor
- Sepete ürün atıyorsun → `.Add()`, `.Remove()`, değişiklik yapıyorsun
- Kasaya gelip ödüyorsun → `SaveChanges()` → veritabanına yazılıyor
- Marketten çıkıyorsun → `DbContext` dispose ediliyor

Sepete attığın her şey, kasaya gelene kadar gerçek değil. Kasada tek seferde tümü işleniyor.

`DbContext` iki klasik pattern'i aynı anda uygular:

**Unit of Work:** Birden fazla değişikliği toplar, tek `SaveChanges()` ile hepsini atomik olarak veritabanına yazar.
> Benzetme: Bankaya para yatırma. "Hesabımdan 500 TL düş, kardeşimin hesabına 500 TL ekle" — bu iki işlem ya birlikte olur ya da hiç olmaz.

**Repository:** Her `DbSet<T>` bir entity koleksiyonuna erişim noktasıdır.
> Benzetme: Kütüphanedeki raf sistemi. `Kitaplar` rafı, `Yazarlar` rafı — her raf kendi türündeki nesneleri tutar.

```csharp
public class KitabeviDbContext : DbContext
{
    // Her DbSet bir veritabanı tablosunu temsil eder.
    // KitapServisi bu property üzerinden sorgu yapar.
    public DbSet<Kitap> Kitaplar { get; set; }
    public DbSet<Yazar> Yazarlar { get; set; }
    public DbSet<Siparis> Siparisler { get; set; }

    public KitabeviDbContext(DbContextOptions<KitabeviDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tablo adı, kolon özellikleri, ilişkiler burada tanımlanır.
        modelBuilder.Entity<Kitap>()
            .HasIndex(k => k.ISBN)
            .IsUnique();

        modelBuilder.Entity<Kitap>()
            .Property(k => k.Fiyat)
            .HasPrecision(18, 2); // Para birimi → decimal hassasiyeti
    }
}
```

**DI container'a kayıt (Program.cs):**

```csharp
builder.Services.AddDbContext<KitabeviDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
```

Bu satır şunu yapar: her HTTP request için yeni bir `KitabeviDbContext` örneği oluştur, request bitince dispose et. Yani **Scoped** lifetime.

---

## 3. Singleton, Scoped, Transient — Garson Analojisi

Bu üç kavram ASP.NET Core'da servislerin "kaç kere oluşturulacağını" belirtir. DbContext özelinde hangisinin doğru olduğunu anlamak için garson benzetmesini kullan.

---

### Singleton — Tüm Restoran İçin Tek Garson

```csharp
// YANLIŞ — asla yapma
builder.Services.AddSingleton<KitabeviDbContext>();
```

**Ne demek:** Uygulama ayağa kalktığında **bir tane** `DbContext` oluşturulur ve uygulama kapanana kadar **aynı nesne** herkese verilir.

**Günlük hayat:** Düşün ki bir restoranda sadece **bir tane garson** var ve tüm masalara o bakıyor. 1. masa "şu yemeği getir" diyor, 2. masa "şunu kaldır" diyor. Garson kafası karışıyor, yanlış masaya yanlış sipariş gidiyor.

**DbContext için neden sorun?**
- DbContext **thread-safe değildir** — aynı anda iki kullanıcı aynı context'i kullanırsa veri bozulur
- Change Tracker sürekli büyür, bellek sızdırır (hiç boşalmıyor çünkü)
- Kullanıcı A'nın yarım kalan değişiklikleri Kullanıcı B'nin SaveChanges'ında veritabanına yazılabilir

---

### Transient — Her Adımda Yeni Garson

```csharp
// YANLIŞ — kaçın
builder.Services.AddTransient<KitabeviDbContext>();
```

**Ne demek:** Bir servis her **inject** edildiğinde **yeni bir nesne** oluşturulur. Aynı request içinde 3 servis DbContext istese, 3 farklı DbContext oluşur.

**Günlük hayat:** Sipariş verirken garson değişiyor. İlk yemeği A garsona söyledin, ikinci yemeği B garsona söyledin, hesabı C garson kesiyor. C garson ne söylediğinden habersiz.

**DbContext için neden sorun?**
- Aynı request içinde Controller, Service, Repository farklı DbContext kullanır
- `SaveChanges()` bazı değişiklikleri görmez çünkü farklı "alışveriş sepeti"nde
- Unit of Work pattern'i çöker — atomik işlem garanti edilemez

---

### Scoped — Her Müşteriye Kendi Garsonu ✅

```csharp
// DOĞRU — AddDbContext zaten bunu yapar
builder.Services.AddDbContext<KitabeviDbContext>(...);
// Arka planda: AddScoped<KitabeviDbContext>()
```

**Ne demek:** Her HTTP request için **bir tane** `DbContext` oluşturulur. O request boyunca her servis **aynı context'i** paylaşır. Request bitince context dispose edilir.

**Günlük hayat:** Restorana girdin, sana bir garson atandı. Starter, ana yemek, tatlı — hepsini **aynı garson** takip ediyor. Hesabı o kesiyor. Masadan kalkınca garson serbest.

```
HTTP Request gelir       → Garson atandı (DbContext oluşturuldu)
  Controller             → Aynı garson
  Service                → Aynı garson
  Repository             → Aynı garson
SaveChanges()            → Hesap kesildi (tüm değişiklikler yazıldı)
HTTP Response döner      → Garson serbest (DbContext dispose edildi)
```

**Doğru yaklaşım:** Her zaman Scoped. `AddDbContext` zaten bunu yapar.

---

## 4. Change Tracker — Akıllı Not Defteri

Change Tracker, DbContext'in hangi nesneleri takip ettiğini ve bu nesnelerin durumunu bilen alt sistemidir. `SaveChanges()` çağrıldığında Change Tracker'a bakılır ve uygun SQL üretilir.

**Günlük hayat benzetmesi:** Bir not defteri tut. İşe gelirken masanın fotoğrafını çek (orijinal durum). Gün içinde masaya bir şey eklersen "eklendi" yaz, bir şeyi değiştirirsen "değişti" yaz, bir şeyi kaldırırsan "silindi" yaz. Gün sonunda not defterindeki değişiklikleri gerçek hayata yansıt.

EF Core tam olarak bunu yapar: entity yüklendiğinde orijinal değerlerin bir kopyasını saklar. `SaveChanges()` çağrıldığında mevcut değerleri orijinalle karşılaştırır.

### Entity States

| Durum | Anlamı | Benzetme | Üretilen SQL |
|-------|--------|----------|-------------|
| `Detached` | Context bunun varlığından habersiz | Not defterinde yok | Hiç SQL üretilmez |
| `Unchanged` | Takip ediliyor, değişiklik yok | "Değişmedi" notu | Hiç SQL üretilmez |
| `Added` | Yeni eklenecek | "Yeni eklendi" notu | `INSERT` |
| `Modified` | Değiştirildi | "Değişti" notu | `UPDATE` |
| `Deleted` | Silinecek | "Silinecek" notu | `DELETE` |

```csharp
var kitap = new Kitap { Ad = "Clean Code", Fiyat = 150 };

// Context bu kitabı bilmiyor — not defterinde yok → Detached
Console.WriteLine(context.Entry(kitap).State); // Detached

context.Kitaplar.Add(kitap);
// Not defterine "yeni kitap var" yazıldı → Added
Console.WriteLine(context.Entry(kitap).State); // Added

await context.SaveChangesAsync();
// INSERT çalıştı, artık veritabanında var → Unchanged
Console.WriteLine(context.Entry(kitap).State); // Unchanged

kitap.Fiyat = 200;
// Fiyat değişti, EF Core fark etti → Modified
Console.WriteLine(context.Entry(kitap).State); // Modified

await context.SaveChangesAsync();
// UPDATE Kitaplar SET Fiyat = 200 WHERE Id = 1
```

### Change Tracker nasıl fark eder?

```csharp
var kitap = await context.Kitaplar.FindAsync(1);
// EF Core: "orijinal Fiyat = 150" → hafızaya alındı (fotoğraf çekildi)

kitap.Fiyat = 200;
// EF Core: mevcut 200 ≠ orijinal 150 → Modified olarak işaretledi

await context.SaveChangesAsync();
// UPDATE Kitaplar SET Fiyat = 200 WHERE Id = 1
// Sadece değişen kolonu günceller, gereksiz SQL üretmez
```

**Önemli:** `Detached` durumdaki nesneyi güncellemek için `context.Update()` veya `context.Attach()` kullanılır:

```csharp
// API controller'dan gelen DTO → context hiç görmedi → Detached
public async Task GuncelleAsync(KitapDto dto)
{
    var kitap = new Kitap
    {
        Id = dto.Id,
        Ad = dto.Ad,
        Fiyat = dto.Fiyat
    };

    // context.Update → tüm kolonları günceller (Modified)
    context.Update(kitap);

    // Ya da sadece belirli property'leri güncellemek istersen:
    // context.Attach(kitap);  // "takip etmeye başla ama değişmedi say"
    // context.Entry(kitap).Property(k => k.Fiyat).IsModified = true; // sadece fiyat UPDATE'lenir

    await context.SaveChangesAsync();
}
```

---

## 5. SaveChanges() — Kasa Gibi Çalışır

`SaveChanges()` / `SaveChangesAsync()` varsayılan olarak bir transaction açar, tüm değişiklikleri yazar, transaction'ı kapatır.

**Günlük hayat benzetmesi:** Banka havalesi. "Kendi hesabımdan 1000 TL düş, arkadaşımın hesabına 1000 TL ekle." Bu iki işlem ya **birlikte başarılı olur** ya da **ikisi de geri alınır**. Birinin başarılı olup diğerinin başarısız olması kabul edilemez.

```csharp
// Senaryo: Sipariş ver → stoğu düşür
var siparis = new Siparis { KitapId = 1, Adet = 3 };
context.Siparisler.Add(siparis);

var kitap = await context.Kitaplar.FindAsync(1);
kitap.StokAdedi -= 3;

// Her iki değişiklik AYNI transaction'da yazar.
// Biri başarısız olursa ikisi de geri alınır.
await context.SaveChangesAsync();
```

**Birden fazla SaveChanges() çağrısı ayrı transaction'dır:**

```csharp
context.Kitaplar.Add(kitap1);
await context.SaveChangesAsync(); // 1. transaction → kitap1 eklendi (kesinleşti)

context.Kitaplar.Add(kitap2);
await context.SaveChangesAsync(); // 2. transaction → kitap2 eklendi (kesinleşti)
// kitap1 ile kitap2 AYRI transaction'larda — biri başarısız olsa diğeri geri alınmaz!
```

İki işlemin aynı transaction'da olmasını istiyorsan `IDbContextTransaction` kullan:

```csharp
await using var transaction = await context.Database.BeginTransactionAsync();
try
{
    context.Siparisler.Add(siparis);
    await context.SaveChangesAsync();  // henüz kesinleşmedi

    context.Stoklar.Update(stok);
    await context.SaveChangesAsync();  // henüz kesinleşmedi

    await transaction.CommitAsync();   // ikisi birden kesinleşti ✅
}
catch
{
    await transaction.RollbackAsync(); // ikisi birden geri alındı ✅
    throw;
}
```

---

## 6. AsNoTracking() — "Sadece Bak, Dokunma" Modu

Her EF Core sorgusu varsayılan olarak dönen nesneleri Change Tracker'a ekler. Bu gereksiz bellek ve CPU harcar — eğer nesneleri sadece okuyacaksan.

**Günlük hayat benzetmesi:** Müzede tablo izliyorsun. Normal müzede rehber her tabloyu not defterine yazıyor (tracking). `AsNoTracking` müzede rehberin not tutmadan sadece göstermesi — sonunda daha az efor, aynı bilgi.

```csharp
// Varsayılan: Change Tracker'a eklenir → bellek kullanımı artar
var kitaplar = await context.Kitaplar.ToListAsync();

// AsNoTracking(): Change Tracker'a eklenmiyor → %10-30 daha hızlı
var kitaplar = await context.Kitaplar
    .AsNoTracking()
    .ToListAsync();
```

**Ne zaman `AsNoTracking()` kullan?**

- Liste/rapor sayfaları — sadece okuma, güncelleme yok
- API'de DTO'ya maplediğin sorgular — entity'yi güncellemeyeceksin
- Yüksek trafik endpoint'leri — her ms önemli

**Ne zaman kullanMA?**

- Entity'yi güncelleyeceksen — Change Tracker takip etmeli, aksi halde farkı göremez
- İlişkili entity'leri de güncelleyeceksen

```csharp
// Güncelleme senaryosu → AsNoTracking KULLANMA
var kitap = await context.Kitaplar.FindAsync(id); // tracking açık
kitap.Fiyat = 200;
await context.SaveChangesAsync(); // Change Tracker: Modified → UPDATE ✅

// Listeleme senaryosu → AsNoTracking KULLAN
var kitaplar = await context.Kitaplar
    .AsNoTracking()
    .Where(k => k.Fiyat < 100)
    .Select(k => new KitapListeDto { Id = k.Id, Ad = k.Ad })
    .ToListAsync();
```

**Global AsNoTracking** — yalnızca okuma ağırlıklı servislerde:

```csharp
builder.Services.AddDbContext<KitabeviDbContext>(options =>
    options.UseSqlServer(connStr)
           .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
// Tüm sorgular varsayılan olarak NoTracking olur
// Güncelleme gerektiğinde context.Update(entity) kullan
```

---

## 7. Özet: DbContext Kullanım Kuralları

```
1. DbContext her zaman Scoped olmalı → AddDbContext bunu yapar
   (Her request'e kendi garsonu)

2. Aynı request → aynı DbContext → Unit of Work
   (Garson tüm siparişleri takip eder)

3. SaveChanges() transaction açar, tüm değişiklikleri atomik yazar
   (Banka havalesi gibi: ya hepsi olur ya hiçbiri)

4. Sadece okuyacaksan → AsNoTracking()
   (Rehber not tutmadan göster)

5. API'den gelen Detached entity → context.Update() veya Attach + IsModified
   (Context tanımıyor, elle tanıt)

6. Change Tracker'ı anla → hangi state'de ne SQL üretilir
   (Not defteri: eklendi/değişti/silindi)
```

---

## 8. Kitabevi Uygulamasında Kullanım

```csharp
// KitapServisi — servis katmanında DbContext kullanımı
public class KitapServisi
{
    private readonly KitabeviDbContext _context;

    public KitapServisi(KitabeviDbContext context)
    {
        _context = context; // DI → Scoped, her request taze context (yeni garson)
    }

    // Listeleme → sadece okuyoruz, güncelleme yok → AsNoTracking
    public async Task<List<KitapListeDto>> HepsiniGetirAsync()
    {
        return await _context.Kitaplar
            .AsNoTracking()
            .Where(k => k.Aktif)
            .OrderBy(k => k.Ad)
            .Select(k => new KitapListeDto
            {
                Id = k.Id,
                Ad = k.Ad,
                Fiyat = k.Fiyat
            })
            .ToListAsync();
    }

    // Detay → tracking açık (ileride güncellenebilir)
    public async Task<Kitap?> DetayGetirAsync(int id)
    {
        return await _context.Kitaplar.FindAsync(id);
    }

    // Ekleme → context'e ekliyoruz, SaveChanges yazıyor
    public async Task EkleAsync(Kitap kitap)
    {
        _context.Kitaplar.Add(kitap);       // State: Added
        await _context.SaveChangesAsync();  // INSERT
    }

    // Güncelleme → önce yükle (tracking açılır), sonra değiştir, sonra kaydet
    public async Task GuncelleAsync(int id, KitapGuncelleDto dto)
    {
        var kitap = await _context.Kitaplar.FindAsync(id); // State: Unchanged
        if (kitap is null) throw new KeyNotFoundException();

        kitap.Ad = dto.Ad;
        kitap.Fiyat = dto.Fiyat;
        // Change Tracker: "Fiyat değişti" → State: Modified

        await _context.SaveChangesAsync(); // UPDATE (sadece değişen kolonlar)
    }

    // Silme
    public async Task SilAsync(int id)
    {
        var kitap = await _context.Kitaplar.FindAsync(id);
        if (kitap is null) throw new KeyNotFoundException();

        _context.Kitaplar.Remove(kitap);   // State: Deleted
        await _context.SaveChangesAsync(); // DELETE
    }
}
```

---

## Sonraki Gün

Gün 30'da `IQueryable<T>` ve LINQ-to-SQL çevirisi incelenecek: sorgular ne zaman veritabanında, ne zaman bellekte çalışır; `Include()` ile eager loading; projection ile gereksiz kolonlardan kaçınma.
