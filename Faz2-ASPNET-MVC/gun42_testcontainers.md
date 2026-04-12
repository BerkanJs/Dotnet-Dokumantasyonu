# Gün 42 — TestContainers: Gerçek DB ile Test

## InMemory Database Neden Yetmez?

`UseInMemoryDatabase` hızlı ve kullanışlıdır ama gerçek SQL Server'ın bazı davranışlarını
**simüle edemez**:

| Özellik | InMemory | Gerçek SQL Server |
|---------|----------|-------------------|
| LINQ to SQL çevirisi | Bellek | Gerçek T-SQL |
| Transaction semantics | Sınırlı | ACID |
| Concurrency (RowVersion) | Çalışmaz | Çalışır |
| Migration | Gereksiz | Gerekli |
| Stored Procedure | Yok | Var |
| Unique constraint | Yok | Var |
| JSON column | Yok | SQL Server 2022+ |

**Gerçek senaryo — e-ticaret:**
`InMemory` testleri geçiyor. Ama production'da:
- `UNIQUE` constraint: aynı ISBN'li kitap ikinci kez eklenince `SqlException` — `InMemory`'de bu hata yoktu.
- `RowVersion` concurrency token: iki kullanıcı aynı anda stok düşürdüğünde `DbUpdateConcurrencyException` — `InMemory`'de kayıt yoktu.
- `EnsureSuccessStatusCode` + migration: `EnsureCreated` schema yaratır ama migration history takip etmez.

---

## TestContainers Nedir?

TestContainers, test süresi boyunca **Docker container** başlatır ve test bitince durdurur.
Testler gerçek SQL Server (veya Postgres, MySQL, Redis) karşısında çalışır.

```
dotnet test
    │
    ├── xUnit runner başlar
    │
    ├── IAsyncLifetime.InitializeAsync()
    │     └── Docker: mcr.microsoft.com/mssql/server container başlar
    │           └── SQL Server portu: 1433 (rastgele host port)
    │
    ├── Test çalışır — gerçek SQL Server'a bağlanır
    │
    └── IAsyncLifetime.DisposeAsync()
          └── Docker: container durdurulur ve silinir
```

**Gereksinim:** Docker Desktop çalışıyor olmalı.

---

## NuGet Kurulum

```xml
<!-- KitabeviMVC.Tests.csproj -->
<PackageReference Include="Testcontainers.MsSql" Version="3.9.0" />
<!-- Testcontainers.MsSql: MsSqlContainer sınıfını içerir -->
<!-- Testcontainers: base paket — otomatik bağımlılık olarak gelir -->
```

---

## MsSqlContainer Kurulumu

```csharp
// KitabeviMVC.Tests/Infrastructure/SqlServerFixture.cs

public class SqlServerFixture : IAsyncLifetime
// IAsyncLifetime: xUnit'in async setup/teardown interface'i.
// InitializeAsync: test class'ı oluşturulmadan önce çağrılır.
// DisposeAsync: tüm testler bittikten sonra çağrılır.
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        // WithImage: hangi SQL Server sürümü kullanılacak.
        // 2022-latest: en güncel SQL Server 2022 image.
        // 2019-latest: daha eski ama lighter.
        .WithPassword("TestSifre123!")
        // WithPassword: SA şifresi — SQL Server güçlü şifre zorunlu tutar.
        // Güvenlik: test ortamı dışında kullanılmamalı.
        .Build();
        // Build(): container builder'ı tamamla — henüz başlatmıyor.

    public string ConnectionString => _container.GetConnectionString();
    // GetConnectionString(): "Server=localhost,PORT;Database=master;User=sa;Password=..."
    // PORT: Docker'ın rastgele atadığı host port — sabit 1433 değil.
    // Neden rastgele: birden fazla test aynı anda çalışırsa port çakışması olmaz.

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        // StartAsync: Docker container başlatır — ~5-10 saniye ilk seferinde.
        // Sonraki çalıştırmalar: Docker image cache'te → daha hızlı.
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        // DisposeAsync: container durdurur ve siler.
        // Test izolasyonu: her test koşumunda temiz başlangıç.
    }
}
```

---

## CollectionFixture — Container Paylaşımı

Container başlatma maliyetli (~10 saniye). Her test sınıfı için başlatmak çok yavaş.
`[Collection]` ile tüm testler aynı container'ı paylaşır:

```csharp
// KitabeviMVC.Tests/Infrastructure/SqlServerCollection.cs

[CollectionDefinition("SqlServer")]
// CollectionDefinition: bu collection'ı "SqlServer" adıyla tanımla.
// Aynı adı kullanan tüm test class'ları bu fixture'ı paylaşır.
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
// ICollectionFixture<T>: fixture tüm collection ömrünce yaşar (IClassFixture'dan farklı).
// IClassFixture: bir test sınıfı ömrünce — her sınıf için ayrı container.
// ICollectionFixture: tüm "SqlServer" collection'ı ömrünce — tek container.
{
    // Body boş — sadece tanım.
    // xUnit bu sınıfı otomatik bulur ve fixture'ı manage eder.
}
```

