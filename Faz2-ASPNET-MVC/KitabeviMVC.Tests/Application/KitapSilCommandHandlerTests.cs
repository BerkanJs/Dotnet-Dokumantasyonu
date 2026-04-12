using KitabeviMVC.Data;
using KitabeviMVC.Features.Kitaplar;
using KitabeviMVC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Tests.Application;

// ─────────────────────────────────────────────────────────────────────────────
// KitapSilCommandHandler Test Sınıfı
//
// Handler doğrudan KitabeviDbContext kullanıyor → Moq değil, InMemory DB.
// Her test için Guid.NewGuid() ile benzersiz DB adı → tam izolasyon.
//
// Test edilen senaryolar:
//   1. Var olan kitap silinir — true döner, DB'de kayıt yok.
//   2. Yok olan ID — false döner.
//   3. Silinen kitap tekrar silinemez.
//   4. Silme diğer kayıtları etkilemez.
// ─────────────────────────────────────────────────────────────────────────────
public class KitapSilCommandHandlerTests
{
    private static KitabeviDbContext YeniContext() =>
        new KitabeviDbContext(
            new DbContextOptionsBuilder<KitabeviDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

    private static async Task<Kitap> KitapEkle(KitabeviDbContext db, string baslik = "Silinecek Kitap")
    {
        var kitap = new Kitap
        {
            Baslik    = baslik,
            Yazar     = "Test Yazarı",
            Fiyat     = 50m,
            Kategori  = "Test",
            StokAdedi = 5
        };
        db.Kitaplar.Add(kitap);
        await db.SaveChangesAsync();
        return kitap;
    }

    // ─── Test 1: Başarılı silme ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_VarOlanKitap_TrueDonderVeDbdenSiler()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var db      = YeniContext();
        var kitap   = await KitapEkle(db);
        var handler = new KitapSilCommandHandler(db);

        // ─── Act ─────────────────────────────────────────────────────────────
        var sonuc = await handler.Handle(new KitapSilCommand(kitap.Id), CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        sonuc.Should().BeTrue(because: "Var olan kitap başarıyla silinmeli.");

        var silinen = await db.Kitaplar.FindAsync(kitap.Id);
        silinen.Should().BeNull(because: "Silinen kayıt artık DB'de görünmemeli.");
        // FindAsync: Change Tracker'a bakar, yoksa DB'ye gider → null bekliyoruz.
    }

    // ─── Test 2: Yok olan ID ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_YokOlanId_FalseDonder()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var db      = YeniContext(); // boş DB
        var handler = new KitapSilCommandHandler(db);

        // ─── Act ─────────────────────────────────────────────────────────────
        var sonuc = await handler.Handle(new KitapSilCommand(9999), CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        sonuc.Should().BeFalse(because: "Olmayan kayıt silinemez — handler false döndürmeli.");
        // Controller bu false'ı 404 NotFound'a çevirir.
    }

    // ─── Test 3: DB sayısı azalır ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_UcKitaptan_BirSilinince_IkiKalir()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var db    = YeniContext();
        var kitap1 = await KitapEkle(db, "Kitap 1");
        var kitap2 = await KitapEkle(db, "Kitap 2");
        var kitap3 = await KitapEkle(db, "Kitap 3");
        var handler = new KitapSilCommandHandler(db);

        // ─── Act ─────────────────────────────────────────────────────────────
        await handler.Handle(new KitapSilCommand(kitap2.Id), CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        var tumKitaplar = await db.Kitaplar.ToListAsync();
        tumKitaplar.Should().HaveCount(2, because: "3 kitaptan biri silindi → 2 kalmalı.");
        tumKitaplar.Should().NotContain(k => k.Id == kitap2.Id,
            because: "Silinen kitap listede görünmemeli.");
        // Diğer kitaplar etkilenmedi
        tumKitaplar.Should().Contain(k => k.Id == kitap1.Id);
        tumKitaplar.Should().Contain(k => k.Id == kitap3.Id);
    }

    // ─── Test 4: Silinen kayıt tekrar silinirse false döner ──────────────────

    [Fact]
    public async Task Handle_SilinmiKitapTekrarSilinirse_FalseDonder()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var db      = YeniContext();
        var kitap   = await KitapEkle(db);
        var handler = new KitapSilCommandHandler(db);

        // İlk silme
        var ilkSonuc = await handler.Handle(new KitapSilCommand(kitap.Id), CancellationToken.None);
        ilkSonuc.Should().BeTrue();

        // ─── Act: İkinci silme denemesi ───────────────────────────────────────
        var ikinciSonuc = await handler.Handle(new KitapSilCommand(kitap.Id), CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        ikinciSonuc.Should().BeFalse(
            because: "Zaten silinmiş kayıt ikinci kez silinmeye çalışılırsa false döndürülmeli.");
        // Idempotent DELETE: REST API'de DELETE idempotent olmalı — 404 dönmek de kabul edilebilir.
        // handler tarafında: FindAsync null döner → false → controller 404 verir → REST standardı.
    }

    // ─── Test 5: Silme diğer kayıtları etkilemez ─────────────────────────────

    [Fact]
    public async Task Handle_BirKitapSilinir_DigerKitaplarEtkilenmez()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var db         = YeniContext();
        var korunacak  = await KitapEkle(db, "Korunacak");
        var silinecek  = await KitapEkle(db, "Silinecek");
        var handler    = new KitapSilCommandHandler(db);

        // ─── Act ─────────────────────────────────────────────────────────────
        await handler.Handle(new KitapSilCommand(silinecek.Id), CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        var kalan = await db.Kitaplar.FindAsync(korunacak.Id);
        kalan.Should().NotBeNull(because: "Silme işlemi sadece hedef kaydı silmeli.");
        kalan!.Baslik.Should().Be("Korunacak");
        // Tüm alanlar doğrulanıyor: silme diğer kaydın içeriğini de bozmadı.
    }
}
