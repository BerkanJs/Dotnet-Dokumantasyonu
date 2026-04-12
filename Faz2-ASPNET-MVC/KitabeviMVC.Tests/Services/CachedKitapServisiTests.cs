using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KitabeviMVC.Tests.Services;

// ─────────────────────────────────────────────────────────────────────
// CachedKitapServisi Unit Testleri
//
// Strateji: IKitapServisi → Mock<IKitapServisi> (kaç kez çağrıldı?)
//           IMemoryCache  → gerçek MemoryCache   (state-based test)
//
// Neden IMemoryCache için gerçek implementasyon?
// _cache.Set() ve GetOrCreate() extension metoddur — Moq mock'layamaz.
// Gerçek MemoryCache kullanmak daha kısa, daha güvenilir, harici bağımlılık yok.
//
// Gün 29: IKitapServisi async'e çevrildi → tüm metodlar Task<T> döndürüyor.
// ReturnsAsync: Moq'un async Setup için karşılığı.
// Her test için yeni cache instance'ı → tam izolasyon.
// ─────────────────────────────────────────────────────────────────────
public class CachedKitapServisiTests
{
    // ─── Yardımcı fabrika metodu ──────────────────────────────────────

    private static (CachedKitapServisi cachedServis, Mock<IKitapServisi> mockGercekServis, IMemoryCache cache)
        OlusturServis()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var mockGercekServis = new Mock<IKitapServisi>();
        var logger = NullLogger<CachedKitapServisi>.Instance;

        var cachedServis = new CachedKitapServisi(
            mockGercekServis.Object,
            cache,
            logger);