---

## Gerçek SQL Server ile Integration Test

```csharp
// KitabeviMVC.Tests/Integration/SqlServerIntegrationTests.cs

[Collection("SqlServer")]
// [Collection("SqlServer")]: bu test sınıfı SqlServerCollection'a dahil.
// SqlServerFixture otomatik inject edilir.
public class SqlServerIntegrationTests : IAsyncLifetime
{
    private readonly SqlServerFixture _sqlFixture;
    private KitabeviDbContext _db = null!;

    public SqlServerIntegrationTests(SqlServerFixture sqlFixture)
    {
        _sqlFixture = sqlFixture;
        // SqlServerFixture: container hazır, connection string alınabilir.
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<KitabeviDbContext>()
            .UseSqlServer(_sqlFixture.ConnectionString)
            // UseSqlServer: gerçek SQL Server bağlantısı — InMemory değil.
            .Options;

        _db = new KitabeviDbContext(options);

        await _db.Database.MigrateAsync();
        // MigrateAsync: tüm migration'ları uygular.
        // EnsureCreated yerine MigrateAsync: migration history tutulur.
        // Neden önemli: migration'ların sıralı çalışması test edilir.
        // EnsureCreated: schema yaratır ama migration'ları atlar — fark var.
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        // EnsureDeletedAsync: test DB'sini sil — temiz başlangıç için.
        // Dikkat: container paylaşılıyor ama DB'yi siliyoruz.
        // Alternatif: her test farklı DB adı kullanır (Guid ile).

        await _db.DisposeAsync();
    }

    // ─── Test: Unique Constraint ────────────────────────────────────────────────

    [Fact]
    public async Task EkleAsync_MukerrerIsbn_UniqueConstraintFirlatir()
    {
        // ─── Arrange ──────────────────────────────────────────────────────────
        var ilkKitap = new Kitap { Baslik = "Test", Isbn = "9786051234567" };
        await _db.Kitaplar.AddAsync(ilkKitap);
        await _db.SaveChangesAsync();
        // İlk kayıt: başarılı.

        var mukerrerKitap = new Kitap { Baslik = "Başka", Isbn = "9786051234567" };
        // Aynı ISBN — UNIQUE constraint ihlali.
        await _db.Kitaplar.AddAsync(mukerrerKitap);

        // ─── Act & Assert ─────────────────────────────────────────────────────
        Func<Task> kayit = () => _db.SaveChangesAsync();

        await kayit.Should().ThrowAsync<DbUpdateException>()
            .WithInnerException<SqlException>();
        // DbUpdateException: EF Core'un üst seviye exception'ı.
        // SqlException: iç exception — SQL Server'ın UNIQUE constraint hatası.
        // InMemory'de: bu hata oluşmaz — gerçek DB gerekir.
    }

    // ─── Test: Concurrency (RowVersion) ─────────────────────────────────────────

    [Fact]
    public async Task StokGuncelle_EsZamanliGuncelleme_ConcurrencyExceptionFirlatir()
    {
        // ─── Arrange ──────────────────────────────────────────────────────────
        var kitap = new Kitap { Baslik = "Rekabetçi", Fiyat = 100m, StokAdedi = 10 };
        await _db.Kitaplar.AddAsync(kitap);
        await _db.SaveChangesAsync();

        // İki farklı context — iki farklı kullanıcı simülasyonu
        var options = new DbContextOptionsBuilder<KitabeviDbContext>()
            .UseSqlServer(_sqlFixture.ConnectionString).Options;

        await using var db1 = new KitabeviDbContext(options);
        await using var db2 = new KitabeviDbContext(options);
        // İki ayrı DbContext: her biri kendi identity cache'ine sahip.
        // Gerçekte: iki ayrı HTTP isteği → iki ayrı scope.

        var kitap1 = await db1.Kitaplar.FindAsync(kitap.Id);
        var kitap2 = await db2.Kitaplar.FindAsync(kitap.Id);
        // Her ikisi de aynı RowVersion ile okudu.

        // ─── Act ──────────────────────────────────────────────────────────────
        kitap1!.StokAdedi = 8;  // Kullanıcı 1: 2 adet sattı
        await db1.SaveChangesAsync();
        // db1 başarılı: RowVersion güncellendi.

        kitap2!.StokAdedi = 7;  // Kullanıcı 2: 3 adet sattı (eski RowVersion ile)
        Func<Task> db2Kayit = () => db2.SaveChangesAsync();

        // ─── Assert ───────────────────────────────────────────────────────────
        await db2Kayit.Should().ThrowAsync<DbUpdateConcurrencyException>();
        // DbUpdateConcurrencyException: RowVersion uyuşmadı → optimistic concurrency.
        // InMemory'de: bu exception oluşmaz — gerçek SQL gerekir.
        // Bunu yazmassaydık: son yazılan kazanır (lost update) — stok yanlış hesaplanır.
    }

    // ─── Test: Migration Doğrulama ──────────────────────────────────────────────

    [Fact]
    public async Task Database_TumMigrasyonlar_Uygulanabilmeli()
    {
        // Tüm migration'lar uygulandı mı?
        var bekleyenMigrasyonlar = await _db.Database.GetPendingMigrationsAsync();

        bekleyenMigrasyonlar.Should().BeEmpty(
            because: "InitializeAsync'de MigrateAsync çağrıldı — bekleyen migration olmamalı");
        // Bunu yazmassaydık: eksik migration CI'da production'a kadar fark edilmez.
    }
}
```

