# Gün 32 — Migration Stratejileri ve Schema Management

---

## 1. Code-First Migration Sistemi

EF Core'da migration, C# entity sınıflarının veritabanı şemasına dönüştürülmesi sürecidir. Entity değişince migration oluşturursun; EF Core farkı hesaplar ve SQL üretir.

```
Akış:

Entity sınıfını değiştirdin (ör. Kitap'a Isbn kolonu ekledin)
          ↓
dotnet ef migrations add IsbnEklendi
          ↓
EF Core mevcut model ile DB snapshot'ını karşılaştırır
          ↓
20240410_IsbnEklendi.cs oluşturulur (Up + Down metodları)
          ↓
dotnet ef database update
          ↓
SQL çalıştırılır: ALTER TABLE Kitaplar ADD Isbn nvarchar(20)
          ↓
__EFMigrationsHistory tablosuna kayıt eklenir
```

### Migration Dosyasının Yapısı

```csharp
// Migrations/20240410123456_IsbnEklendi.cs
// Bu dosya otomatik üretilir — elle düzenleyebilirsin ama dikkatli ol.

public partial class IsbnEklendi : Migration
{
    // Up: ileri yön — bu migration uygulandığında çalışır
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(    // ALTER TABLE ... ADD Isbn
            name: "Isbn",                      // kolon adı
            table: "Kitaplar",                 // hangi tablo
            type: "nvarchar(20)",              // SQL tipi — HasMaxLength(20) buraya dönüşür
            nullable: true);                   // nullable: true → mevcut satırlar NULL alır
                                               // nullable: false yazsaydık ve mevcut satır varsa
                                               // migration başarısız olurdu (NOT NULL constraint ihlali)
    }

    // Down: geri alma — dotnet ef database update [önceki migration] çalışınca
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(           // ALTER TABLE ... DROP COLUMN Isbn
            name: "Isbn",
            table: "Kitaplar");
        // Down metodunu boş bırakırsaydık "rollback" imkansız olurdu
        // EF Core Down'ı otomatik üretir ama karmaşık senaryolarda kontrol et
    }
}
```

### Temel CLI Komutları

```bash
# Migration oluştur (entity değişikliğini DB'ye yansıtmak için)
dotnet ef migrations add IsbnEklendi
# bunu çalıştırmadan database update yaparsaydın son entity değişikliği DB'ye gitmezdi

# Migration'ı DB'ye uygula
dotnet ef database update
# bunu çalıştırmadan uygulama ayağa kalkardı ama Isbn kolonu olmadığı için
# Isbn içeren sorgular runtime'da patlardı

# Belirli bir migration'a git (ileri veya geri)
dotnet ef database update IsbnEklendi
# daha önceki bir migration adı verirsen Down metodları çalışır (rollback)

# Son migration'ı geri al (henüz DB'ye uygulanmadıysa dosyayı sil)
dotnet ef migrations remove
# DB'ye uygulandıktan sonra remove çalışmaz, önce database update [önceki] yapman gerekir

# Mevcut migration durumunu gör
dotnet ef migrations list
# __EFMigrationsHistory tablosuyla karşılaştırır, hangisi uygulanmış hangisi bekliyor

# Migration'ın üreteceği SQL'i önizle (çalıştırmadan)
dotnet ef migrations script
# Production'da çalıştırmadan önce DBA'e göstermek için kullanılır
```

---

## 2. Migration Idempotency — Tekrar Uygulanabilirlik

Aynı migration iki kez çalıştırıldığında hata vermemesi durumudur. EF Core bunu `__EFMigrationsHistory` tablosuyla sağlar.

```
__EFMigrationsHistory tablosu:
  MigrationId                          | ProductVersion
  -------------------------------------|---------------
  20240101_InitialCreate               | 9.0.0
  20240410_IsbnEklendi                 | 9.0.0

dotnet ef database update çalıştı:
  EF Core: "IsbnEklendi zaten uygulanmış, atlıyorum."
  → Hata yok, iki kez ALTER TABLE çalışmıyor.
```

