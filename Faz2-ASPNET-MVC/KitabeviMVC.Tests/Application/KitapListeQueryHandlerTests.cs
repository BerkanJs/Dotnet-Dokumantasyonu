using KitabeviMVC.Data;
using KitabeviMVC.Features.Kitaplar;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Tests.Application;

// ─────────────────────────────────────────────────────────────────────────────
// KitapListeQueryHandler Test Sınıfı
//
// Query handler DB'den okur — InMemoryDatabase ile test edilir.
// Her test kendi seed verisini oluşturur: izolasyon garantisi.
// ─────────────────────────────────────────────────────────────────────────────
public class KitapListeQueryHandlerTests
{
    private static KitabeviDbContext YeniContext() =>
        new KitabeviDbContext(
            new DbContextOptionsBuilder<KitabeviDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

    // ─── Test 1: Boş DB ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_BosDatabaseDe_BosListeDoner()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var context = YeniContext();
        var handler = new KitapListeQueryHandler(context);
        var query   = new KitapListeQuery(Kategori: null);
        // KitapListeQuery record — Kategori null: tüm kitapları getir.

        // ─── Act ─────────────────────────────────────────────────────────────
        var sonuc = await handler.Handle(query, CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        sonuc.Should().BeEmpty("DB'de hiç kayıt yokken boş liste bekleniyor.");
        // Assert.Empty(sonuc) yerine: başarısız mesajı daha açıklayıcı.
    }

    // ─── Test 2: Üç kayıt — üç eleman ───────────────────────────────────────

    [Fact]
    public async Task Handle_UcKitapVar_UcElemanDoner()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var context = YeniContext();
        context.Kitaplar.AddRange(
            new Kitap { Baslik = "Kitap A", Yazar = "Y", Fiyat = 10m, Kategori = "Roman", StokAdedi = 1 },
            new Kitap { Baslik = "Kitap B", Yazar = "Y", Fiyat = 20m, Kategori = "Roman", StokAdedi = 2 },
            new Kitap { Baslik = "Kitap C", Yazar = "Y", Fiyat = 30m, Kategori = "Tarih", StokAdedi = 3 }
        );
        await context.SaveChangesAsync();
        // SaveChangesAsync: InMemory DB'ye kaydet; sonraki sorgularda görünür hale gelir.

        var handler = new KitapListeQueryHandler(context);

        // ─── Act ─────────────────────────────────────────────────────────────
        var sonuc = await handler.Handle(new KitapListeQuery(null), CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        sonuc.Should().HaveCount(3);
    }

    // ─── Test 3: Alfabetik sıralama ──────────────────────────────────────────

    [Fact]
    public async Task Handle_UcKitap_BaslikaGoreAlfabetikSiralanmis()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        // Kasıtlı olarak ters sırada ekliyoruz — handler sıralamalı mı döndürüyor?
        var context = YeniContext();
        context.Kitaplar.AddRange(
            new Kitap { Baslik = "Zürafa", Yazar = "Y", Fiyat = 10m, Kategori = "Çocuk", StokAdedi = 1 },
            new Kitap { Baslik = "Arı Maya", Yazar = "Y", Fiyat = 20m, Kategori = "Çocuk", StokAdedi = 1 },
            new Kitap { Baslik = "Maymun", Yazar = "Y", Fiyat = 30m, Kategori = "Çocuk", StokAdedi = 1 }
        );
        await context.SaveChangesAsync();

        var handler = new KitapListeQueryHandler(context);

        // ─── Act ─────────────────────────────────────────────────────────────
        var sonuc = await handler.Handle(new KitapListeQuery(null), CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        sonuc.Select(k => k.Baslik)
             .Should().BeInAscendingOrder(because: "Handler OrderBy(Baslik) uygulamalı.");
        // Beklenen: ["Arı Maya", "Maymun", "Zürafa"]
    }

    // ─── Test 4: Kategori filtresi ───────────────────────────────────────────

    [Fact]
    public async Task Handle_KategoriFiltreliQuery_SadeceTekKategoridekilerDoner()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var context = YeniContext();
        context.Kitaplar.AddRange(
            new Kitap { Baslik = "Roman 1", Yazar = "Y", Fiyat = 10m, Kategori = "Roman",  StokAdedi = 1 },
            new Kitap { Baslik = "Roman 2", Yazar = "Y", Fiyat = 20m, Kategori = "Roman",  StokAdedi = 1 },
            new Kitap { Baslik = "Tarih 1", Yazar = "Y", Fiyat = 30m, Kategori = "Tarih",  StokAdedi = 1 }
        );
        await context.SaveChangesAsync();

        var handler = new KitapListeQueryHandler(context);

        // ─── Act ─────────────────────────────────────────────────────────────
        var sonuc = await handler.Handle(new KitapListeQuery(Kategori: "Roman"), CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        sonuc.Should().HaveCount(2);
        sonuc.Should().OnlyContain(k => k.Kategori == "Roman",
            because: "Kategori filtresi sadece 'Roman' kitapları getirmeli.");
    }

    // ─── Test 5: DTO projeksiyon doğruluğu ───────────────────────────────────

    [Fact]
    public async Task Handle_TekKitap_DtoAlanlariDogruMaplenmis()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var context = YeniContext();
        var kitap = new Kitap
        {
            Baslik    = "Clean Architecture",
            Yazar     = "Robert C. Martin",
            Fiyat     = 135m,
            Kategori  = "Yazılım",
            StokAdedi = 8
        };
        context.Kitaplar.Add(kitap);
        await context.SaveChangesAsync();

        var handler = new KitapListeQueryHandler(context);

        // ─── Act ─────────────────────────────────────────────────────────────
        var sonuc = await handler.Handle(new KitapListeQuery(null), CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        sonuc.Should().HaveCount(1);

        var dto = sonuc.Single();
        // .Single(): tam bir eleman varsa döner; yoksa veya birden fazlaysa exception.
        // Sayıyı zaten HaveCount(1) ile doğruladık — Single() güvenli.

        dto.Baslik.Should().Be("Clean Architecture");
        dto.Yazar.Should().Be("Robert C. Martin");
        dto.Fiyat.Should().Be(135m);
        dto.Kategori.Should().Be("Yazılım");
        dto.StokAdedi.Should().Be(8);
        dto.Id.Should().BeGreaterThan(0, "EF Core SaveChanges sonrası ID atanmış olmalı.");
    }
}
