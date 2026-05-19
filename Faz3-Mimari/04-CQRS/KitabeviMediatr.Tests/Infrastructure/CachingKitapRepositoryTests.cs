using KitabeviMediatr.Domain.Entities;
using KitabeviMediatr.Domain.Interfaces;
using KitabeviMediatr.Domain.ValueObjects;
using KitabeviMediatr.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace KitabeviMediatr.Tests.Infrastructure;

public class CachingKitapRepositoryTests
{
    private readonly Mock<IKitapRepository> _innerMock;
    private readonly IMemoryCache _cache;
    private readonly CachingKitapRepository _cachingRepo;

    public CachingKitapRepositoryTests()
    {
        _innerMock = new Mock<IKitapRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        //        ↑ gerçek MemoryCache — mock değil
        //          bunu mock'lasaydık → Set/Get davranışı gerçeği yansıtmazdı,
        //          cache çalışıyor mu çalışmıyor mu test edemezdik

        _cachingRepo = new CachingKitapRepository(_innerMock.Object, _cache);
    }

    [Fact]
    public async Task TumunuGetir_IlkCagri_DbdenAlir()
    {
        // Arrange
        var beklenen = new List<Kitap>
        {
            new("Clean Code", new Isbn("9780132350884"), new Fiyat(150), 10)
        }.AsReadOnly();

        _innerMock
            .Setup(r => r.TumunuGetirAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(beklenen);

        // Act
        var sonuc = await _cachingRepo.TumunuGetirAsync();

        // Assert
        Assert.Equal(beklenen, sonuc);

        _innerMock.Verify(
            r => r.TumunuGetirAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        //  ↑ ilk çağrıda DB'ye gitti
    }

    [Fact]
    public async Task TumunuGetir_IkinciCagri_CachedenAlir()
    {
        // Arrange
        var beklenen = new List<Kitap>
        {
            new("Clean Code", new Isbn("9780132350884"), new Fiyat(150), 10)
        }.AsReadOnly();

        _innerMock
            .Setup(r => r.TumunuGetirAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(beklenen);

        // Act — iki kez çağır
        await _cachingRepo.TumunuGetirAsync();
        await _cachingRepo.TumunuGetirAsync();

        // Assert — DB sadece bir kez çağrıldı
        _innerMock.Verify(
            r => r.TumunuGetirAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        //  ↑ ikinci çağrıda cache'den geldi — DB çağrısı yok
        //    bunu test etmeseydik → cache çalışıp çalışmadığından emin olamazdık
    }

    [Fact]
    public async Task KaydetAsync_SonrasiCache_Temizlenir()
    {
        // Arrange — önce cache'i doldur
        var beklenen = new List<Kitap>().AsReadOnly();

        _innerMock
            .Setup(r => r.TumunuGetirAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(beklenen);

        _innerMock
            .Setup(r => r.KaydetAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _cachingRepo.TumunuGetirAsync(); // cache doldu

        // Act — kaydet → cache temizlenmeli
        await _cachingRepo.KaydetAsync();

        // Assert — bir sonraki TumunuGetir DB'den gitmeli
        await _cachingRepo.TumunuGetirAsync();

        _innerMock.Verify(
            r => r.TumunuGetirAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        //  ↑ KaydetAsync sonrası cache temizlendi → ikinci DB çağrısı yapıldı
        //    bunu test etmeseydik → stale cache davranışını yakalayamazdık
    }
}
