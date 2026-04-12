using KitabeviMVC.Models.Entities;

namespace KitabeviMVC.Tests.Domain;

// ─────────────────────────────────────────────────────────────────────────────
// FluentAssertions kapsamlı örnek — Kitap entity üzerinde tüm assertion türleri
//
// Bu dosya gün 40'ın referans dosyasıdır:
//   - Primitive / string assertion'lar
//   - Koleksiyon assertion'lar
//   - BeEquivalentTo (derin nesne karşılaştırma)
//   - Nullable assertion
//   - Exception assertion
// ─────────────────────────────────────────────────────────────────────────────
public class KitapFluentTests
{
    // ─── 1. Primitive ve string assertion'ları ────────────────────────────────

    [Fact]
    public void Kitap_Fiyat_BeklenenAralikta()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var kitap = new Kitap { Baslik = "Sapiens", Fiyat = 140m, StokAdedi = 20 };

        // ─── Assert ──────────────────────────────────────────────────────────
        kitap.Fiyat.Should().BeGreaterThan(0m,
            because: "Negatif veya sıfır fiyatlı kitap satışa çıkarılamaz.");
        kitap.Fiyat.Should().BeLessThanOrEqualTo(10_000m,
            because: "Makul üst sınır kontrolü.");
        kitap.Fiyat.Should().BeInRange(1m, 10_000m);
        // BeInRange: BeGreaterThan + BeLessThanOrEqualTo tek satırda.

