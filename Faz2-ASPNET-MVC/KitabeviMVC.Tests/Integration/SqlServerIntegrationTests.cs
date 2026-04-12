using KitabeviMVC.Data;
using KitabeviMVC.Models.Entities;
using KitabeviMVC.Tests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Tests.Integration;

// ─────────────────────────────────────────────────────────────────────────────
// SQL Server Integration Testleri — TestContainers ile
//
// Gün 42: InMemory'nin yetersiz kaldığı senaryolar:
//   1. UNIQUE constraint ihlali → SqlException
//   2. RowVersion concurrency → DbUpdateConcurrencyException
//   3. Migration geçerliliği → GetPendingMigrationsAsync
//
// [Collection("SqlServer")]: SqlServerFixture'ı tüm testlerle paylaşır.
//   Container bir kez başlar, tüm testler aynı SQL Server'ı kullanır.
//   Test izolasyonu: her test yeni DB adı (Guid) ile çalışır.
//
// IAsyncLifetime: her test için ayrı DB kurulumu ve yıkımı.
// ─────────────────────────────────────────────────────────────────────────────
[Collection("SqlServer")]
// [Collection("SqlServer")]: SqlServerCollection'da tanımlı fixture'ı inject et.
// Bu attribute olmadan: SqlServerFixture constructor'a inject edilemez.
public class SqlServerIntegrationTests : IAsyncLifetime
{
    private readonly SqlServerFixture _sqlFixture;
    private KitabeviDbContext _db = null!;
    // null!: null-forgiving operator — InitializeAsync'de atanacak.
    // InitializeAsync öncesi _db kullanılırsa NullReferenceException — kasıtlı kısıtlama.

    public SqlServerIntegrationTests(SqlServerFixture sqlFixture)
    {
        _sqlFixture = sqlFixture;
        // SqlServerFixture: container hazır, ConnectionString alınabilir.
        // DI: xUnit ICollectionFixture fixture'ı constructor'a inject eder.
    }

    public async Task InitializeAsync()
    {
        // Her test için benzersiz DB adı — izolasyon garantisi.
        var dbAdi = $"kitabevi_test_{Guid.NewGuid():N}";
        // Format N: kısa GUID (kısa çizgisiz, 32 karakter) — SQL DB adı için uygun.
        // Guid.NewGuid(): her InitializeAsync'de farklı → testler aynı DB'yi görmez.

        // Connection string'deki "master" DB adını yeni test DB adıyla değiştir.
        var baglanti = _sqlFixture.ConnectionString
            .Replace("master", dbAdi, StringComparison.OrdinalIgnoreCase);
        // ConnectionString format: "...;Database=master;..."
        // Replace: "master" → benzersiz DB adı. Basit string replace yeterli.
        // SqlConnectionStringBuilder kullanmak: daha güvenli ama verbose.

        var options = new DbContextOptionsBuilder<KitabeviDbContext>()
            .UseSqlServer(baglanti)
            // UseSqlServer: gerçek SQL Server provider — InMemory değil.
            // Bunu UseInMemoryDatabase yapmak: TestContainers'ı anlamsız kılar.
            .Options;

        _db = new KitabeviDbContext(options);

        await _db.Database.EnsureCreatedAsync();
        // EnsureCreated: schema yaratır — migration history olmadan.
        // MigrateAsync: tüm migration'ları sırayla uygular, history tutar.
        // Bu testlerde EnsureCreated tercih: migration testleri ayrı — hız öncelikli.
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        // EnsureDeletedAsync: test DB'sini tamamen siler.
        // Container silinmiyor — sadece bu testin DB'si gidiyor.
        // Bunu yapmamak: her test koşumunda DB birikir, disk dolar.

        await _db.DisposeAsync();
        // DbContext dispose: bağlantıyı connection pool'a geri verir.
    }

    // ─── Test 1: UNIQUE Constraint — InMemory'de olmayan davranış ────────────