```csharp
// Idempotency'yi elle yazılan SQL'de nasıl sağlarsın?
// migrationBuilder.Sql() ile özel SQL yazarken dikkatli ol:

protected override void Up(MigrationBuilder migrationBuilder)
{
    // YANLIŞ — iki kez çalışırsa hata verir:
    migrationBuilder.Sql("INSERT INTO Kategoriler (Ad) VALUES ('Roman')");
    // bunu iki kez çalıştırırsan duplicate kayıt oluşur veya unique constraint patlar

    // DOĞRU — idempotent: yoksa ekle
    migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM Kategoriler WHERE Ad = 'Roman')
            INSERT INTO Kategoriler (Ad) VALUES ('Roman')");
    // bu şekilde kaç kez çalıştırırsan çalıştır, sonuç aynı
}
```

---

## 3. Production'da Migration — Database.Migrate() Riski

Geliştirme ortamında `Database.Migrate()` pratiktir: uygulama başlayınca otomatik migration uygular. Production'da bu yaklaşım ciddi riskler taşır.

```csharp
// Program.cs — GELİŞTİRME için kabul edilebilir
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KitabeviDbContext>();
    db.Database.Migrate();   // uygulama başlayınca tüm bekleyen migration'ları uygula
                             // bunu production'da yaparsaydın aşağıdaki riskler ortaya çıkar
}
```

### Production'da Neden Tehlikeli?

```
Risk 1: Çok instance aynı anda başlıyor (Kubernetes, load balancer)
  Instance A → Migrate() başladı
  Instance B → Migrate() başladı (aynı migration'ı uygulamaya çalışıyor)
  → Race condition → deadlock veya hata

Risk 2: Migration uzun sürüyorsa uygulama health check'i geçemez
  Büyük tabloya ALTER TABLE → 10 dakika sürebilir
  Load balancer: "uygulama yanıt vermiyor" → traffic kesilir

Risk 3: Migration başarısız olursa uygulama hiç başlamaz
  Yarım kalan migration → tablo tutarsız durumda
  Her restart tekrar dener → her seferinde başarısız

Risk 4: Rollback imkansız
  Migrate() çalıştı, migration başarısız, DB yarım kaldı
  Down() otomatik çalışmaz
```

### Production'da Doğru Yaklaşımlar

```bash
# Yaklaşım 1: Deployment öncesi CLI ile uygula
# CI/CD pipeline'ında (GitHub Actions, Azure Pipelines):
dotnet ef database update --connection "Server=prod-db;..."
# bunu uygulama başlamadan önce ayrı bir adım olarak çalıştırırsın
# başarısız olursa deployment durur, uygulama eski haliyle çalışmaya devam eder

# Yaklaşım 2: SQL script üret, DBA uygulasın
dotnet ef migrations script --idempotent --output migration.sql
# --idempotent: IF NOT EXISTS kontrolleriyle script üretir
# DBA script'i inceler, onaylar, production'da manuel çalıştırır
# büyük şirketlerde standart budur — DBA onayı olmadan prod DB değişmez
```

```csharp
// Yaklaşım 3: Startup'ta sadece KONTROL et, uygulama
// migration bekleniyorsa başlatma — migration'ı sen uygula

// Program.cs
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KitabeviDbContext>();

    var bekleyenler = db.Database.GetPendingMigrations().ToList();
    // bunu yazmadan Migrate() çağırırsaydın ne beklediğini bile bilmezdik

    if (bekleyenler.Any())
    {
        // Production'da burada loglayıp uygulamayı durdurabilirsin:
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(
            "Uygulanmamış {Adet} migration var: {Listesi}. " +
            "Uygulamayı başlatmadan önce migration'ları uygulayın.",
            bekleyenler.Count,
            string.Join(", ", bekleyenler));

        // Geliştirmede: otomatik uygula
        if (app.Environment.IsDevelopment())
            db.Database.Migrate();
        else
            throw new InvalidOperationException("Production'da migration bekleniyor!");
            // bunu fırlatmasaydık uygulama tutarsız DB şemasıyla çalışmaya başlardı
    }
}
```

---

## 4. Migration Bundle — Executable Migration

Migration bundle, migration'ları bağımsız çalıştırılabilir bir dosyaya paketler. CI/CD pipeline'ında `dotnet ef` veya SDK gerekmez.

