using KitabeviMVC.Data;
using KitabeviMVC.Features.Kitaplar;
using KitabeviMVC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Tests.Application;

// ─────────────────────────────────────────────────────────────────────────────
// KitapGuncelleCommandHandler Test Sınıfı
//
// Handler doğrudan KitabeviDbContext kullanıyor → Moq değil, InMemory DB.
// Her test için Guid.NewGuid() ile benzersiz DB adı → tam izolasyon.
//
// Test edilen senaryolar:
//   1. Var olan kitap güncellenir — true döner ve DB'de değişiklik görünür.
//   2. Yok olan ID — false döner, DB'ye dokunulmaz.
//   3. Kısmi güncelleme — sadece değiştirilen alanlar kontrol edilir.
//   4. Stok sıfıra inebilir — iş kuralı: geçerli senaryo.
// ─────────────────────────────────────────────────────────────────────────────
public class KitapGuncelleCommandHandlerTests
{
    private static KitabeviDbContext YeniContext() =>
        new KitabeviDbContext(
            new DbContextOptionsBuilder<KitabeviDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

    // Seed: test kitabı oluşturup kaydeden yardımcı metod.
    private static async Task<Kitap> KitapEkle(KitabeviDbContext db, string baslik = "Test Kitabı")
    {
        var kitap = new Kitap
        {
            Baslik    = baslik,
            Yazar     = "Test Yazarı",
            Fiyat     = 100m,
            Kategori  = "Test",
            StokAdedi = 10
        };
        db.Kitaplar.Add(kitap);
        await db.SaveChangesAsync();
        return kitap;
        // Kaydedilen kitabı döndür: Id EF Core tarafından atandı.
    }

    // ─── Test 1: Başarılı güncelleme ─────────────────────────────────────────

    [Fact]
    public async Task Handle_VarOlanKitap_TrueDonderVeAlanlariGunceller()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var db     = YeniContext();
        var kitap  = await KitapEkle(db);
        var handler = new KitapGuncelleCommandHandler(db);

        var komut = new KitapGuncelleCommand(
            Id:        kitap.Id,
            Baslik:    "Güncellenmiş Başlık",
            Yazar:     "Güncellenmiş Yazar",
            Fiyat:     150m,
            Kategori:  "Yazılım",
            StokAdedi: 5
        );

        // ─── Act ─────────────────────────────────────────────────────────────
        var sonuc = await handler.Handle(komut, CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        sonuc.Should().BeTrue(because: "Var olan kitap başarıyla güncellenmeli.");

        var guncel = await db.Kitaplar.FindAsync(kitap.Id);
        guncel.Should().NotBeNull();
        guncel!.Baslik.Should().Be("Güncellenmiş Başlık");
        guncel.Yazar.Should().Be("Güncellenmiş Yazar");
        guncel.Fiyat.Should().Be(150m);
        guncel.Kategori.Should().Be("Yazılım");
        guncel.StokAdedi.Should().Be(5);
        // Her alan ayrı ayrı doğrulanıyor: hangi alan güncellenmedi tespit edilebilir.
    }

    // ─── Test 2: Var olmayan ID ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_YokOlanId_FalseDonder()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var db     = YeniContext(); // boş DB
        var handler = new KitapGuncelleCommandHandler(db);

        var komut = new KitapGuncelleCommand(
            Id:        9999,           // var olmayan ID
            Baslik:    "Yok Kitap",
            Yazar:     "Yazar",
            Fiyat:     50m,
            Kategori:  "Roman",
            StokAdedi: 1
        );

        // ─── Act ─────────────────────────────────────────────────────────────
        var sonuc = await handler.Handle(komut, CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        sonuc.Should().BeFalse(because: "Olmayan kayıt güncellenemez — handler false döndürmeli.");
        // Controller bu false'ı 404 NotFound'a çevirir.
        // Exception fırlatmak: her çağıran try/catch zorunda → handler tasarımı kötü.
    }

    // ─── Test 3: Yok olan ID — DB'ye dokunulmaz ──────────────────────────────

    [Fact]
    public async Task Handle_YokOlanId_DigerkayitlarEtkilenmez()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var db    = YeniContext();
        var kitap = await KitapEkle(db, "Korunacak Kitap");
        var handler = new KitapGuncelleCommandHandler(db);

        var komut = new KitapGuncelleCommand(
            Id:        9999,           // var olmayan ID
            Baslik:    "Yok Kitap",
            Yazar:     "Y",
            Fiyat:     50m,
            Kategori:  "Roman",
            StokAdedi: 1
        );

        // ─── Act ─────────────────────────────────────────────────────────────
        await handler.Handle(komut, CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        // Var olan kitap değişmemeli
        var korunan = await db.Kitaplar.FindAsync(kitap.Id);
        korunan!.Baslik.Should().Be("Korunacak Kitap",
            because: "Yanlış ID ile gelen güncelleme diğer kayıtları etkilememeli.");
    }

    // ─── Test 4: Stok sıfıra inebilir ────────────────────────────────────────

    [Fact]
    public async Task Handle_StokSifiraIndirilir_BasariliGuncelleme()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var db    = YeniContext();
        var kitap = await KitapEkle(db);
        var handler = new KitapGuncelleCommandHandler(db);

        var komut = new KitapGuncelleCommand(
            Id:        kitap.Id,
            Baslik:    kitap.Baslik,
            Yazar:     kitap.Yazar,
            Fiyat:     kitap.Fiyat,
            Kategori:  kitap.Kategori,
            StokAdedi: 0              // stok sıfıra düşürülüyor
        );

        // ─── Act ─────────────────────────────────────────────────────────────
        var sonuc = await handler.Handle(komut, CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        sonuc.Should().BeTrue();

        var guncel = await db.Kitaplar.FindAsync(kitap.Id);
        guncel!.StokAdedi.Should().Be(0,
            because: "Stok sıfır geçerli iş senaryosu — tükenen ürün.");
    }

    // ─── Test 5: Dönüş değeri True — bir kez True döner ─────────────────────

    [Fact]
    public async Task Handle_AyniKomutIkiKez_HerIkisiDeTrueDonder()
    {
        // Güncelleme idempotent mi? Aynı veriyle iki kez güncelleme yapılabilmeli.
        // ─── Arrange ────────────────────────────────────────────────────────
        var db    = YeniContext();
        var kitap = await KitapEkle(db);
        var handler = new KitapGuncelleCommandHandler(db);

        var komut = new KitapGuncelleCommand(
            Id:        kitap.Id,
            Baslik:    "Yeni Başlık",
            Yazar:     "Yeni Yazar",
            Fiyat:     75m,
            Kategori:  "Roman",
            StokAdedi: 3
        );

        // ─── Act ─────────────────────────────────────────────────────────────
        var sonuc1 = await handler.Handle(komut, CancellationToken.None);
        var sonuc2 = await handler.Handle(komut, CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        sonuc1.Should().BeTrue();
        sonuc2.Should().BeTrue(because: "Aynı verilerle tekrar güncelleme de başarılı olmalı.");
    }
}
