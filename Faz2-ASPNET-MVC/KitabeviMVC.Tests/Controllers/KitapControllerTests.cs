using KitabeviMVC.Controllers;
using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

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
// Gün 29: Tüm action'lar async Task<IActionResult> → testler async Task.
// ReturnsAsync: Moq'un async Setup için karşılığı (Returns → ReturnsAsync).
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
        var mockServis   = new Mock<IKitapServisi>();
        var mockSorgu    = new Mock<IKitapSorguServisi>();
        var mockBatch    = new Mock<IKitapBatchServisi>();
        var mockAuth     = new Mock<IAuthorizationService>();
        var logger       = NullLogger<KitapController>.Instance;

        var controller = new KitapController(
            mockServis.Object,
            mockSorgu.Object,
            mockBatch.Object,
            logger,
            mockAuth.Object);

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
    public async Task Liste_HepsiniGetirAsyncCagilinca_ViewResultDondurur()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.HepsiniGetirAsync()).ReturnsAsync(OrnekListe());
        // ReturnsAsync: async Task<T> döndüren metodlar için Returns'ün async karşılığı.

        // Act — Liste action async, await zorunlu
        var sonuc = await controller.Liste();

        // Assert
        sonuc.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task Liste_ViewResult_ModelKitapListesidir()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.HepsiniGetirAsync()).ReturnsAsync(OrnekListe());

        // Act
        var viewResult = (ViewResult)await controller.Liste();

        // Assert — view'a gönderilen model doğru tipte
        viewResult.Model.Should().BeAssignableTo<IReadOnlyList<KitapListeViewModel>>();
    }

    [Fact]
    public async Task Liste_HepsiniGetirAsyncCagirir()
    {
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.HepsiniGetirAsync()).ReturnsAsync(OrnekListe());

        await controller.Liste();

        mockServis.Verify(s => s.HepsiniGetirAsync(), Times.Once);
    }

    // ─── Detay (GET /kitaplar/detay/{id}) ────────────────────────────

    [Fact]
    public async Task Detay_VarOlanId_ViewResultDondurur()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        mockServis
            .Setup(s => s.BulByIdAsync(1))
            .ReturnsAsync(new KitapFormViewModel { Id = 1, Baslik = "1984" });

        // Act
        var sonuc = await controller.Detay(1);

        // Assert
        sonuc.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task Detay_VarOlanId_DogruModeleIleViewDondurur()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        var kitap = new KitapFormViewModel { Id = 1, Baslik = "1984", Yazar = "Orwell" };
        mockServis.Setup(s => s.BulByIdAsync(1)).ReturnsAsync(kitap);

        // Act
        var viewResult = (ViewResult)await controller.Detay(1);

        // Assert — doğru kitap view'a gönderildi
        var model = viewResult.Model.Should().BeOfType<KitapFormViewModel>().Subject;
        model.Id.Should().Be(1);
        model.Baslik.Should().Be("1984");
    }

    [Fact]
    public async Task Detay_YokOlanId_NotFoundDondurur()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.BulByIdAsync(9999)).ReturnsAsync((KitapFormViewModel?)null);

        // Act
        var sonuc = await controller.Detay(9999);

        // Assert
        sonuc.Should().BeOfType<NotFoundResult>();
    }

    // ─── Ekle POST (/kitaplar/ekle) ───────────────────────────────────

    [Fact]
    public async Task Ekle_Post_GecerliModel_EkleAsyncMetodunuCagirir()
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
        mockServis.Setup(s => s.BaslikVarMiAsync(model.Baslik, 0)).ReturnsAsync(false);
        mockServis.Setup(s => s.EkleAsync(model)).ReturnsAsync(6);

        // Act
        await controller.Ekle(model);

        // Assert — servis çağrıldı
        mockServis.Verify(s => s.EkleAsync(model), Times.Once);
    }

    [Fact]
    public async Task Ekle_Post_GecerliModel_DetayaRedirectEder()
    {
        // Başarılı eklemede PRG pattern: POST → Redirect → GET
        var (controller, mockServis) = OlusturController();
        var model = new KitapFormViewModel { Baslik = "Yeni", Yazar = "Y", Kategori = "Roman" };
        mockServis.Setup(s => s.BaslikVarMiAsync(model.Baslik, 0)).ReturnsAsync(false);
        mockServis.Setup(s => s.EkleAsync(model)).ReturnsAsync(6);

        // Act
        var sonuc = await controller.Ekle(model);

        // Assert — Detay action'ına redirect
        var redirect = sonuc.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Detay");
        redirect.RouteValues!["id"].Should().Be(6);
    }

    [Fact]
    public async Task Ekle_Post_CakisanBaslik_EkleAsyncMetodunuCagirmaz()
    {
        // BaslikVarMiAsync true döndürürse → EkleAsync çağrılmamalı
        var (controller, mockServis) = OlusturController();
        var model = new KitapFormViewModel { Baslik = "1984", Yazar = "Y", Kategori = "Roman" };
        mockServis.Setup(s => s.BaslikVarMiAsync("1984", 0)).ReturnsAsync(true); // başlık mevcut

        // Act
        await controller.Ekle(model);

        // Assert — EkleAsync çağrılmadı
        mockServis.Verify(s => s.EkleAsync(It.IsAny<KitapFormViewModel>()), Times.Never);
    }

    [Fact]
    public async Task Ekle_Post_CakisanBaslik_ViewTekrarDondurur()
    {
        // Hata durumunda kullanıcı formu tekrar görür
        var (controller, mockServis) = OlusturController();
        var model = new KitapFormViewModel { Baslik = "1984", Yazar = "Y", Kategori = "Roman" };
        mockServis.Setup(s => s.BaslikVarMiAsync("1984", 0)).ReturnsAsync(true);

        // Act
        var sonuc = await controller.Ekle(model);

        // Assert
        sonuc.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task Ekle_Post_CakisanBaslik_ModelStateHatasiEkler()
    {
        // ModelState'e "Baslik" için hata eklenmeli
        var (controller, mockServis) = OlusturController();
        var model = new KitapFormViewModel { Baslik = "1984", Yazar = "Y", Kategori = "Roman" };
        mockServis.Setup(s => s.BaslikVarMiAsync("1984", 0)).ReturnsAsync(true);

        // Act
        await controller.Ekle(model);

        // Assert
        controller.ModelState.Should().ContainKey("Baslik");
    }

    // ─── Sil POST (/kitaplar/sil/{id}) ────────────────────────────────

    [Fact]
    public async Task Sil_VarOlanId_SilAsyncMetodunuCagirir()
    {
        // Arrange
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.BulByIdAsync(1)).ReturnsAsync(new KitapFormViewModel { Id = 1, Baslik = "1984" });
        mockServis.Setup(s => s.SilAsync(1)).ReturnsAsync(true);

        // Act
        await controller.Sil(1);

        // Assert
        mockServis.Verify(s => s.SilAsync(1), Times.Once);
    }

    [Fact]
    public async Task Sil_VarOlanId_ListeyeRedirectEder()
    {
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.BulByIdAsync(1)).ReturnsAsync(new KitapFormViewModel { Id = 1, Baslik = "1984" });
        mockServis.Setup(s => s.SilAsync(1)).ReturnsAsync(true);

        // Act
        var sonuc = await controller.Sil(1);

        // Assert
        var redirect = sonuc.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Liste");
    }

    [Fact]
    public async Task Sil_YokOlanId_NotFoundDondurur()
    {
        // Arrange — BulByIdAsync null döner → NotFound
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.BulByIdAsync(9999)).ReturnsAsync((KitapFormViewModel?)null);

        // Act
        var sonuc = await controller.Sil(9999);

        // Assert
        sonuc.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Sil_YokOlanId_SilAsyncMetodunuCagirmaz()
    {
        // BulByIdAsync null dönerse SilAsync çağrılmamalı
        var (controller, mockServis) = OlusturController();
        mockServis.Setup(s => s.BulByIdAsync(9999)).ReturnsAsync((KitapFormViewModel?)null);

        // Act
        await controller.Sil(9999);

        // Assert
        mockServis.Verify(s => s.SilAsync(It.IsAny<int>()), Times.Never);
    }
}