```bash
# Bundle oluştur
dotnet ef migrations bundle --output migrate-bundle
# migrate-bundle: tek başına çalışan executable
# içinde: tüm migration'lar + EF Core runtime gömülü
# dotnet SDK gerekmez — Docker image'ına sadece bu dosyayı kopyalamak yeter

# Bundle'ı çalıştır (production sunucusunda)
./migrate-bundle --connection "Server=prod-db;Database=Kitabevi;..."
# bunu Migrate() yerine kullanırsaydık:
# → Race condition yok (tek process çalışıyor)
# → Uygulama henüz başlamadı → health check sorunu yok
# → Başarısız olursa deployment durur, uygulama eski haliyle kalır

# Self-contained bundle (hedef makinede .NET runtime bile gerekmez)
dotnet ef migrations bundle --self-contained --runtime linux-x64
```

### CI/CD Pipeline'da Kullanım

```yaml
# .github/workflows/deploy.yml — örnek GitHub Actions adımları
# (gerçek syntax'ı göstermek için, çalışan workflow değil)

# Adım 1: Bundle oluştur
- name: Migration bundle oluştur
  run: dotnet ef migrations bundle --output ./migrate-bundle

# Adım 2: Bundle'ı production'a gönder ve çalıştır
- name: Migration uygula
  run: ./migrate-bundle --connection ${{ secrets.PROD_DB }}
  # bu adım başarısız olursa sonraki adım (uygulama deploy) çalışmaz

# Adım 3: Ancak migration başarılıysa uygulamayı deploy et
- name: Uygulamayı deploy et
  run: kubectl apply -f deployment.yaml
  # migration olmadan deploy yapmak: yeni kod, eski şema → runtime hatası
```

---

## 5. Flyway/Liquibase ile Karşılaştırma

EF Core migration, code-first dünyada güçlüdür. Flyway ve Liquibase ise SQL-first araçlardır; dil bağımsız çalışır.

| | EF Core Migration | Flyway | Liquibase |
|---|---|---|---|
| Dil | C# (code-first) | SQL / Java | SQL / XML / YAML |
| Platform | .NET | JVM veya CLI | JVM veya CLI |
| Rollback | Down() metodu (manuel) | Undo scripts (pro) | Rollback tag |
| Versiyon takibi | `__EFMigrationsHistory` | `flyway_schema_history` | `DATABASECHANGELOG` |
| Idempotency | Otomatik | Checksum ile | Checksum ile |
| Multi-dil takım | Zor (.NET gerekir) | Kolay | Kolay |

```
Ne zaman EF Core migration?
  → Takım tamamen .NET kullanıyor
  → Entity'ler C#'ta tanımlı, DB şeması buradan türüyor
  → Hızlı prototipleme, startup ortamı

Ne zaman Flyway/Liquibase?
  → Birden fazla uygulama aynı DB'yi paylaşıyor (Java + .NET + Python)
  → DBA'in SQL kontrolü şart
  → Migration'lar code review'dan bağımsız onay sürecinden geçmeli
  → EF Core'dan bağımsız kalmak istiyorsun (ORM değişebilir, migration kalmalı)
```

```sql
-- Flyway örneği: V2__isbn_ekle.sql
-- Flyway bu dosyayı bulur, checksum hesaplar, bir kez çalıştırır.
-- .NET SDK gerekmez, Java bile gerekmez (CLI sürümü var)

ALTER TABLE Kitaplar ADD Isbn NVARCHAR(20) NULL;
-- EF Core'da bunu C# migration olarak yazardın;
-- Flyway'de doğrudan SQL yazıyorsun — DBA'e göstermek çok daha kolay
```

---

## 6. Seed Data — HasData() vs Özel Seed Servisi

Başlangıç verisi (seed) eklemenin iki yolu vardır.

### HasData() — Migration Tabanlı Seed

```csharp
// KitabeviDbContext.cs — OnModelCreating içinde

modelBuilder.Entity<Kitap>().HasData(
    new Kitap
    {
        Id       = 1,           // HasData'da Id ZORUNLU — migration bu değeri kullanır
                                // Id yazmasaydık EF Core exception fırlatırdı
        Baslik   = "Clean Code",
        Yazar    = "Robert Martin",
        Fiyat    = 45m,
        Kategori = "Yazılım",
        StokAdedi = 10,
        EklemeTarihi = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        // DateTime.UtcNow yazmaman gerekir — her migration oluşturduğunda
        // değer değişir, EF Core "veri değişti" sanır ve tekrar INSERT üretir
    }
);
// Bu seed migration'a dönüşür:
// INSERT INTO Kitaplar (Id, Baslik, ...) VALUES (1, 'Clean Code', ...)
// Ama: InMemory provider'da HasData çalışmaz → Program.cs'te manuel seed şart
```

