using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;

namespace KitabeviMVC.Tests.Services;

// ─────────────────────────────────────────────────────────────────────
// KitapServisi Unit Testleri
//
// KitapServisi dışa bağımlı değil (in-memory liste) → mock gerekmez.
// Her test yeni bir KitapServisi instance'ı oluşturur → tam izolasyon.
//
// Gün 29: IKitapServisi async'e çevrildi → tüm metodlar Task<T> döndürüyor.
// KitapServisi → Task.FromResult ile uyum sağlandı; testler await ile çağırır.
//
// Başlangıç verisi (KitapServisi'nin constructor'ında tanımlı):
//   Id=1 "Suç ve Ceza"          Roman    89₺   stok:12
//   Id=2 "1984"                 Roman    75₺   stok:8
//   Id=3 "Kısa Türk Tarihi"     Tarih   120₺   stok:5
//   Id=4 "Sapiens"              Tarih   140₺   stok:20
//   Id=5 "Atomik Alışkanlıklar" Kişisel  95₺   stok:30
// ─────────────────────────────────────────────────────────────────────
public class KitapServisiTests
{
    // ─── HepsiniGetirAsync ────────────────────────────────────────────

    [Fact]
    public async Task HepsiniGetirAsync_BaslangicVerisiyle_BosDegildir()
    {
        // Arrange
        var servis = new KitapServisi();

        // Act
        var sonuc = await servis.HepsiniGetirAsync();

        // Assert
        sonuc.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HepsiniGetirAsync_BaslangicVerisiyle_BesKitapDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = await servis.HepsiniGetirAsync();

        sonuc.Should().HaveCount(5);
    }

    [Fact]
    public async Task HepsiniGetirAsync_DonulenListe_BasligaGoreAlfabetikSiralidir()
    {
        // Arrange
        var servis = new KitapServisi();

        // Act
        var sonuc = await servis.HepsiniGetirAsync();

        // Assert — OrderBy(Baslik) ile sıralanmış olmalı
        sonuc.Should().BeInAscendingOrder(k => k.Baslik);
    }

    // ─── KategoriyeGoreGetirAsync ─────────────────────────────────────

    [Theory]
    [InlineData("Roman",   2)] // "1984" + "Suç ve Ceza"
    [InlineData("Tarih",   2)] // "Kısa Türk Tarihi" + "Sapiens"
    [InlineData("Kişisel", 1)] // "Atomik Alışkanlıklar"
    [InlineData("YokKategori", 0)]
    public async Task KategoriyeGoreGetirAsync_DogruKitapSayisiniDondurur(string kategori, int beklenenSayi)
    {
        // Arrange
        var servis = new KitapServisi();

        // Act
        var sonuc = await servis.KategoriyeGoreGetirAsync(kategori);

        // Assert
        sonuc.Should().HaveCount(beklenenSayi);
    }

    [Fact]
    public async Task KategoriyeGoreGetirAsync_BuyukKucukHarfDuyarsiz_DogruSonucDondurur()
    {
        // "roman", "Roman", "ROMAN" hepsi aynı sonucu vermeli
        var servis = new KitapServisi();

        var kucuk  = await servis.KategoriyeGoreGetirAsync("roman");
        var buyuk  = await servis.KategoriyeGoreGetirAsync("ROMAN");
        var pascal = await servis.KategoriyeGoreGetirAsync("Roman");

        kucuk.Should().HaveCount(2);
        buyuk.Should().HaveCount(2);
        pascal.Should().HaveCount(2);
    }

    // ─── BulByIdAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task BulByIdAsync_VarOlanId_DogruKitabiDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = await servis.BulByIdAsync(2); // "1984"

