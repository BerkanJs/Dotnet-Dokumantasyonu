using KitabeviMVC.Features.Kitaplar;
using KitabeviMVC.Data;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Tests.Application;

// ─────────────────────────────────────────────────────────────────────────────
// KitapEkleCommandHandler Test Sınıfı
//
// KitapEkleCommandHandler doğrudan KitabeviDbContext kullanıyor (IKitapRepository değil).
// Bu nedenle Moq ile mock edemeyiz — InMemoryDatabase kullanıyoruz.
//
// Her test için yeni DbContext: Guid.NewGuid() ile benzersiz DB adı.
// Bunu yapmassaydık testler arası state paylaşımı olurdu → sıraya bağımlı hatalar.
// ─────────────────────────────────────────────────────────────────────────────
public class KitapEkleCommandHandlerTests
{
    // Her test için taze InMemoryDatabase seçeneği oluşturan yardımcı metod.
    private static DbContextOptions<KitabeviDbContext> YeniOptions() =>
        new DbContextOptionsBuilder<KitabeviDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            // Guid.NewGuid(): her testte farklı DB adı → testler birbirini görmez.
            // Sabit "TestDb" yazmak: paralel çalışırken veya state sızarsa testler birbirini etkiler.
            .Options;

    // ─── Test 1: Başarılı ekleme — DB'de kayıt oluştu mu? ───────────────────

    [Fact]
    public async Task Handle_GecerliCommand_KitapVeritabanindaOlusur()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var options = YeniOptions();
        var context = new KitabeviDbContext(options);
        var handler = new KitapEkleCommandHandler(context);

        var command = new KitapEkleCommand(
            Baslik:    "Clean Code",
            Yazar:     "Robert C. Martin",
            Fiyat:     120m,
            Kategori:  "Yazılım",
            StokAdedi: 10
        );
        // KitapEkleCommand bir record — positional constructor ile kısa tanım.

        // ─── Act ─────────────────────────────────────────────────────────────
        var yeniId = await handler.Handle(command, CancellationToken.None);
        // CancellationToken.None: iptal sinyali yok — test ortamında timeout riski düşük.

        // ─── Assert ──────────────────────────────────────────────────────────
        var kaydedilen = await context.Kitaplar.FindAsync(yeniId);
        // FindAsync: primary key ile hızlı arama — önce tracker'a, sonra DB'ye bakar.
        // Bunu FirstOrDefaultAsync(k => k.Id == yeniId) ile de yazabilirdik, daha uzun.

        kaydedilen.Should().NotBeNull("Handler yeni kayıt oluşturmalıydı.");
        kaydedilen!.Baslik.Should().Be("Clean Code");
        kaydedilen.Yazar.Should().Be("Robert C. Martin");
        kaydedilen.Fiyat.Should().Be(120m);
        kaydedilen.Kategori.Should().Be("Yazılım");
        kaydedilen.StokAdedi.Should().Be(10);
    }

    // ─── Test 2: Handle dönüş değeri sıfırdan büyük mü? ─────────────────────

    [Fact]
    public async Task Handle_GecerliCommand_SifirdenBuyukIdDoner()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var context = new KitabeviDbContext(YeniOptions());
        var handler = new KitapEkleCommandHandler(context);

        var command = new KitapEkleCommand(
            Baslik: "Domain-Driven Design",
            Yazar: "Eric Evans",
            Fiyat: 150m,
            Kategori: "Yazılım",
            StokAdedi: 5
        );

        // ─── Act ─────────────────────────────────────────────────────────────
        var yeniId = await handler.Handle(command, CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        yeniId.Should().BeGreaterThan(0,
            because: "EF Core SaveChanges sonrası DB Id'si atanmış olmalı.");
        // InMemoryDatabase: Id'yi 1'den başlatarak otomatik atar.
        // SaveChanges öncesi Id okunmuş olsaydı: 0 dönerdi (DB henüz atamamış).
    }

    // ─── Test 3: Stok sıfır geçerli kayıt mı? ───────────────────────────────

    [Fact]
    public async Task Handle_StokSifir_KayitOlusur()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        // İş kuralı: Yeni yayınlanmış kitap stoksuz sisteme girilebilir.
        // "Stok 0'a izin ver" veya "stok 0 hata ver" — bu test iş kuralını belgeler.
        var context = new KitabeviDbContext(YeniOptions());
        var handler = new KitapEkleCommandHandler(context);

        var command = new KitapEkleCommand(
            Baslik:    "Yeni Çıkacak Kitap",
            Yazar:     "Yazar",
            Fiyat:     80m,
            Kategori:  "Roman",
            StokAdedi: 0   // ← stok sıfır
        );

        // ─── Act ─────────────────────────────────────────────────────────────
        var yeniId = await handler.Handle(command, CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        var kaydedilen = await context.Kitaplar.FindAsync(yeniId);
        kaydedilen.Should().NotBeNull();
        kaydedilen!.StokAdedi.Should().Be(0,
            because: "Stok sıfır geçerli iş senaryosu; handler reddetmemeli.");
    }

    // ─── Test 4: İki ayrı komut — iki ayrı kayıt ───────────────────────────

    [Fact]
    public async Task Handle_IkiKomut_IkiAyriKayitOlusur()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var context = new KitabeviDbContext(YeniOptions());
        var handler = new KitapEkleCommandHandler(context);

        // ─── Act ─────────────────────────────────────────────────────────────
        var id1 = await handler.Handle(
            new KitapEkleCommand("Kitap A", "Yazar A", 50m, "Roman", 3),
            CancellationToken.None);

        var id2 = await handler.Handle(
            new KitapEkleCommand("Kitap B", "Yazar B", 60m, "Roman", 7),
            CancellationToken.None);

        // ─── Assert ──────────────────────────────────────────────────────────
        id1.Should().NotBe(id2, because: "Her ekleme benzersiz ID almalı.");

        var tumKitaplar = await context.Kitaplar.ToListAsync();
        tumKitaplar.Should().HaveCount(2);
    }
}
