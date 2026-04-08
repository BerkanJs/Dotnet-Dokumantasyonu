using FluentAssertions;
using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;
using Xunit;

namespace KitabeviMVC.Tests.Services;

// ─────────────────────────────────────────────────────────────────────
// KitapServisi Unit Testleri
//
// KitapServisi dışa bağımlı değil (in-memory liste) → mock gerekmez.
// Her test yeni bir KitapServisi instance'ı oluşturur → tam izolasyon.
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
    // ─── HepsiniGetir ─────────────────────────────────────────────────

    [Fact]
    public void HepsiniGetir_BaslangicVerisiyle_BosDegildir()
    {
        // Arrange
        var servis = new KitapServisi();

        // Act
        var sonuc = servis.HepsiniGetir();

        // Assert
        sonuc.Should().NotBeEmpty();
    }

    [Fact]
    public void HepsiniGetir_BaslangicVerisiyle_BesKitapDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = servis.HepsiniGetir();

        sonuc.Should().HaveCount(5);
    }

    [Fact]
    public void HepsiniGetir_DonulenListe_BasligaGoreAlfabetikSiralidir()
    {
        // Arrange
        var servis = new KitapServisi();

        // Act
        var sonuc = servis.HepsiniGetir();

        // Assert — OrderBy(Baslik) ile sıralanmış olmalı
        // Beklenen sıra: 1984, Atomik Alışkanlıklar, Kısa Türk Tarihi, Sapiens, Suç ve Ceza
        sonuc.Should().BeInAscendingOrder(k => k.Baslik);
    }

    // ─── KategoriyeGoreGetir ──────────────────────────────────────────

    [Theory]
    [InlineData("Roman",   2)] // "1984" + "Suç ve Ceza"
    [InlineData("Tarih",   2)] // "Kısa Türk Tarihi" + "Sapiens"
    [InlineData("Kişisel", 1)] // "Atomik Alışkanlıklar"
    [InlineData("YokKategori", 0)]
    public void KategoriyeGoreGetir_DogruKitapSayisiniDondurur(string kategori, int beklenenSayi)
    {
        // Arrange
        var servis = new KitapServisi();

        // Act
        var sonuc = servis.KategoriyeGoreGetir(kategori);

        // Assert
        sonuc.Should().HaveCount(beklenenSayi);
    }

    [Fact]
    public void KategoriyeGoreGetir_BuyukKucukHarfDuyarsiz_DogruSonucDondurur()
    {
        // "roman", "Roman", "ROMAN" hepsi aynı sonucu vermeli
        var servis = new KitapServisi();

        var kucuk  = servis.KategoriyeGoreGetir("roman");
        var buyuk  = servis.KategoriyeGoreGetir("ROMAN");
        var pascal = servis.KategoriyeGoreGetir("Roman");

        kucuk.Should().HaveCount(2);
        buyuk.Should().HaveCount(2);
        pascal.Should().HaveCount(2);
    }

    // ─── BulById ──────────────────────────────────────────────────────

    [Fact]
    public void BulById_VarOlanId_DogruKitabiDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = servis.BulById(2); // "1984"

        sonuc.Should().NotBeNull();
        sonuc!.Id.Should().Be(2);
        sonuc.Baslik.Should().Be("1984");
        sonuc.Yazar.Should().Be("Orwell");
    }

    [Fact]
    public void BulById_YokOlanId_NullDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = servis.BulById(9999);

        sonuc.Should().BeNull();
    }

    // ─── Ekle ─────────────────────────────────────────────────────────

    [Fact]
    public void Ekle_GecerliModel_KitabiListeyeEkler()
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
        servis.Ekle(yeniKitap);

        // Assert
        var liste = servis.HepsiniGetir();
        liste.Should().HaveCount(6);
        liste.Should().Contain(k => k.Baslik == "Sefiller");
    }

    [Fact]
    public void Ekle_GecerliModel_SifirdenBuyukIdDondurur()
    {
        var servis = new KitapServisi();
        var model = new KitapFormViewModel { Baslik = "Yeni", Yazar = "Yazar", Kategori = "Roman" };

        var yeniId = servis.Ekle(model);

        yeniId.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Ekle_IkiKitap_ArtalanIdlerFarklidir()
    {
        var servis = new KitapServisi();
        var model1 = new KitapFormViewModel { Baslik = "Kitap A", Yazar = "Y", Kategori = "Roman" };
        var model2 = new KitapFormViewModel { Baslik = "Kitap B", Yazar = "Y", Kategori = "Roman" };

        var id1 = servis.Ekle(model1);
        var id2 = servis.Ekle(model2);

        id1.Should().NotBe(id2);
    }

    // ─── Guncelle ─────────────────────────────────────────────────────

    [Fact]
    public void Guncelle_VarOlanKitap_TrueDonderVeGunceller()
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
        var sonuc = servis.Guncelle(model);

        // Assert
        sonuc.Should().BeTrue();
        var guncellenen = servis.BulById(1);
        guncellenen!.Baslik.Should().Be("Suç ve Ceza — Yeni Baskı");
        guncellenen.Fiyat.Should().Be(99);
    }

    [Fact]
    public void Guncelle_YokOlanId_FalseDondurur()
    {
        var servis = new KitapServisi();
        var model  = new KitapFormViewModel { Id = 9999, Baslik = "Yok", Yazar = "Y", Kategori = "Roman" };

        var sonuc = servis.Guncelle(model);

        sonuc.Should().BeFalse();
    }

    // ─── Sil ──────────────────────────────────────────────────────────

    [Fact]
    public void Sil_VarOlanKitap_TrueDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = servis.Sil(1);

        sonuc.Should().BeTrue();
    }

    [Fact]
    public void Sil_VarOlanKitap_ListedekiSayiyiAzaltir()
    {
        var servis = new KitapServisi();

        servis.Sil(1);

        servis.HepsiniGetir().Should().HaveCount(4);
    }

    [Fact]
    public void Sil_VarOlanKitap_ArtikListedeGorunnmez()
    {
        var servis = new KitapServisi();

        servis.Sil(2); // "1984"

        servis.HepsiniGetir().Should().NotContain(k => k.Id == 2);
    }

    [Fact]
    public void Sil_YokOlanId_FalseDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = servis.Sil(9999);

        sonuc.Should().BeFalse();
    }

    [Fact]
    public void Sil_YokOlanId_ListeyiDegistirmez()
    {
        var servis = new KitapServisi();

        servis.Sil(9999);

        servis.HepsiniGetir().Should().HaveCount(5);
    }

    // ─── BaslikVarMi ──────────────────────────────────────────────────

    [Fact]
    public void BaslikVarMi_MevcutBaslik_TrueDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = servis.BaslikVarMi("1984");

        sonuc.Should().BeTrue();
    }

    [Fact]
    public void BaslikVarMi_YokBaslik_FalseDondurur()
    {
        var servis = new KitapServisi();

        var sonuc = servis.BaslikVarMi("Olmayan Kitap");

        sonuc.Should().BeFalse();
    }

    [Fact]
    public void BaslikVarMi_MevcutBaslik_HaricIdIleKendinHaricTutar()
    {
        // Güncelleme senaryosu: "1984" kitabını düzenlerken başlığı değiştirmiyoruz.
        // kendi ID'si (2) hariç tutulursa → aynı başlık var ama bu zaten kendisi → false
        var servis = new KitapServisi();

        var sonuc = servis.BaslikVarMi("1984", haricId: 2);

        // Id=2 hariç → "1984" başlığı artık başka kitapta yok → false
        sonuc.Should().BeFalse();
    }

    [Fact]
    public void BaslikVarMi_BuyukKucukHarfDuyarsiz_TrueDondurur()
    {
        var servis = new KitapServisi();

        // "1984" kaydedilmiş, "1984" ile sorgu — aynı değer olduğu için case-insensitive önemsiz
        // Daha iyi örnek: harfli başlık
        var sonuc = servis.BaslikVarMi("sapiens"); // "Sapiens" kayıtlı

        sonuc.Should().BeTrue();
    }
}