        return (cachedServis, mockGercekServis, cache);
    }

    private static List<KitapListeViewModel> OrnekListe() =>
    [
        new(1, "1984", "Orwell", 75, "Roman", 8),
        new(2, "Sapiens", "Harari", 140, "Tarih", 20)
    ];

    // ─── HepsiniGetirAsync — Cache Miss ──────────────────────────────

    [Fact]
    public async Task HepsiniGetirAsync_CacheMiss_GercekServisiBirKezCagirir()
    {
        // Arrange
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis.Setup(s => s.HepsiniGetirAsync()).ReturnsAsync(OrnekListe());
        // ReturnsAsync: Moq'un async metodlar için Returns karşılığı.
        // Returns(Task.FromResult(...)) yazılabilir ama ReturnsAsync daha kısa.

        // Act — iki kez çağır
        await cachedServis.HepsiniGetirAsync();
        await cachedServis.HepsiniGetirAsync();

        // Assert — 2 çağrıya rağmen altta yatan servis sadece 1 kez çağrılmalı
        // İkinci çağrı cache'ten geldi
        mockGercekServis.Verify(s => s.HepsiniGetirAsync(), Times.Once);
        // Times.Once: tam olarak 1 kez çağrılmalı — cache varsa tekrar gitme.
    }

    [Fact]
    public async Task HepsiniGetirAsync_CacheMiss_DogruVeriDondurur()
    {
        // Arrange
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        var beklenen = OrnekListe();
        mockGercekServis.Setup(s => s.HepsiniGetirAsync()).ReturnsAsync(beklenen);

        // Act
        var sonuc = await cachedServis.HepsiniGetirAsync();

        // Assert
        sonuc.Should().BeEquivalentTo(beklenen);
    }

    [Fact]
    public async Task HepsiniGetirAsync_CacheMiss_SonucuCacheYazar()
    {
        // Arrange
        var (cachedServis, mockGercekServis, cache) = OlusturServis();
        mockGercekServis.Setup(s => s.HepsiniGetirAsync()).ReturnsAsync(OrnekListe());

        // Act
        await cachedServis.HepsiniGetirAsync();

        // Assert — cache'te anahtar var mı?
        cache.TryGetValue("kitaplar:hepsi", out _).Should().BeTrue();
    }

    // ─── HepsiniGetirAsync — Cache Hit ───────────────────────────────

    [Fact]
    public async Task HepsiniGetirAsync_CacheHit_GercekServisiHicCagirmaz()
    {
        // Arrange — önce cache'i doldur, sonra tekrar çağır
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis.Setup(s => s.HepsiniGetirAsync()).ReturnsAsync(OrnekListe());

        await cachedServis.HepsiniGetirAsync(); // cache'i doldur (1. çağrı - miss)
        mockGercekServis.Invocations.Clear(); // sayacı sıfırla

        // Act — 2. çağrı cache hit olmalı
        await cachedServis.HepsiniGetirAsync();

        // Assert — servis bir daha çağrılmadı
        mockGercekServis.Verify(s => s.HepsiniGetirAsync(), Times.Never);
        // Times.Never: cache hit → gerçek servis çağrılmamalı.
    }

    // ─── KategoriyeGoreGetirAsync ─────────────────────────────────────

    [Fact]
    public async Task KategoriyeGoreGetirAsync_AyniKategori_IkinciCagridaCacheHit()
    {
        // Arrange
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis
            .Setup(s => s.KategoriyeGoreGetirAsync("Roman"))
            .ReturnsAsync([new(1, "1984", "Orwell", 75, "Roman", 8)]);

        // Act
        await cachedServis.KategoriyeGoreGetirAsync("Roman"); // miss
        await cachedServis.KategoriyeGoreGetirAsync("Roman"); // hit

        // Assert
        mockGercekServis.Verify(s => s.KategoriyeGoreGetirAsync("Roman"), Times.Once);
    }

    [Fact]
    public async Task KategoriyeGoreGetirAsync_FarkliKategoriler_AyriCacheGirdileriOlusturur()
    {
        // "Roman" ve "Tarih" birbirinin cache'ini etkilemez
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis.Setup(s => s.KategoriyeGoreGetirAsync("Roman")).ReturnsAsync([]);
        mockGercekServis.Setup(s => s.KategoriyeGoreGetirAsync("Tarih")).ReturnsAsync([]);

        await cachedServis.KategoriyeGoreGetirAsync("Roman");
        await cachedServis.KategoriyeGoreGetirAsync("Tarih");
        await cachedServis.KategoriyeGoreGetirAsync("Roman"); // hit
        await cachedServis.KategoriyeGoreGetirAsync("Tarih"); // hit

        // Her kategori DB'ye 1 kez gitmiş olmalı (2'şer değil)
        mockGercekServis.Verify(s => s.KategoriyeGoreGetirAsync("Roman"), Times.Once);
        mockGercekServis.Verify(s => s.KategoriyeGoreGetirAsync("Tarih"), Times.Once);
    }

    // ─── BulByIdAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task BulByIdAsync_AyniId_IkinciCagridaCacheHit()
    {
        // Arrange
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis
            .Setup(s => s.BulByIdAsync(1))
            .ReturnsAsync(new KitapFormViewModel { Id = 1, Baslik = "Test" });

        // Act
        await cachedServis.BulByIdAsync(1); // miss
        await cachedServis.BulByIdAsync(1); // hit

        // Assert
        mockGercekServis.Verify(s => s.BulByIdAsync(1), Times.Once);
    }

    [Fact]
    public async Task BulByIdAsync_YokOlanId_NegativeCacheUygulanir()
    {
        // Negative cache: null sonuç da cache'lenir.
        // Olmayan ID için tekrarlayan istekler DB'ye gitmez.
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis.Setup(s => s.BulByIdAsync(9999)).ReturnsAsync((KitapFormViewModel?)null);

        await cachedServis.BulByIdAsync(9999); // miss → null, cache'e yaz
        await cachedServis.BulByIdAsync(9999); // hit → cache'teki null gelir

        // DB'ye sadece 1 kez gidildi
        mockGercekServis.Verify(s => s.BulByIdAsync(9999), Times.Once);
    }

    // ─── EkleAsync — Cache Invalidation ──────────────────────────────

    [Fact]
    public async Task EkleAsync_SonrasindaTumKitaplarCacheSiliniyor()
    {
        // Arrange — önce liste cache'ini doldur
        var (cachedServis, mockGercekServis, cache) = OlusturServis();
        mockGercekServis.Setup(s => s.HepsiniGetirAsync()).ReturnsAsync(OrnekListe());
        mockGercekServis.Setup(s => s.EkleAsync(It.IsAny<KitapFormViewModel>())).ReturnsAsync(6);

        await cachedServis.HepsiniGetirAsync(); // cache'i doldur
        cache.TryGetValue("kitaplar:hepsi", out _).Should().BeTrue(); // doldu mu kontrol

        // Act
        await cachedServis.EkleAsync(new KitapFormViewModel { Baslik = "Yeni", Yazar = "Y", Kategori = "Roman" });

        // Assert — "kitaplar:hepsi" cache'ten silindi
        cache.TryGetValue("kitaplar:hepsi", out _).Should().BeFalse();
    }

    [Fact]
    public async Task EkleAsync_SonrasindaKategoriCacheSiliniyor()
    {
        // Arrange
        var (cachedServis, mockGercekServis, cache) = OlusturServis();
        mockGercekServis.Setup(s => s.KategoriyeGoreGetirAsync("Roman")).ReturnsAsync([]);
        mockGercekServis.Setup(s => s.EkleAsync(It.IsAny<KitapFormViewModel>())).ReturnsAsync(6);

        await cachedServis.KategoriyeGoreGetirAsync("Roman"); // cache'i doldur
        cache.TryGetValue("kitaplar:kategori:roman", out _).Should().BeTrue();

        // Act — Roman kategorisinde yeni kitap ekle
        await cachedServis.EkleAsync(new KitapFormViewModel { Baslik = "Yeni", Yazar = "Y", Kategori = "Roman" });

        // Assert — Roman kategori cache'i temizlendi
        cache.TryGetValue("kitaplar:kategori:roman", out _).Should().BeFalse();
    }

    [Fact]
    public async Task EkleAsync_SonrasindaCacheMissOlur_DBdenTazeVeriAlir()
    {
        // Ekle sonrası cache temizlenince bir sonraki okuma DB'ye gider
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis.Setup(s => s.HepsiniGetirAsync()).ReturnsAsync(OrnekListe());
        mockGercekServis.Setup(s => s.EkleAsync(It.IsAny<KitapFormViewModel>())).ReturnsAsync(6);

        await cachedServis.HepsiniGetirAsync();                                                  // 1. çağrı: miss
        await cachedServis.EkleAsync(new KitapFormViewModel { Kategori = "Roman" });             // cache'i sil
        await cachedServis.HepsiniGetirAsync();                                                  // 2. çağrı: miss (cache temizlendi)

        // DB'ye 2 kez gidilmeli (ekleme araya girdi ve cache'i bozdu)
        mockGercekServis.Verify(s => s.HepsiniGetirAsync(), Times.Exactly(2));
    }

    // ─── GuncelleAsync — Cache Invalidation ──────────────────────────

    [Fact]
    public async Task GuncelleAsync_SonrasindaBireyselKitapCacheSiliniyor()
    {
        // Arrange
        var (cachedServis, mockGercekServis, cache) = OlusturServis();
        var model = new KitapFormViewModel { Id = 1, Baslik = "Test", Kategori = "Roman" };
        mockGercekServis.Setup(s => s.BulByIdAsync(1)).ReturnsAsync(model);
        mockGercekServis.Setup(s => s.GuncelleAsync(It.IsAny<KitapFormViewModel>())).ReturnsAsync(true);

        await cachedServis.BulByIdAsync(1); // cache'i doldur
        cache.TryGetValue("kitap:1", out _).Should().BeTrue();

        // Act
        await cachedServis.GuncelleAsync(model);

        // Assert — bireysel kitap cache'i temizlendi
        cache.TryGetValue("kitap:1", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GuncelleAsync_BasarisizOlursa_CacheDokunulmaz()
    {
        // GuncelleAsync false dönerse (kayıt yok) — cache'e dokunulmamalı
        var (cachedServis, mockGercekServis, cache) = OlusturServis();
        mockGercekServis.Setup(s => s.HepsiniGetirAsync()).ReturnsAsync(OrnekListe());
        mockGercekServis.Setup(s => s.GuncelleAsync(It.IsAny<KitapFormViewModel>())).ReturnsAsync(false);

        await cachedServis.HepsiniGetirAsync(); // cache'i doldur

        // Act
        await cachedServis.GuncelleAsync(new KitapFormViewModel { Id = 9999, Kategori = "Roman" });

        // Assert — başarısız güncelleme cache'i bozmadı
        cache.TryGetValue("kitaplar:hepsi", out _).Should().BeTrue();
    }

    // ─── BaslikVarMiAsync — Hiç Cache'lenmez ─────────────────────────

    [Fact]
    public async Task BaslikVarMiAsync_HerZamanGercekServiseDelege_Eder()
    {
        // BaslikVarMiAsync veri bütünlüğü kontrolü — anlık tutarlılık zorunlu.
        // Her çağrı DB'ye gitmelidir.
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis.Setup(s => s.BaslikVarMiAsync("1984", 0)).ReturnsAsync(true);

        // Act — 3 kez çağır
        await cachedServis.BaslikVarMiAsync("1984");
        await cachedServis.BaslikVarMiAsync("1984");
        await cachedServis.BaslikVarMiAsync("1984");

        // Assert — her çağrı DB'ye gitmiş (cache yok)
        mockGercekServis.Verify(s => s.BaslikVarMiAsync("1984", 0), Times.Exactly(3));
    }

    [Fact]
    public async Task BaslikVarMiAsync_CacheHicYazilmaz()
    {
        // Arrange
        var (cachedServis, mockGercekServis, cache) = OlusturServis();
        mockGercekServis.Setup(s => s.BaslikVarMiAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);

        // Act
        await cachedServis.BaslikVarMiAsync("1984");

        // Assert — cache hâlâ boş
        cache.TryGetValue("kitaplar:hepsi", out _).Should().BeFalse();
        cache.TryGetValue("kitap:1", out _).Should().BeFalse();
    }
}