---

## WebApplicationFactory + TestContainers Entegrasyonu

En güçlü kombinasyon: gerçek HTTP pipeline + gerçek DB:

```csharp
public class SqlServerWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
// Hem WebApplicationFactory hem IAsyncLifetime — container yönetimi dahil.
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("TestSifre123!")
        .Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Gerçek DbContext'i kaldır, container connection string ile değiştir
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<KitabeviDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<KitabeviDbContext>(options =>
                options.UseSqlServer(_container.GetConnectionString()));
            // Gerçek SQL Server — InMemory değil.

            // Schema oluştur
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KitabeviDbContext>();
            db.Database.Migrate();
            // Migrate(): migration'ları uygula — tam production benzeri kurulum.
        });
    }
}
```

---

## InMemory vs TestContainers — Seçim Kılavuzu

```
Hangi test türü?
├── Unit test → Test Double (Moq, Fake) — DB gereksiz
│
├── Integration test
│     ├── Hızlı geri bildirim istiyorum → InMemory
│     │     Uygun değil: unique constraint, concurrency, migration
│     │
│     └── Gerçek DB davranışı gerekiyor → TestContainers
│           Gerekli: UNIQUE index, RowVersion, stored procedure, JSON column
│
└── CI/CD ortamı
      ├── Docker var → TestContainers direkt çalışır
      └── Docker yok → InMemory fallback veya hosted DB servisi
```

**Pratik kural:**
- Yeni proje başlıyorsanız: InMemory ile başlayın, sorun çıktıkça TestContainers ekleyin.
- Kritik iş mantığı (ödeme, stok) testlerinde: TestContainers zorunlu.
- Her commit için çalışacak testlerde: InMemory (hızlı).
- Nightly build / release öncesi: TestContainers (kapsamlı).

---

## Java Spring + Testcontainers Karşılaştırması

| Kavram | .NET TestContainers | Java Spring Testcontainers |
|--------|--------------------|-----------------------------|
| Container sınıfı | `MsSqlContainer` | `MSSQLServerContainer` |
| Başlat | `container.StartAsync()` | `container.start()` |
| Durdur | `container.DisposeAsync()` | `container.stop()` |
| Connection string | `container.GetConnectionString()` | `container.getJdbcUrl()` |
| Paylaşım | `ICollectionFixture` | `@Container` + `@Testcontainers` |
| DB yaratma | `MigrateAsync()` | Flyway/Liquibase veya `@Sql` |
| Annotation | `[Collection("SqlServer")]` | `@ActiveProfiles("testcontainers")` |

---

## Performans İpuçları

```csharp
// 1. Container'ı paylaş — her test için başlatma
[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture> { }

// 2. Her test için yeni DB (izolasyon + hız dengesi)
public async Task InitializeAsync()
{
    var dbAdi = $"kitabevi_test_{Guid.NewGuid():N}";
    // Guid formatı N: kısa (32 karakter, kısa çizgisiz) — SQL DB adı için uygun.
    await _db.Database.EnsureCreatedAsync();
    // EnsureCreated: InMemory gibi hızlı başlangıç — migration olmadan.
    // Test sonunda EnsureDeletedAsync ile sil.
}

// 3. Paralel test çalıştırma — xUnit
// [assembly: CollectionBehavior(DisableTestParallelization = true)]
// Paylaşılan container ile paralel çalışma sorun yaratır.
// CollectionDefinition içinde paralel devre dışı bırakılabilir.
```

---

## Özet

TestContainers'ın değeri:
- **Gerçekçilik:** Gerçek SQL Server → migration, constraint, concurrency testi
- **İzolasyon:** Her test suite kendi container'ını başlatır — birbirini kirletmez
- **Otomatik temizlik:** `DisposeAsync` container'ı siler — artık kalmaz
- **CI uyumlu:** Docker olan herhangi bir CI ortamında çalışır (GitHub Actions, GitLab CI, Azure DevOps)

Maliyeti: test süresi uzar (~10s container başlatma, ilk seferinde image pull).
Bu maliyet kritik testler için kabul edilebilir — yanlış production davranışı 10 saniyeden pahalı.

Bir sonraki adım: **Architecture Testing** ile katman kurallarını otomatik doğrulamak (Gün 43).
