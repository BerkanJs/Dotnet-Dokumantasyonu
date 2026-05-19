using KitabeviMediatr.Application.UseCases.KitapEkle;
using KitabeviMediatr.Domain.Entities;
using KitabeviMediatr.Domain.Exceptions;
using KitabeviMediatr.Domain.Interfaces;
using KitabeviMediatr.Domain.ValueObjects;
using Moq;

namespace KitabeviMediatr.Tests.Application;

public class KitapEkleHandlerTests
{
    private readonly Mock<IKitapRepository> _kitapRepoMock;
    //               ↑ Interface mock — gerçek DB yok, test hızlı
    //                 bunu yazmasaydık → gerçek AppDbContext kurardık, test yavaş + kırılgan

    private readonly KitapEkleHandler _handler;

    public KitapEkleHandlerTests()
    {
        _kitapRepoMock = new Mock<IKitapRepository>();
        _handler = new KitapEkleHandler(_kitapRepoMock.Object);
    }

    [Fact]
    public async Task Handle_GecerliKitap_KitapEklenir()
    {
        // Arrange
        var cmd = new KitapEkleCommand(
            Baslik: "Clean Code",
            Isbn: "9780132350884",
            Fiyat: 150,
            ParaBirimi: "TRY",
            IlkStok: 10);

        _kitapRepoMock
            .Setup(r => r.IsbnMevcutMu(It.IsAny<Isbn>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        //  ↑ "bu ISBN DB'de yok" → kaydetmeye devam et
        //    bunu yazmasaydık → mock null döner, NullReferenceException alırdık

        _kitapRepoMock
            .Setup(r => r.EkleAsync(It.IsAny<Kitap>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _kitapRepoMock
            .Setup(r => r.KaydetAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var sonuc = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        Assert.Equal("Clean Code", sonuc.Baslik);
        Assert.Equal("9780132350884", sonuc.Isbn);
        Assert.Equal(150, sonuc.Fiyat);
        Assert.Equal(10, sonuc.StokAdedi);

        _kitapRepoMock.Verify(
            r => r.EkleAsync(It.IsAny<Kitap>(), It.IsAny<CancellationToken>()),
            Times.Once);
        //  ↑ EkleAsync gerçekten bir kez çağrıldı mı?
        //    bunu yazmasaydık → handler çağırmadan testi geçebilirdi
    }

    [Fact]
    public async Task Handle_IsbnZatenKayitli_DomainExceptionFirlar()
    {
        // Arrange
        var cmd = new KitapEkleCommand("Clean Code", "9780132350884", 150, "TRY", 10);

        _kitapRepoMock
            .Setup(r => r.IsbnMevcutMu(It.IsAny<Isbn>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        //  ↑ "bu ISBN var" → DomainException bekliyoruz

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(cmd, CancellationToken.None));
        //  ↑ exception fırladı mı?

        _kitapRepoMock.Verify(
            r => r.EkleAsync(It.IsAny<Kitap>(), It.IsAny<CancellationToken>()),
            Times.Never);
        //  ↑ duplikat ISBN'de EkleAsync ÇAĞRILMAMALI
        //    bunu test etmeseydik → mock setup yanlış bile olsa test geçebilirdi
    }

    [Fact]
    public async Task Handle_GecersizIsbn_DomainExceptionFirlar()
    {
        // Arrange — ISBN format hatası: Isbn Value Object içinde patlar
        var cmd = new KitapEkleCommand("Clean Code", "YANLIS_ISBN", 150, "TRY", 10);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(cmd, CancellationToken.None));
        //  ↑ Isbn("YANLIS_ISBN") → DomainException: "Geçersiz ISBN"
        //    Handler'a ulaşmadan Domain kuralı devreye girdi

        _kitapRepoMock.Verify(
            r => r.IsbnMevcutMu(It.IsAny<Isbn>(), It.IsAny<CancellationToken>()),
            Times.Never);
        //  ↑ ISBN geçersizse DB'ye sorgu bile gitmemeli
    }

    [Fact]
    public async Task Handle_NegativeFiyat_DomainExceptionFirlar()
    {
        // Arrange
        var cmd = new KitapEkleCommand("Clean Code", "9780132350884", -10, "TRY", 5);
        //                                                               ↑ negatif fiyat

        _kitapRepoMock
            .Setup(r => r.IsbnMevcutMu(It.IsAny<Isbn>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(cmd, CancellationToken.None));
        //  ↑ new Fiyat(-10, "TRY") → DomainException: "Fiyat 0'dan büyük olmalı"
    }
}
