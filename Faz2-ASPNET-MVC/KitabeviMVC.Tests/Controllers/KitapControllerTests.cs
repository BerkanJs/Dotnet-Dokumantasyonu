using FluentAssertions;
using KitabeviMVC.Controllers;
using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KitabeviMVC.Tests.Controllers;

// ─────────────────────────────────────────────────────────────────────
// KitapController Unit Testleri
//
// HTTP pipeline çalışmaz — action metodu doğrudan çağrılır.
// [Authorize], [ValidateAntiForgeryToken], [TypeFilter(ValidationFilter)]
// attribute'ları çalışmaz — bunlar pipeline seviyesinde.
//
// ModelState unit testte varsayılan olarak geçerlidir.
// ValidationFilter (pipeline) geçtiğini varsayıyoruz.
//
// Bağımlılıklar:
//   IKitapServisi    → Mock
//   ILogger          → NullLogger (gerçek logger test çıktısını kirletir)
//   IAuthorizationService → Mock (resource-based auth için)
// ─────────────────────────────────────────────────────────────────────
public class KitapControllerTests
{
    // ─── Yardımcı fabrika ─────────────────────────────────────────────

    private static (KitapController controller, Mock<IKitapServisi> mockServis)
        OlusturController()
    {
        var mockServis = new Mock<IKitapServisi>();
        var mockAuth   = new Mock<IAuthorizationService>();
        var logger     = NullLogger<KitapController>.Instance;

        var controller = new KitapController(mockServis.Object, logger, mockAuth.Object);

        // ControllerContext: TempData, User, HttpContext için gerekli
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // TempData: Ekle/Sil action'larında TempData["BasariMesaji"] kullanılıyor
        // Gerçek provider gerekmez — mock yeterli
        controller.TempData = new TempDataDictionary(
            controller.ControllerContext.HttpContext,
            Mock.Of<ITempDataProvider>());

        return (controller, mockServis);
    }

    private static List<KitapListeViewModel> OrnekListe() =>
    [
        new(1, "1984",    "Orwell",  75, "Roman", 8),
        new(2, "Sapiens", "Harari", 140, "Tarih", 20)
    ];

    // ─── Liste (GET /kitaplar) ────────────────────────────────────────