    [Fact]
    public async Task Ekle_MukerrerBaslik_UniqueConstraintFirlatir()
    {
        // ─── Arrange ──────────────────────────────────────────────────────────
        var ilkKitap = new Kitap
        {
            Baslik    = "Clean Code",
            Yazar     = "Robert Martin",
            Fiyat     = 89.90m,
            Kategori  = "Yazılım",
            StokAdedi = 10
        };
        await _db.Kitaplar.AddAsync(ilkKitap);
        await _db.SaveChangesAsync();
        // İlk kayıt başarılı.

        var mukerrerKitap = new Kitap
        {
            Baslik    = "Clean Code",   // aynı başlık — UNIQUE index ihlali
            Yazar     = "Başka Yazar",
            Fiyat     = 50m,
            Kategori  = "Yazılım",
            StokAdedi = 5
        };
        await _db.Kitaplar.AddAsync(mukerrerKitap);

        // ─── Act & Assert ─────────────────────────────────────────────────────
        Func<Task> kaydet = () => _db.SaveChangesAsync();

        // DbUpdateException: EF Core'un üst seviye exception'ı — her DB hatası için.
        // await + parantez: ThrowAsync Task döndürür → önce ExceptionAssertions al, sonra zincirle.
        (await kaydet.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<SqlException>();
        // SqlException: SQL Server'ın UNIQUE constraint ihlali hatası.
        // InMemory'de: bu exception OLUŞMAZ — gerçek SQL Server gerektiği ispat edildi.
        // Bunu yazmassaydık: aynı başlıkta kitap eklenebilirdi → data integrity bozulurdu.
    }

    // ─── Test 2: RowVersion Concurrency — InMemory'de çalışmaz ──────────────

    [Fact]
    public async Task Guncelle_EsZamanliGuncelleme_ConcurrencyExceptionFirlatir()
    {
        // ─── Arrange ──────────────────────────────────────────────────────────
        // İki kullanıcı aynı kitabı aynı anda düzenliyor senaryosu.

        var kitap = new Kitap
        {
            Baslik    = "Eş Zamanlı Test",
            Yazar     = "Yazar",
            Fiyat     = 100m,
            Kategori  = "Test",
            StokAdedi = 10
        };
        await _db.Kitaplar.AddAsync(kitap);
        await _db.SaveChangesAsync();
        // Kitap eklendi. RowVersion SQL Server tarafından atandı (8 byte).

        // İki ayrı DbContext — iki ayrı kullanıcı/HTTP isteği simülasyonu.
        var baglanti = _sqlFixture.ConnectionString
            .Replace("master", $"kitabevi_test_concurrency_{kitap.Id}", StringComparison.OrdinalIgnoreCase);
        // Aynı DB'ye farklı context: bağlantı aynı ama identity map farklı.
        // Gerçekte: iki farklı HTTP isteği → iki farklı DbContext scope.

        var options = new DbContextOptionsBuilder<KitabeviDbContext>()
            .UseSqlServer(_db.Database.GetConnectionString()!)
            .Options;

        await using var db1 = new KitabeviDbContext(options);
        await using var db2 = new KitabeviDbContext(options);
        // İki ayrı context: her biri kendi Change Tracker ve identity cache'ine sahip.

        var kitap1 = await db1.Kitaplar.FindAsync(kitap.Id);
        var kitap2 = await db2.Kitaplar.FindAsync(kitap.Id);
        // Her ikisi de aynı RowVersion ile okudu.

        // ─── Act ──────────────────────────────────────────────────────────────
        kitap1!.StokAdedi = 8;   // Kullanıcı 1: 2 adet sattı
        await db1.SaveChangesAsync();
        // db1 başarılı: RowVersion DB tarafından yeni değere güncellendi.

        kitap2!.StokAdedi = 7;   // Kullanıcı 2: 3 adet sattı (eski RowVersion ile)
        Func<Task> db2Kaydet = () => db2.SaveChangesAsync();

        // ─── Assert ───────────────────────────────────────────────────────────
        await db2Kaydet.Should().ThrowAsync<DbUpdateConcurrencyException>(
            because: "db1 RowVersion'ı güncelledi; db2'nin eski RowVersion'ı artık eşleşmiyor.");
        // DbUpdateConcurrencyException: optimistic concurrency ihlali.
        // WHERE Id = @id AND RowVersion = @eskiRowVersion → 0 satır etkilendi → exception.
        // InMemory provider: RowVersion'ı desteklemez — bu exception OLUŞMAZ.
        // Bunu yazmassaydık: son yazılan kazanır (lost update) — stok yanlış hesaplanır.
    }

    // ─── Test 3: Stoklu Kitap Sorgusu — Gerçek SQL Planı ─────────────────────

    [Fact]
    public async Task GetStokluKitaplar_MixedData_SadecePozitifStokDoner()
    {
        // ─── Arrange ──────────────────────────────────────────────────────────
        await _db.Kitaplar.AddRangeAsync(
            new Kitap { Baslik = "Stoklu 1",  Yazar = "Y", Fiyat = 50m, Kategori = "Roman", StokAdedi = 10 },
            new Kitap { Baslik = "Stoklu 2",  Yazar = "Y", Fiyat = 60m, Kategori = "Roman", StokAdedi = 5  },
            new Kitap { Baslik = "Stoksuz 1", Yazar = "Y", Fiyat = 70m, Kategori = "Roman", StokAdedi = 0  },
            new Kitap { Baslik = "Stoksuz 2", Yazar = "Y", Fiyat = 80m, Kategori = "Roman", StokAdedi = -1 }
            // Negatif stok: InMemory'de geçer, ama UNIQUE constraint olmadığı için bu test daha çok sorgu testi.
        );
        await _db.SaveChangesAsync();

        // ─── Act ──────────────────────────────────────────────────────────────
        var stokluKitaplar = await _db.Kitaplar
            .AsNoTracking()
            .Where(k => k.StokAdedi > 0)
            // Gerçek SQL: WHERE StokAdedi > 0 — SQL Server bu sorguyu IX_Kitaplar_Kategori_Stok ile optimize edebilir.
            .OrderBy(k => k.Baslik)
            .ToListAsync();

        // ─── Assert ───────────────────────────────────────────────────────────
        stokluKitaplar.Should().HaveCount(2);
        stokluKitaplar.Should().OnlyContain(k => k.StokAdedi > 0,
            because: "Stoklu kitap sorgusu yalnızca pozitif stoklu kitapları döndürmeli.");
        stokluKitaplar.Should().BeInAscendingOrder(k => k.Baslik,
            because: "Handler OrderBy(Baslik) uyguluyor.");
    }

    // ─── Test 4: Migration Kontrolü ───────────────────────────────────────────

    [Fact]
    public async Task Database_EnsureCreated_TablolarOlusur()
    {
        // EnsureCreated sonrası Kitaplar tablosu var mı?
        // Migration değil, schema kontrolü: tablo oluşturuldu mu?

        var kitapSayisi = await _db.Kitaplar.CountAsync();
        // CountAsync: SELECT COUNT(*) FROM Kitaplar
        // Tablo yoksa: SqlException (Invalid object name 'Kitaplar')
        // Tablo varsa: 0 (yeni DB, boş)

        kitapSayisi.Should().Be(0,
            because: "Yeni oluşturulan DB'de henüz kayıt yok.");
        // Bu test tablonun var olduğunu dolaylı doğrular:
        // Exception fırlatılmadıysa → tablo var → EnsureCreated çalıştı.
    }

    // ─── Test 5: Cascade Davranışı — FK ile Yazar Silme ──────────────────────

    [Fact]
    public async Task YazarSil_BagliKitaplar_YazarIdNullOlur()
    {
        // ─── Arrange ──────────────────────────────────────────────────────────
        // Yazar ekle
        var yazar = new Yazar
        {
            Ad      = "Robert",
            Soyad   = "Martin",
            Biyografi = "Uncle Bob"
        };
        await _db.Yazarlar.AddAsync(yazar);
        await _db.SaveChangesAsync();

        // Yazara bağlı kitap ekle
        var kitap = new Kitap
        {
            Baslik    = "Clean Code",
            Yazar     = "Robert Martin",
            Fiyat     = 89.90m,
            Kategori  = "Yazılım",
            StokAdedi = 10,
            YazarId   = yazar.Id
            // YazarId: FK — Yazarlar tablosuna referans.
        };
        await _db.Kitaplar.AddAsync(kitap);
        await _db.SaveChangesAsync();

        // ─── Act ──────────────────────────────────────────────────────────────
        _db.Yazarlar.Remove(yazar);
        await _db.SaveChangesAsync();
        // Yazar silindi. DbContext konfigürasyonu: OnDelete(DeleteBehavior.SetNull)
        // → Kitap.YazarId = null (cascade delete değil, set null).

        // ─── Assert ───────────────────────────────────────────────────────────
        var kitapSonraki = await _db.Kitaplar
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == kitap.Id);

        kitapSonraki.Should().NotBeNull();
        kitapSonraki!.YazarId.Should().BeNull(
            because: "OnDelete(DeleteBehavior.SetNull) konfigürasyonu: Yazar silindiğinde Kitap.YazarId null'a çekilmeli.");
        // Bu davranış InMemory'de tam çalışmaz — gerçek SQL FK cascade işlemi gerekir.
    }
}
