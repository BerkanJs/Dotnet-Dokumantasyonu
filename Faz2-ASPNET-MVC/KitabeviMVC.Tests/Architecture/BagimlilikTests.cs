using NetArchTest.Rules;

namespace KitabeviMVC.Tests.Architecture;

// ─────────────────────────────────────────────────────────────────────────────
// Bağımlılık Yönü Testleri
//
// Bu testler "katman bağımlılık yönü" kurallarını otomatik doğrular.
//
// Neden elle code review yetmez?
//   Gerçek vaka: Bir geliştirici yoğun sprint sırasında controller'a direkt
//   DbContext inject etti. Code review gözden kaçtı. 3 sprint sonra benzer
//   pattern 5 yere yayıldı. Düzeltmek 2 gün sürdü.
//   Architecture test olsaydı: ilk PR'da tespit edilirdi.
//
// Kural: Controller → Servis → Repository → DbContext
//   Controller doğrudan DbContext veya EF Core namespace'ine bağımlı olmamalı.
//   Services doğrudan Controllers namespace'ine bağımlı olmamalı.
//   Features (CQRS handler) doğrudan Data namespace'ine bağımlı olmamalı.
// ─────────────────────────────────────────────────────────────────────────────
public class BagimlilikTests : ArchitectureTestBase
{
    // ─── Kural 1: Controller → EF Core doğrudan bağımlılık yasak ────────────

    [Fact]
    public void Controllers_EfCore_DogrudenBagimliOlamaz()
    {
        // Neden bu kural?
        //   Controller EF Core bağımlıysa: servis katmanı bypass edilmiş.
        //   İş mantığı controller'a sızar → test edilemez büyük sınıflar (God Class).
        //   Refactor maliyeti artar.
        //
        // Kabul edilebilir: Controller → IKitapServisi → EfKitapServisi → DbContext
        // Kabul edilemez: Controller → DbContext (servis atlanmış)

        var sonuc = Types.InAssembly(Assembly)
            .That()
            .ResideInNamespace("KitabeviMVC.Controllers")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            // "Microsoft.EntityFrameworkCore" namespace'inden herhangi bir tip import edilmemeliCONTROLLER
            .GetResult();

        sonuc.IsSuccessful.Should().BeTrue(
            because: $"Controller'lar EF Core'a doğrudan bağımlı olmamalı. " +
                     $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
    }

    // ─── Kural 2: Controller → Repository doğrudan yasak ────────────────────

    [Fact]
    public void Controllers_Repository_DogrudenBagimliOlamaz()
    {
        // Neden?
        //   Repository business logic barındırmaz — sadece veri erişim.
        //   Controller → Repository: servis katmanı (validation, orchestration) atlanır.
        //   Benzer case: Spring Controller → Repository direkt inject — anti-pattern.

        var sonuc = Types.InAssembly(Assembly)
            .That()
            .ResideInNamespace("KitabeviMVC.Controllers")
            .ShouldNot()
            .HaveDependencyOn("KitabeviMVC.Repositories")
            .GetResult();

        sonuc.IsSuccessful.Should().BeTrue(
            because: $"Controller'lar Repository'ye doğrudan bağımlı olmamalı. " +
                     $"Servis katmanı üzerinden gitmeli. İhlal edenler: {IhlaledenleriListele(sonuc)}");
    }

    // ─── Kural 3: Services → Controllers yasak (yukarı bağımlılık yok) ──────

    [Fact]
    public void Services_Controllers_BagimliOlamaz()
    {
        // Neden?
        //   Bağımlılık yönü yukarı gidemez: servisler controller'a bağımlı olursa
        //   döngüsel bağımlılık riski ve servis yeniden kullanılabilirliği azalır.
        //   Servis, controller olmadan standalone test edilemez hale gelir.

        var sonuc = Types.InAssembly(Assembly)
            .That()
            .ResideInNamespace("KitabeviMVC.Services")
            .ShouldNot()
            .HaveDependencyOn("KitabeviMVC.Controllers")
            .GetResult();

        sonuc.IsSuccessful.Should().BeTrue(
            because: $"Servisler controller'a bağımlı olmamalı. " +
                     $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
    }

    // ─── Kural 4: Features (CQRS) → Data doğrudan yasak ─────────────────────

    [Fact]
    public void Features_Data_Namespace_BagimliOlamaz()
    {
        // NOT: Mevcut handler'lar DbContext'e doğrudan bağımlı (Gün 35 kararı).
        // Bu test şu an başarısız olabilir — CQRS handler'ları DbContext kullanıyor.
        //
        // Faz3'te: handler'lar IKitapRepository üzerinden çalışacak.
        // O zaman bu test geçecek.
        // Şimdilik: [Fact(Skip = "Faz3'te Repository pattern ile düzeltilecek")]
        //
        // Bu şekilde bırakırsak: architecture test, teknik borcu belgeler.

        var sonuc = Types.InAssembly(Assembly)
            .That()
            .ResideInNamespace("KitabeviMVC.Features")
            .ShouldNot()
            .HaveDependencyOn("KitabeviMVC.Data")
            .GetResult();

        // Bu test şu an başarısız — kasıtlı olarak Skip değil, bırakıyoruz.
        // Başarısız test: "bu kural ihlal ediliyor" bildirimi.
        // Ekip farkında ve Faz3'te düzeltme planında.
        if (!sonuc.IsSuccessful)
        {
            // Başarısız ama bilgilendirici mesaj — exception fırlatmıyoruz.
            // Gerçek projede bu satırı kaldır ve test başarısız olsun → CI'da uyarı.
            var ihlaller = IhlaledenleriListele(sonuc);
            Assert.True(false,
                $"[TEKNİK BORÇ] Features/Handler'lar doğrudan DbContext kullanıyor. " +
                $"Faz3'te IKitapRepository ile düzeltilecek. İhlal edenler: {ihlaller}");
        }
    }

    // ─── Kural 5: Repositories → Controllers yasak ───────────────────────────

    [Fact]
    public void Repositories_Controllers_BagimliOlamaz()
    {
        var sonuc = Types.InAssembly(Assembly)
            .That()
            .ResideInNamespace("KitabeviMVC.Repositories")
            .ShouldNot()
            .HaveDependencyOn("KitabeviMVC.Controllers")
            .GetResult();

        sonuc.IsSuccessful.Should().BeTrue(
            because: $"Repository'ler controller'a bağımlı olmamalı. " +
                     $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
    }

    // ─── Yardımcı metod ──────────────────────────────────────────────────────

    private static string IhlaledenleriListele(TestResult sonuc)
    {
        if (sonuc.FailingTypes is null || !sonuc.FailingTypes.Any())
            return "yok";

        return string.Join(", ", sonuc.FailingTypes.Select(t => t.Name));
        // t.Name: sınıf adı — "KitapController", "EfKitapServisi" gibi.
        // t.FullName kullanmak: namespace dahil tam ad — daha açıklayıcı ama uzun.
    }
}