    [Fact]
    public void Liste_HepsiniGetirCagilinca_ViewResultDondurur()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.HepsiniGetir()).Returns(OrnekListe());

        // Act
        var sonuc = controller.Liste();

        // Assert
        sonuc.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void Liste_ViewResult_ModelKitapListesidir()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.HepsiniGetir()).Returns(OrnekListe());

        // Act
        var viewResult = (ViewResult)controller.Liste();

        // Assert — view'a gönderilen model doğru tipte
        viewResult.Model.Should().BeAssignableTo<IReadOnlyList<KitapListeViewModel>>();
    }

    [Fact]
    public void Liste_HepsiniGetiriCagirir()
    {
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.HepsiniGetir()).Returns(OrnekListe());

        controller.Liste();

        mockServis.Verify(s => s.HepsiniGetir(), Times.Once);
    }

    // ─── Detay (GET /kitaplar/detay/{id}) ────────────────────────────

    [Fact]
    public void Detay_VarOlanId_ViewResultDondurur()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        mockServis
            .Setup(s => s.BulById(1))
            .Returns(new KitapFormViewModel { Id = 1, Baslik = "1984" });

        // Act
        var sonuc = controller.Detay(1);

        // Assert
        sonuc.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void Detay_VarOlanId_DogruModeleIleViewDondurur()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        var kitap = new KitapFormViewModel { Id = 1, Baslik = "1984", Yazar = "Orwell" };
        mockServis.Setup(s => s.BulById(1)).Returns(kitap);

        // Act
        var viewResult = (ViewResult)controller.Detay(1);

        // Assert — doğru kitap view'a gönderildi
        var model = viewResult.Model.Should().BeOfType<KitapFormViewModel>().Subject;
        model.Id.Should().Be(1);
        model.Baslik.Should().Be("1984");
    }

    [Fact]
    public void Detay_YokOlanId_NotFoundDondurur()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.BulById(9999)).Returns((KitapFormViewModel?)null);

        // Act
        var sonuc = controller.Detay(9999);

        // Assert
        sonuc.Should().BeOfType<NotFoundResult>();
    }

    // ─── Ekle POST (/kitaplar/ekle) ───────────────────────────────────

    [Fact]
    public void Ekle_Post_GecerliModel_EkleMetodunuCagirir()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        var model = new KitapFormViewModel
        {
            Baslik   = "Yeni Kitap",
            Yazar    = "Yazar",
            Kategori = "Roman",
            Fiyat    = 90
        };
        mockServis.Setup(s => s.BaslikVarMi(model.Baslik, 0)).Returns(false);
        mockServis.Setup(s => s.Ekle(model)).Returns(6);

        // Act
        controller.Ekle(model);

        // Assert — servis çağrıldı
        mockServis.Verify(s => s.Ekle(model), Times.Once);
    }

    [Fact]
    public void Ekle_Post_GecerliModel_DetayaRedirectEder()
    {
        // Başarılı eklemede PRG pattern: POST → Redirect → GET
        var (controller, mockServis) = OlusturController();
        var model = new KitapFormViewModel { Baslik = "Yeni", Yazar = "Y", Kategori = "Roman" };
        mockServis.Setup(s => s.BaslikVarMi(model.Baslik, 0)).Returns(false);
        mockServis.Setup(s => s.Ekle(model)).Returns(6);

        // Act
        var sonuc = controller.Ekle(model);

        // Assert — Detay action'ına redirect
        var redirect = sonuc.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Detay");
        redirect.RouteValues!["id"].Should().Be(6);
    }

    [Fact]
    public void Ekle_Post_CakisanBaslik_EkleMetodunuCagirmaz()
    {
        // BaslikVarMi true döndürürse → Ekle çağrılmamalı
        var (controller, mockServis) = OlusturController();
        var model = new KitapFormViewModel { Baslik = "1984", Yazar = "Y", Kategori = "Roman" };
        mockServis.Setup(s => s.BaslikVarMi("1984", 0)).Returns(true); // başlık mevcut

        // Act
        controller.Ekle(model);

        // Assert — Ekle çağrılmadı
        mockServis.Verify(s => s.Ekle(It.IsAny<KitapFormViewModel>()), Times.Never);
    }

    [Fact]
    public void Ekle_Post_CakisanBaslik_ViewTekrarDondurur()
    {
        // Hata durumunda kullanıcı formu tekrar görür
        var (controller, mockServis) = OlusturController();
        var model = new KitapFormViewModel { Baslik = "1984", Yazar = "Y", Kategori = "Roman" };
        mockServis.Setup(s => s.BaslikVarMi("1984", 0)).Returns(true);

        // Act
        var sonuc = controller.Ekle(model);

        // Assert
        sonuc.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void Ekle_Post_CakisanBaslik_ModelStateHatasiEkler()
    {
        // ModelState'e "Baslik" için hata eklenmeli
        var (controller, mockServis) = OlusturController();
        var model = new KitapFormViewModel { Baslik = "1984", Yazar = "Y", Kategori = "Roman" };
        mockServis.Setup(s => s.BaslikVarMi("1984", 0)).Returns(true);

        // Act
        controller.Ekle(model);

        // Assert
        controller.ModelState.Should().ContainKey("Baslik");
    }

    // ─── Sil POST (/kitaplar/sil/{id}) ────────────────────────────────

    [Fact]
    public void Sil_VarOlanId_SilMetodunuCagirir()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.BulById(1)).Returns(new KitapFormViewModel { Id = 1, Baslik = "1984" });
        mockServis.Setup(s => s.Sil(1)).Returns(true);

        // Act
        controller.Sil(1);

        // Assert
        mockServis.Verify(s => s.Sil(1), Times.Once);
    }

    [Fact]
    public void Sil_VarOlanId_ListeyeRedirectEder()
    {
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.BulById(1)).Returns(new KitapFormViewModel { Id = 1, Baslik = "1984" });
        mockServis.Setup(s => s.Sil(1)).Returns(true);

        // Act
        var sonuc = controller.Sil(1);

        // Assert
        var redirect = sonuc.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Liste");
    }

    [Fact]
    public void Sil_YokOlanId_NotFoundDondurur()
    {
        // Arrange — BulById null döner → NotFound
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.BulById(9999)).Returns((KitapFormViewModel?)null);

        // Act
        var sonuc = controller.Sil(9999);

        // Assert
        sonuc.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Sil_YokOlanId_SilMetodunuCagirmaz()
    {
        // BulById null dönerse Sil çağrılmamalı
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.BulById(9999)).Returns((KitapFormViewModel?)null);

        // Act
        controller.Sil(9999);

        // Assert
        mockServis.Verify(s => s.Sil(It.IsAny<int>()), Times.Never);
    }
}