### HasData'nın Sorunları

```
Sorun 1: Her değişiklik yeni migration üretir
  Kitabın fiyatını 45'ten 50'ye değiştirdin → yeni migration gerekiyor
  → Prod'da deployment + migration sadece fiyat için

Sorun 2: Referential integrity sırası
  Yazarlar tablosuna Yazar seed edip Kitaplar'da YazarId kullanmak istiyorsan
  Yazar'ın seed'i Kitap seed'inden önce migration'a girmeli
  → Büyüdükçe yönetilemez

Sorun 3: InMemory provider desteklemiyor
  Testlerde, geliştirmede HasData çalışmıyor
  → Program.cs'te ayrıca manuel seed yazmak zorundasın

Ne zaman HasData kullanılır?
  → Az sayıda, nadiren değişen referans veriler (ülke listesi, para birimi kodu vb.)
  → Değiştirilmesi deployment gerektirse de sorun olmayan veriler
```

### Özel Seed Servisi — Esnek ve Kontrollü

```csharp
// Services/DbSeeder.cs

public class DbSeeder
{
    private readonly KitabeviDbContext _context;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(KitabeviDbContext context, ILogger<DbSeeder> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task SeedAsync()
    {
        // ─────────────────────────────────────────────────────────────
        // Önce Yazarlar — Kitaplar YazarId'ye bağlı
        // bunu atlarsaydık Kitap seed'i FK constraint'i ihlal ederdi
        // ─────────────────────────────────────────────────────────────
        if (!await _context.Yazarlar.AnyAsync())   // zaten veri varsa tekrar ekleme
        {                                           // AnyAsync yazmasaydık her uygulama
                                                    // başlangıcında duplicate kayıt girerdi
            var yazarlar = new List<Yazar>
            {
                new() { Id = 1, Ad = "Robert",  Soyad = "Martin" },
                new() { Id = 2, Ad = "Eric",    Soyad = "Evans"  },
                new() { Id = 3, Ad = "Andrew",  Soyad = "Hunt"   }
            };

            await _context.Yazarlar.AddRangeAsync(yazarlar);
            await _context.SaveChangesAsync();      // Yazarlar önce kaydedildi
                                                    // bunu yazmadan Kitaplar'ı eklersek
                                                    // YazarId FK'ı çözümsüz kalır → hata
            _logger.LogInformation("Seed: {Adet} yazar eklendi.", yazarlar.Count);
        }

        // ─────────────────────────────────────────────────────────────
        // Sonra Kitaplar
        // ─────────────────────────────────────────────────────────────
        if (!await _context.Kitaplar.AnyAsync())
        {
            var kitaplar = new List<Kitap>
            {
                new() { Id = 1, Baslik = "Clean Code",  YazarId = 1, Fiyat = 45m,  Kategori = "Yazılım", StokAdedi = 10, EklemeTarihi = DateTime.UtcNow },
                new() { Id = 2, Baslik = "DDD",         YazarId = 2, Fiyat = 85m,  Kategori = "Mimari",  StokAdedi = 5,  EklemeTarihi = DateTime.UtcNow },
                new() { Id = 3, Baslik = "Pragmatic",   YazarId = 3, Fiyat = 55m,  Kategori = "Yazılım", StokAdedi = 8,  EklemeTarihi = DateTime.UtcNow }
            };
            // DateTime.UtcNow burada sorun değil — HasData'dan farklı olarak
            // migration'a dönüşmüyor, kod her çalıştığında anlık zamanı alıyor

            await _context.Kitaplar.AddRangeAsync(kitaplar);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seed: {Adet} kitap eklendi.", kitaplar.Count);
        }
    }
}
```

```csharp
// Program.cs — seed servisi çağrısı

builder.Services.AddScoped<DbSeeder>();
// Scoped: DbContext Scoped olduğu için DbSeeder de Scoped olmalı
// Singleton yaparsaydık "cannot consume scoped service from singleton" hatası alırdık

// ...

using (var scope = app.Services.CreateScope())
{
    // Migration önce uygulanmalı, sonra seed
    var db = scope.ServiceProvider.GetRequiredService<KitabeviDbContext>();
    db.Database.Migrate();               // tablo yapısı hazır olsun
                                         // bunu yazmadan seed yaparsaydık tablo yoksa hata alırdık

    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();            // tablolar dolu değilse doldur
}
```

