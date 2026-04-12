using KitabeviMVC.Models.Entities;

namespace KitabeviMVC.Tests.Domain;

// ─────────────────────────────────────────────────────────────────────────────
// TDD Döngüsü Demonstrasyonu
//
// Bu dosya TDD'nin Red → Green → Refactor döngüsünü belgeler.
// KitapStokServisi: stok düşürme ve indirim hesaplama mantığı.
//
// TDD adımları bu dosyada yorum olarak işaretlenmiştir:
//   // [RED]    → Test yazıldı, henüz kod yok, derleme hatası
//   // [GREEN]  → Minimum kod yazıldı, test geçiyor
//   // [REFACTOR] → Kod temizlendi, test hâlâ geçiyor
// ─────────────────────────────────────────────────────────────────────────────
public class TDDTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // İç sınıf: KitapStokServisi
    // Production'da KitabeviMVC.Services.KitapStokServisi olurdu.
    // Bu dosyada iç sınıf olarak tutuyoruz: test projesine kalmak şartıyla izolasyon.
    // ─────────────────────────────────────────────────────────────────────────
    private sealed class KitapStokServisi
    // sealed: bu sınıftan miras alınamaz — Handler tasarımı gibi kasıtlı kısıtlama.
    {
        /// <summary>
        /// Stok adedini düşürür.
        /// İş kuralları:
        ///   1. Adet sıfır veya negatif olamaz.
        ///   2. Mevcut stok düşülecek adetten az olamaz.
        /// </summary>
        public void StokDus(Kitap kitap, int adet)
        {
            if (adet <= 0)
                throw new ArgumentException(
                    "Düşülecek adet sıfırdan büyük olmalı.", nameof(adet));
            // Guard clause: geçersiz girdi hemen reddedilir.
            // Bunu yazmassaydık: -5 adet düşünce stok 5 artardı — sessiz bug.

            if (kitap.StokAdedi < adet)
                throw new InvalidOperationException(
                    $"Yetersiz stok. Mevcut: {kitap.StokAdedi}, İstenen: {adet}");
            // Domain kuralı: stok eksi gidemez.
            // Bunu yazmassaydık: StokAdedi negatif olurdu; sipariş verilmeye devam eder.

            kitap.StokAdedi -= adet;
            // Başarılı düşüm: mevcut stoktan adet çıkarılır.
        }

        /// <summary>
        /// Fiyata indirim uygular ve yeni fiyatı döndürür.
        /// İş kuralları:
        ///   1. İndirim oranı 0 ile 100 arasında olmalı (dahil değil üst sınır).
        ///   2. Negatif indirim kabul edilmez.
        /// </summary>
        public decimal IndirimliFiyatHesapla(decimal fiyat, decimal yuzde)
        {
            if (yuzde < 0)
                throw new ArgumentException(
                    "İndirim oranı negatif olamaz.", nameof(yuzde));

            if (yuzde >= 100)
                throw new ArgumentException(
                    "İndirim oranı %100 veya üstü olamaz — kitap bedava verilemez.", nameof(yuzde));

            return Math.Round(fiyat * (1 - yuzde / 100m), 2);
            // Math.Round(..., 2): 2 ondalık basamak — para birimi için standart.
            // (1 - yuzde / 100m): yüzde → oran dönüşümü; 20% → 0.80 (katsayı).
            // "m" suffix: decimal literal — double ile karışmayı önler.
        }
    }

    // =========================================================================
    // StokDus Testleri — TDD döngüsü ile yazıldı
    // =========================================================================

    // [RED → GREEN: Döngü 1]
    [Fact]
    public void StokDus_YeterlıStok_StokAdediDuser()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        // RED: Bu test önce yazıldı. StokDus metodu yoktu → derleme hatası.
        // GREEN: StokDus metodu yazıldı, minimum kod: kitap.StokAdedi -= adet;
        // REFACTOR: Guard clause'lar eklendi; test hâlâ geçiyor.
        var kitap   = new Kitap { Baslik = "Test", Fiyat = 50m, StokAdedi = 10 };
        var servis  = new KitapStokServisi();

        // ─── Act ─────────────────────────────────────────────────────────────
        servis.StokDus(kitap, 3);

        // ─── Assert ──────────────────────────────────────────────────────────
        kitap.StokAdedi.Should().Be(7,
            because: "10 - 3 = 7 olmalı.");
    }

    // [RED → GREEN: Döngü 2 — hata senaryosu]
    [Fact]
    public void StokDus_YetersizStok_InvalidOperationFirlatir()
    {
        // RED: "Stok 2 iken 5 düşürmeye çalışırsa ne olur?" — henüz davranış yok.
        // GREEN: if (kitap.StokAdedi < adet) throw new InvalidOperationException(...)
        // REFACTOR: hata mesajı mevcut ve istenen adedi içerecek şekilde zenginleştirildi.
        var kitap  = new Kitap { StokAdedi = 2 };
        var servis = new KitapStokServisi();

        Action stokDus = () => servis.StokDus(kitap, 5);

        stokDus.Should()
            .Throw<InvalidOperationException>(because: "Stok yetersizse exception fırlatmalı.")
            .WithMessage("*Yetersiz stok*");
        // "*Yetersiz stok*": wildcard — mesajın herhangi bir yerinde bu metin geçmeli.
        // Tam mesaj eşleşmesi: dil/format değişirse test kırılır — kırılgan test.
    }

    // [RED → GREEN: Döngü 3 — edge case]
    [Fact]
    public void StokDus_NegatifAdet_ArgumentExceptionFirlatir()
    {
        // Edge case: negatif adet geçilirse ne olur?
        // Bu senaryoyu unit testle keşfettik — integration test geç yakalar.
        var kitap  = new Kitap { StokAdedi = 10 };
        var servis = new KitapStokServisi();

        Action stokDus = () => servis.StokDus(kitap, -3);

        stokDus.Should()
            .Throw<ArgumentException>()
            .And.ParamName.Should().Be("adet");
        // ParamName: ArgumentException'ın hangi parametreden geldiğini doğrular.
        // Sadece Throw<ArgumentException>() yazmak yeterli; ParamName ek güven.
    }

    [Fact]
    public void StokDus_SifirAdet_ArgumentExceptionFirlatir()
    {
        // Boundary test: sıfır adet "düşürme" anlamsız — iş kuralı: adet > 0.
        var kitap  = new Kitap { StokAdedi = 10 };
        var servis = new KitapStokServisi();

        Action stokDus = () => servis.StokDus(kitap, 0);

        stokDus.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StokDus_TamStokKadar_StokSifirOlur()
    {
        // Son stok adedi kadar düşülebilmeli — stok 0'a ulaşabilir.
        var kitap  = new Kitap { StokAdedi = 5 };
        var servis = new KitapStokServisi();

        servis.StokDus(kitap, 5);

        kitap.StokAdedi.Should().Be(0);
    }

    // =========================================================================
    // IndirimliFiyatHesapla Testleri — [Theory] ile parametrik
    // =========================================================================

    [Theory]
    [InlineData(100,   0,  100)]    // indirim yok
    [InlineData(100,  10,   90)]    // %10 indirim
    [InlineData(100,  25,   75)]    // %25 indirim
    [InlineData(100,  50,   50)]    // %50 indirim
    [InlineData(200,  20,  160)]    // farklı başlangıç fiyatı
    [InlineData(150,  33, 100.5)]   // kesirli sonuç → 2 ondalık
    public void IndirimliFiyat_DogruHesaplar(decimal fiyat, decimal yuzde, decimal beklenen)
    {
        // Her InlineData satırı bağımsız test çalıştırması.
        // [Fact] ile yazsaydık: 6 ayrı test method — kod tekrarı.
        var servis = new KitapStokServisi();

        var sonuc = servis.IndirimliFiyatHesapla(fiyat, yuzde);

        sonuc.Should().Be(beklenen,
            because: $"{fiyat} * (1 - {yuzde}/100) = {beklenen} olmalı.");
    }

    [Theory]
    [InlineData( -1)]    // negatif — kesinlikle geçersiz
    [InlineData(-50)]    // çok negatif
    public void IndirimliFiyat_NegatifYuzde_ArgumentExceptionFirlatir(decimal gecersizYuzde)
    {
        var servis = new KitapStokServisi();

        Action indirim = () => servis.IndirimliFiyatHesapla(100m, gecersizYuzde);

        indirim.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(100)]    // tam 100 — kitap bedava olur, kabul edilemez
    [InlineData(150)]    // 100'den büyük — anlamsız
    public void IndirimliFiyat_YuzdeYuzVeUstu_ArgumentExceptionFirlatir(decimal gecersizYuzde)
    {
        var servis = new KitapStokServisi();

        Action indirim = () => servis.IndirimliFiyatHesapla(100m, gecersizYuzde);

        indirim.Should().Throw<ArgumentException>()
            .WithMessage("*%100*");
    }

    [Fact]
    public void IndirimliFiyat_SifirYuzde_FiyatDegismez()
    {
        // Edge case: %0 indirim → fiyat aynı kalmalı.
        var servis = new KitapStokServisi();

        var sonuc = servis.IndirimliFiyatHesapla(120m, 0m);

        sonuc.Should().Be(120m);
    }
}