        kitap.StokAdedi.Should().BeGreaterThanOrEqualTo(0,
            because: "Stok adedi negatif olamaz.");
    }

    [Fact]
    public void Kitap_Baslik_StringAssertionlar()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var kitap = new Kitap { Baslik = "Clean Code: A Handbook" };

        // ─── Assert ──────────────────────────────────────────────────────────
        kitap.Baslik.Should().NotBeNullOrEmpty(
            because: "Başlıksız kitap sistemde olmamalı.");
        kitap.Baslik.Should().StartWith("Clean",
            because: "Bu test verisi için sabit kontrol.");
        kitap.Baslik.Should().Contain("Code",
            because: "'Code' kelimesi başlıkta geçmeli.");
        kitap.Baslik.Length.Should().BeGreaterThan(5,
            because: "Çok kısa başlıklar genellikle hatalı veri.");
        // HaveLengthGreaterThan FA'da yok — .Length.Should().BeGreaterThan ile eşdeğer.
        kitap.Baslik.Should().NotStartWith(" ",
            because: "Başında boşluk olmamalı — kullanıcıdan gelen girdi normalize edilmeli.");
    }

    // ─── 2. Koleksiyon assertion'ları ─────────────────────────────────────────

    [Fact]
    public void KitapListesi_StokluKitaplar_DogruFiltre()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var kitaplar = new List<Kitap>
        {
            new() { Id = 1, Baslik = "A", Fiyat = 10m, StokAdedi = 5,  Kategori = "Roman" },
            new() { Id = 2, Baslik = "B", Fiyat = 20m, StokAdedi = 0,  Kategori = "Roman" },
            new() { Id = 3, Baslik = "C", Fiyat = 30m, StokAdedi = 12, Kategori = "Tarih" },
        };

        var stoklu = kitaplar.Where(k => k.StokAdedi > 0).ToList();

        // ─── Assert ──────────────────────────────────────────────────────────
        stoklu.Should().HaveCount(2);
        // HaveCount: sayıyı doğrular; başarısız mesajda tüm koleksiyon içeriği görünür.

        stoklu.Should().OnlyContain(k => k.StokAdedi > 0,
            because: "Stoksuz kitap bu listede bulunmamalı.");
        // OnlyContain: TÜM elemanlar bu koşulu sağlamalı.

        stoklu.Should().NotContain(k => k.Id == 2,
            because: "Stoksuz kitap (Id=2) filtrelendi.");

        stoklu.Should().Contain(k => k.Kategori == "Tarih",
            because: "Tarih kategorisindeki stoklu kitap listede olmalı.");

        stoklu.Should().ContainSingle(k => k.Kategori == "Tarih",
            because: "Tarih kategorisinden tam bir stoklu kitap var.");
        // ContainSingle: HaveCount(1) + Contain birleşimi, tek satırda daha okunabilir.
    }

    [Fact]
    public void KitapListesi_Zincirleme_Assertion()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var kitaplar = new List<Kitap>
        {
            new() { Id = 1, Baslik = "Arı Maya", Fiyat = 15m, StokAdedi = 3 },
            new() { Id = 2, Baslik = "Bambi",    Fiyat = 18m, StokAdedi = 7 },
            new() { Id = 3, Baslik = "Cüceler",  Fiyat = 22m, StokAdedi = 1 },
        };

        // ─── Assert ──────────────────────────────────────────────────────────
        kitaplar.Should()
            .HaveCount(3)
            .And.BeInAscendingOrder(k => k.Baslik)
            .And.OnlyContain(k => k.Fiyat > 0 && k.StokAdedi > 0);
        // .And: zincirleme — önceki assertion geçtiyse devam et.
        // Üç ayrı satır da yazılabilir; zincirleme okunabilirliği artırır.
    }

    [Fact]
    public void KitapListesi_Collection_ElemanSirali_Dogrula()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var beklenen = new[] { "Dede Korkut", "İnce Memed", "Mavi Sürgün" };
        var liste    = new List<Kitap>
        {
            new() { Baslik = "Dede Korkut" },
            new() { Baslik = "İnce Memed"  },
            new() { Baslik = "Mavi Sürgün" },
        };

        // ─── Assert ──────────────────────────────────────────────────────────
        liste.Should().SatisfyRespectively(
            // SatisfyRespectively: her eleman için sırayla ayrı assertion lambda.
            k1 => k1.Baslik.Should().Be("Dede Korkut"),
            k2 => k2.Baslik.Should().Be("İnce Memed"),
            k3 => k3.Baslik.Should().Be("Mavi Sürgün")
        );
        // Assert.Collection ile aynı amaç; FluentAssertions versiyonu.
    }

    // ─── 3. BeEquivalentTo — derin nesne karşılaştırma ───────────────────────

    [Fact]
    public void KitapDto_VeKitap_DegerEsitligi()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        // Senaryo: Entity → DTO dönüşümü doğru yapıldı mı?
        var kaynak = new Kitap
        {
            Id        = 42,
            Baslik    = "Refactoring",
            Yazar     = "Martin Fowler",
            Fiyat     = 110m,
            Kategori  = "Yazılım",
            StokAdedi = 6,
        };

        // Manuel mapping simülasyonu (gerçekte AutoMapper veya servis yapar)
        var beklenen = new
        {
            Id        = 42,
            Baslik    = "Refactoring",
            Yazar     = "Martin Fowler",
            Fiyat     = 110m,
            Kategori  = "Yazılım",
            StokAdedi = 6,
        };

        // ─── Assert ──────────────────────────────────────────────────────────
        kaynak.Should().BeEquivalentTo(beklenen,
            because: "Entity → DTO mapping'i tüm alanları doğru aktarmalı.");
        // BeEquivalentTo: referans eşitliği değil, özellik değeri eşitliği.
        // Assert.Equal(beklenen, kaynak): iki farklı tip → derleme hatası.
        // BeEquivalentTo anonim nesne ile de çalışır — DTO sınıfı oluşturmaya gerek yok.
    }

    [Fact]
    public void Kitap_EklemeTarihi_Haric_TumAlanlariEsit()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        // Senaryo: EklemeTarihi test anında atanıyor — sabit değerle karşılaştırılamaz.
        var kaydedilen = new Kitap
        {
            Id           = 1,
            Baslik       = "Yapay Zeka",
            Fiyat        = 95m,
            Kategori     = "Teknoloji",
            EklemeTarihi = DateTime.UtcNow, // test sırasında "şimdi" — tahmin edilemez
        };
        var beklenen = new Kitap
        {
            Id           = 1,
            Baslik       = "Yapay Zeka",
            Fiyat        = 95m,
            Kategori     = "Teknoloji",
            EklemeTarihi = DateTime.MinValue, // placeholder — karşılaştırmayacağız
        };

        // ─── Assert ──────────────────────────────────────────────────────────
        kaydedilen.Should().BeEquivalentTo(beklenen,
            opts => opts.Excluding(k => k.EklemeTarihi)
                        .Excluding(k => k.RowVersion)
                        .Excluding(k => k.YazarNavigation),
            because: "EklemeTarihi ve RowVersion DB tarafından set edilir, test edemeyiz.");
        // Excluding: bu özellik karşılaştırma dışı bırakılır.
        // Bunu yazmassaydık: EklemeTarihi farkından test başarısız olur — false negative.
    }

    // ─── 4. Nullable assertion ────────────────────────────────────────────────

    [Fact]
    public void Kitap_YazarId_NulllamaAssertion()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var yazarsizKitap = new Kitap { Baslik = "Anonim Eser" };
        // YazarId int? — varsayılan null.

        var yazarliKitap = new Kitap { Baslik = "İmzalı Kitap", YazarId = 5 };

        // ─── Assert ──────────────────────────────────────────────────────────
        yazarsizKitap.YazarId.Should().BeNull(
            because: "Anonim kitapların YazarId'si null olabilir.");

        yazarliKitap.YazarId.Should().NotBeNull();
        yazarliKitap.YazarId.Should().Be(5);
        yazarliKitap.YazarNavigation.Should().BeNull(
            because: "Navigation property Include() yapılmadan null kalır — lazy loading yok.");
    }

    // ─── 5. Exception assertion ────────────────────────────────────────────────

    [Fact]
    public void KoleksizonIslem_IndexDisiErisim_ExceptionFirlatir()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var liste = new List<Kitap>();

        // ─── Act + Assert ────────────────────────────────────────────────────
        Action erisim = () => { var _ = liste[0]; };

        erisim.Should().Throw<ArgumentOutOfRangeException>(
            because: "Boş listede index erişimi ArgumentOutOfRangeException fırlatmalı.")
            .WithMessage("*index*", because: "Hata mesajı 'index' kelimesini içermeli.");
        // WithMessage wildcard (*): mesajın herhangi bir yerinde "index" geçmeli.
        // Tam mesaj yazılsaydı: yerelleştirme farkları testten geçemezdi.
    }

    [Fact]
    public void BoslukKontrol_NullKitap_ExceptionFirlatmamali()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        // Senaryo: Null koruma check — exception fırlatmamalı.
        Kitap? kitap = null;

        // ─── Act + Assert ────────────────────────────────────────────────────
        Action null_kontrol = () =>
        {
            if (kitap is not null)
            {
                var _ = kitap.Baslik.Length;
            }
        };

        null_kontrol.Should().NotThrow(
            because: "Null guard sayesinde null kitap exception'a yol açmamalı.");
    }
}