### HasData vs Özel Seed Servisi Karşılaştırması

| | `HasData()` | Özel Seed Servisi |
|---|---|---|
| Nerede çalışır | Migration zamanı | Uygulama başlangıcı |
| InMemory desteği | Hayır | Evet |
| Veri değişince | Yeni migration gerekir | Sadece kod değişir |
| Sıra kontrolü | Zor | Tam kontrol |
| Ortama göre farklı seed | Zor | Kolay (`IsDevelopment()` kontrolü) |
| Ne zaman tercih et | Referans veriler (ülke, para birimi) | Dinamik, ortama özel başlangıç verileri |

---

## 7. Kitabevi Uygulamasına Uygulama

Şimdiye kadar `UseInMemoryDatabase` kullandık. Aşağıda SQL Server'a geçiş ve migration akışı gösterilmiştir — kodu değiştirmeden şemayı nasıl taşıyacağını görmek için.

```csharp
// Program.cs — InMemory → SQL Server geçişi (tek satır değişiyor)

// ESKİ (Gün 29):
builder.Services.AddDbContext<KitabeviDbContext>(options =>
    options.UseInMemoryDatabase("KitabeviDb"));
// InMemory: tablolar bellekte, uygulama kapanınca yok

// YENİ:
builder.Services.AddDbContext<KitabeviDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default")));
        // bağlantı dizesi appsettings.json'dan okunuyor
        // bunu hard-code yazarsaydık her ortam için farklı binary gerekecekti
```

```json
// appsettings.json — bağlantı dizesi
{
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\mssqllocaldb;Database=KitabeviDb;Trusted_Connection=True"
  }
}

// appsettings.Development.json — geliştirme ortamı bağlantısı
// Bu dosya appsettings.json'ı ezer → aynı anahtar, farklı değer
{
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\mssqllocaldb;Database=KitabeviDb_Dev;Trusted_Connection=True"
  }
}
// Development'a özel DB adı (KitabeviDb_Dev) → production verisi bozulmaz
```

```bash
# SQL Server'a geçince yapılacaklar:

# 1. İlk migration oluştur (entity'ler zaten var)
dotnet ef migrations add InitialCreate
# EF Core mevcut entity'leri tarar, tablo+index+FK SQL'lerini üretir

# 2. DB'yi oluştur ve migration'ı uygula
dotnet ef database update
# LocalDB yoksa oluşturur, Kitaplar ve Yazarlar tablolarını açar
# __EFMigrationsHistory tablosunu da oluşturur

# 3. Gelecekte entity değişince:
#    Entity'ye yeni property ekle → migration oluştur → uygula
dotnet ef migrations add KitapIsbnEklendi
dotnet ef database update
```

---

## 8. Özet

```
Code-First Migration
  Entity değişir → dotnet ef migrations add → Up/Down metodları
  dotnet ef database update → SQL çalışır → __EFMigrationsHistory'e kayıt

Idempotency
  __EFMigrationsHistory: uygulanmış migration tekrar çalışmaz
  Elle yazılan SQL'de IF NOT EXISTS kontrolü şart

Production'da Migration
  Database.Migrate() → race condition, health check riski → production'da kullanma
  Doğru yol: CI/CD'de ayrı adım olarak bundle veya script

Migration Bundle
  dotnet ef migrations bundle → bağımsız executable
  dotnet SDK gerekmez → Docker/Kubernetes dostu
  --idempotent → güvenli tekrar çalıştırma

Flyway / Liquibase
  SQL-first, dil bağımsız → multi-dil takımlarda veya DBA onayı gerektiğinde
  EF Core migration → .NET-only takımlarda, code-first yaklaşımda

Seed Data
  HasData()       → referans veriler, migration'a gömülür, InMemory'de çalışmaz
  Özel DbSeeder   → dinamik, ortama özel, InMemory destekli, sıra kontrolü tam
```

---

## Sonraki Gün

Gün 33'te EF Core Performance Tuning: `AsNoTracking()` gerçek kazancı, `ExecuteUpdate/Delete` (EF Core 7+), connection pooling, `TagWith()` ile sorgu etiketleme ve index stratejileri ele alınacak.