        sonuc.Should().NotBeNull();
        sonuc!.Id.Should().Be(2);
        sonuc.Baslik.Should().Be("1984");
        sonuc.Yazar.Should().Be("Orwell");
    }

    [Fact]
    public async Task BulByIdAsync_YokOlanId_NullDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = await servis.BulByIdAsync(9999);

        sonuc.Should().BeNull();
    }

    // ─── EkleAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task EkleAsync_GecerliModel_KitabiListeyeEkler()
    {
        // Arrange
        var servis = new KitapServisi();
        var yeniKitap = new KitapFormViewModel
        {
            Baslik    = "Sefiller",
            Yazar     = "Victor Hugo",
            Fiyat     = 110,
            Kategori  = "Roman",
            StokAdedi = 7
        };

        // Act
        await servis.EkleAsync(yeniKitap);

        // Assert
        var liste = await servis.HepsiniGetirAsync();
        liste.Should().HaveCount(6);
        liste.Should().Contain(k => k.Baslik == "Sefiller");
    }

    [Fact]
    public async Task EkleAsync_GecerliModel_SifirdenBuyukIdDondurur()
    {
        var servis = new KitapServisi();
        var model = new KitapFormViewModel { Baslik = "Yeni", Yazar = "Yazar", Kategori = "Roman" };

        var yeniId = await servis.EkleAsync(model);

        yeniId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EkleAsync_IkiKitap_FarkliIdlerAlir()
    {
        var servis = new KitapServisi();
        var model1 = new KitapFormViewModel { Baslik = "Kitap A", Yazar = "Y", Kategori = "Roman" };
        var model2 = new KitapFormViewModel { Baslik = "Kitap B", Yazar = "Y", Kategori = "Roman" };

        var id1 = await servis.EkleAsync(model1);
        var id2 = await servis.EkleAsync(model2);

        id1.Should().NotBe(id2);
    }

    // ─── GuncelleAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GuncelleAsync_VarOlanKitap_TrueDonderVeGunceller()
    {
        // Arrange
        var servis = new KitapServisi();
        var model = new KitapFormViewModel
        {
            Id       = 1,
            Baslik   = "Suç ve Ceza — Yeni Baskı",
            Yazar    = "Dostoyevski",
            Fiyat    = 99,
            Kategori = "Roman"
        };

        // Act
        var sonuc = await servis.GuncelleAsync(model);

        // Assert
        sonuc.Should().BeTrue();
        var guncellenen = await servis.BulByIdAsync(1);
        guncellenen!.Baslik.Should().Be("Suç ve Ceza — Yeni Baskı");
        guncellenen.Fiyat.Should().Be(99);
    }

    [Fact]
    public async Task GuncelleAsync_YokOlanId_FalseDondurur()
    {
        var servis = new KitapServisi();
        var model  = new KitapFormViewModel { Id = 9999, Baslik = "Yok", Yazar = "Y", Kategori = "Roman" };

        var sonuc = await servis.GuncelleAsync(model);

        sonuc.Should().BeFalse();
    }

    // ─── SilAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SilAsync_VarOlanKitap_TrueDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = await servis.SilAsync(1);

        sonuc.Should().BeTrue();
    }

    [Fact]
    public async Task SilAsync_VarOlanKitap_ListedekiSayiyiAzaltir()
    {
        var servis = new KitapServisi();

        await servis.SilAsync(1);

        var liste = await servis.HepsiniGetirAsync();
        liste.Should().HaveCount(4);
    }

    [Fact]
    public async Task SilAsync_VarOlanKitap_ArtikListedeGorunmez()
    {
        var servis = new KitapServisi();

        await servis.SilAsync(2); // "1984"

        var liste = await servis.HepsiniGetirAsync();
        liste.Should().NotContain(k => k.Id == 2);
    }

    [Fact]
    public async Task SilAsync_YokOlanId_FalseDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = await servis.SilAsync(9999);

        sonuc.Should().BeFalse();
    }

    [Fact]
    public async Task SilAsync_YokOlanId_ListeyiDegistirmez()
    {
        var servis = new KitapServisi();

        await servis.SilAsync(9999);

        var liste = await servis.HepsiniGetirAsync();
        liste.Should().HaveCount(5);
    }

    // ─── BaslikVarMiAsync ─────────────────────────────────────────────

    [Fact]
    public async Task BaslikVarMiAsync_MevcutBaslik_TrueDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = await servis.BaslikVarMiAsync("1984");

        sonuc.Should().BeTrue();
    }

    [Fact]
    public async Task BaslikVarMiAsync_YokBaslik_FalseDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = await servis.BaslikVarMiAsync("Olmayan Kitap");

        sonuc.Should().BeFalse();
    }

    [Fact]
    public async Task BaslikVarMiAsync_MevcutBaslik_HaricIdIleKendinHaricTutar()
    {
        // Güncelleme senaryosu: "1984" kitabını düzenlerken başlığı değiştirmiyoruz.
        // kendi ID'si (2) hariç tutulursa → aynı başlık var ama bu zaten kendisi → false
        var servis = new KitapServisi();

        var sonuc = await servis.BaslikVarMiAsync("1984", haricId: 2);

        // Id=2 hariç → "1984" başlığı başka kitapta yok → false
        sonuc.Should().BeFalse();
    }

    [Fact]
    public async Task BaslikVarMiAsync_BuyukKucukHarfDuyarsiz_TrueDondurur()
    {
        var servis = new KitapServisi();

        // "Sapiens" kayıtlı, "sapiens" ile sorgu
        var sonuc = await servis.BaslikVarMiAsync("sapiens");

        sonuc.Should().BeTrue();
    }
}
