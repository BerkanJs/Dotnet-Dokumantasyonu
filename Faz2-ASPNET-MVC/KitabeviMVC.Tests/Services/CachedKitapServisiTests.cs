using FluentAssertions;
using KitabeviMVC.Models.ViewModels;
using KitabeviMVC.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

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

    // ─── HepsiniGetir — Cache Miss ────────────────────────────────────

    [Fact]
    public void HepsiniGetir_CacheMiss_GercekServisiBirKezCagirir()
    {
        // Arrange
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis.Setup(s => s.HepsiniGetir()).Returns(OrnekListe());

        // Act — iki kez çağır
        cachedServis.HepsiniGetir();
        cachedServis.HepsiniGetir();

        // Assert — 2 çağrıya rağmen altta yatan servis sadece 1 kez çağrılmalı
        // İkinci çağrı cache'ten geldi
        mockGercekServis.Verify(s => s.HepsiniGetir(), Times.Once);
    }

    [Fact]
    public void HepsiniGetir_CacheMiss_DogruVeriDondurur()
    {
        // Arrange
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        var beklenen = OrnekListe();
        mockGercekServis.Setup(s => s.HepsiniGetir()).Returns(beklenen);

        // Act
        var sonuc = cachedServis.HepsiniGetir();

        // Assert
        sonuc.Should().BeEquivalentTo(beklenen);
    }

    [Fact]
    public void HepsiniGetir_CacheMiss_SonucuCacheYazar()
    {
        // Arrange
        var (cachedServis, mockGercekServis, cache) = OlusturServis();
        mockGercekServis.Setup(s => s.HepsiniGetir()).Returns(OrnekListe());

        // Act
        cachedServis.HepsiniGetir();

        // Assert — cache'te anahtar var mı?
        cache.TryGetValue("kitaplar:hepsi", out _).Should().BeTrue();
    }

    // ─── HepsiniGetir — Cache Hit ─────────────────────────────────────

    [Fact]
    public void HepsiniGetir_CacheHit_GercekServisiHicCagirmaz()
    {
        // Arrange — önce cache'i doldur, sonra tekrar çağır
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis.Setup(s => s.HepsiniGetir()).Returns(OrnekListe());

        cachedServis.HepsiniGetir(); // cache'i doldur (1. çağrı - miss)
        mockGercekServis.Invocations.Clear(); // sayacı sıfırla

        // Act — 2. çağrı cache hit olmalı
        cachedServis.HepsiniGetir();

        // Assert — servis bir daha çağrılmadı
        mockGercekServis.Verify(s => s.HepsiniGetir(), Times.Never);
    }

    // ─── KategoriyeGoreGetir ──────────────────────────────────────────

    [Fact]
    public void KategoriyeGoreGetir_AyniKategori_IkinciCagridaCacheHit()
    {
        // Arrange
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis
            .Setup(s => s.KategoriyeGoreGetir("Roman"))
            .Returns([new(1, "1984", "Orwell", 75, "Roman", 8)]);

        // Act
        cachedServis.KategoriyeGoreGetir("Roman"); // miss
        cachedServis.KategoriyeGoreGetir("Roman"); // hit

        // Assert
        mockGercekServis.Verify(s => s.KategoriyeGoreGetir("Roman"), Times.Once);
    }

    [Fact]
    public void KategoriyeGoreGetir_FarkliKategoriler_AyriCacheGirdileriOlusturur()
    {
        // "Roman" ve "Tarih" birbirinin cache'ini etkilemez
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis.Setup(s => s.KategoriyeGoreGetir("Roman")).Returns([]);
        mockGercekServis.Setup(s => s.KategoriyeGoreGetir("Tarih")).Returns([]);

        cachedServis.KategoriyeGoreGetir("Roman");
        cachedServis.KategoriyeGoreGetir("Tarih");
        cachedServis.KategoriyeGoreGetir("Roman"); // hit
        cachedServis.KategoriyeGoreGetir("Tarih"); // hit

        // Her kategori DB'ye 1 kez gitmiş olmalı (2'şer değil)
        mockGercekServis.Verify(s => s.KategoriyeGoreGetir("Roman"), Times.Once);
        mockGercekServis.Verify(s => s.KategoriyeGoreGetir("Tarih"), Times.Once);
    }

    // ─── BulById ──────────────────────────────────────────────────────

    [Fact]
    public void BulById_AyniId_IkinciCagridaCacheHit()
    {
        // Arrange
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis
            .Setup(s => s.BulById(1))
            .Returns(new KitapFormViewModel { Id = 1, Baslik = "Test" });

        // Act
        cachedServis.BulById(1); // miss
        cachedServis.BulById(1); // hit

        // Assert
        mockGercekServis.Verify(s => s.BulById(1), Times.Once);
    }

    [Fact]
    public void BulById_YokOlanId_NegativeCacheUygulanir()
    {
        // Negative cache: null sonuç da cache'lenir.
        // Olmayan ID için tekrarlayan istekler DB'ye gitmez.
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis.Setup(s => s.BulById(9999)).Returns((KitapFormViewModel?)null);

        cachedServis.BulById(9999); // miss → null, cache'e yaz
        cachedServis.BulById(9999); // hit → cache'teki null gelir

        // DB'ye sadece 1 kez gidildi
        mockGercekServis.Verify(s => s.BulById(9999), Times.Once);
    }

    // ─── Ekle — Cache Invalidation ────────────────────────────────────

    [Fact]
    public void Ekle_SonrasindaTumKitaplarCacheSiliniyor()
    {
        // Arrange — önce liste cache'ini doldur
        var (cachedServis, mockGercekServis, cache) = OlusturServis();
        mockGercekServis.Setup(s => s.HepsiniGetir()).Returns(OrnekListe());
        mockGercekServis.Setup(s => s.Ekle(It.IsAny<KitapFormViewModel>())).Returns(6);

        cachedServis.HepsiniGetir(); // cache'i doldur
        cache.TryGetValue("kitaplar:hepsi", out _).Should().BeTrue(); // doldu mu kontrol

        // Act
        cachedServis.Ekle(new KitapFormViewModel { Baslik = "Yeni", Yazar = "Y", Kategori = "Roman" });

        // Assert — "kitaplar:hepsi" cache'ten silindi
        cache.TryGetValue("kitaplar:hepsi", out _).Should().BeFalse();
    }

    [Fact]
    public void Ekle_SonrasindaKategoriCacheSiliniyor()
    {
        // Arrange
        var (cachedServis, mockGercekServis, cache) = OlusturServis();
        mockGercekServis.Setup(s => s.KategoriyeGoreGetir("Roman")).Returns([]);
        mockGercekServis.Setup(s => s.Ekle(It.IsAny<KitapFormViewModel>())).Returns(6);

        cachedServis.KategoriyeGoreGetir("Roman"); // cache'i doldur
        cache.TryGetValue("kitaplar:kategori:roman", out _).Should().BeTrue();

        // Act — Roman kategorisinde yeni kitap ekle
        cachedServis.Ekle(new KitapFormViewModel { Baslik = "Yeni", Yazar = "Y", Kategori = "Roman" });

        // Assert — Roman kategori cache'i temizlendi
        cache.TryGetValue("kitaplar:kategori:roman", out _).Should().BeFalse();
    }

    [Fact]
    public void Ekle_SonrasindaCacheMissOlur_DBdenTazeVeriAlir()
    {
        // Ekle sonrası cache temizlenince bir sonraki okuma DB'ye gider
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis.Setup(s => s.HepsiniGetir()).Returns(OrnekListe());
        mockGercekServis.Setup(s => s.Ekle(It.IsAny<KitapFormViewModel>())).Returns(6);

        cachedServis.HepsiniGetir();                                    // 1. çağrı: miss
        cachedServis.Ekle(new KitapFormViewModel { Kategori = "Roman" }); // cache'i sil
        cachedServis.HepsiniGetir();                                    // 2. çağrı: miss (cache temizlendi)

        // DB'ye 2 kez gidilmeli (ekleme araya girdi ve cache'i bozdu)
        mockGercekServis.Verify(s => s.HepsiniGetir(), Times.Exactly(2));
    }

    // ─── Guncelle — Cache Invalidation ───────────────────────────────

    [Fact]
    public void Guncelle_SonrasindaBireyselKitapCacheSiliniyor()
    {
        // Arrange
        var (cachedServis, mockGercekServis, cache) = OlusturServis();
        var model = new KitapFormViewModel { Id = 1, Baslik = "Test", Kategori = "Roman" };
        mockGercekServis.Setup(s => s.BulById(1)).Returns(model);
        mockGercekServis.Setup(s => s.Guncelle(It.IsAny<KitapFormViewModel>())).Returns(true);

        cachedServis.BulById(1); // cache'i doldur
        cache.TryGetValue("kitap:1", out _).Should().BeTrue();

        // Act
        cachedServis.Guncelle(model);

        // Assert — bireysel kitap cache'i temizlendi
        cache.TryGetValue("kitap:1", out _).Should().BeFalse();
    }

    [Fact]
    public void Guncelle_BasarisizOlursa_CacheDokunulmaz()
    {
        // Guncelle false dönerse (kayıt yok) — cache'e dokunulmamalı
        var (cachedServis, mockGercekServis, cache) = OlusturServis();
        mockGercekServis.Setup(s => s.HepsiniGetir()).Returns(OrnekListe());
        mockGercekServis.Setup(s => s.Guncelle(It.IsAny<KitapFormViewModel>())).Returns(false);

        cachedServis.HepsiniGetir(); // cache'i doldur

        // Act
        cachedServis.Guncelle(new KitapFormViewModel { Id = 9999, Kategori = "Roman" });

        // Assert — başarısız güncelleme cache'i bozmadı
        cache.TryGetValue("kitaplar:hepsi", out _).Should().BeTrue();
    }

    // ─── BaslikVarMi — Hiç Cache'lenmez ──────────────────────────────

    [Fact]
    public void BaslikVarMi_HerZamanGercekServiseDelege_Eder()
    {
        // BaslikVarMi veri bütünlüğü kontrolü — anlık tutarlılık zorunlu.
        // Her çağrı DB'ye gitmelidir.
        var (cachedServis, mockGercekServis, _) = OlusturServis();
        mockGercekServis.Setup(s => s.BaslikVarMi("1984", 0)).Returns(true);

        // Act — 3 kez çağır
        cachedServis.BaslikVarMi("1984");
        cachedServis.BaslikVarMi("1984");
        cachedServis.BaslikVarMi("1984");

        // Assert — her çağrı DB'ye gitmiş (cache yok)
        mockGercekServis.Verify(s => s.BaslikVarMi("1984", 0), Times.Exactly(3));
    }

    [Fact]
    public void BaslikVarMi_CacheHicYazilmaz()
    {
        // Arrange
        var (cachedServis, mockGercekServis, cache) = OlusturServis();
        mockGercekServis.Setup(s => s.BaslikVarMi(It.IsAny<string>(), It.IsAny<int>())).Returns(true);

        // Act
        cachedServis.BaslikVarMi("1984");

        // Assert — cache hâlâ boş
        cache.TryGetValue("kitaplar:hepsi", out _).Should().BeFalse();
        cache.TryGetValue("kitap:1", out _).Should().BeFalse();
    }
}
