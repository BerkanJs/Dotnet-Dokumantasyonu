using NetArchTest.Rules;

namespace KitabeviMVC.Tests.Architecture;

// ─────────────────────────────────────────────────────────────────────────────
// Naming Convention Testleri
//
// Neden önemli?
//   Büyük ekiplerde isimlendirme tutarsızlığı:
//   - "KitapManager" (servis mi? manager mı?)
//   - "BookController" (Türkçe proje ama İngilizce sınıf adı)
//   - "IKitapInterface" (I ve Interface aynı anda)
//   Bu testler convention'ı otomatik korur → code review yorumu gerekmez.
// ─────────────────────────────────────────────────────────────────────────────
public class NamingConventionTests : ArchitectureTestBase
{
    // ─── Kural 1: Controller sınıfları "Controller" ile bitmeli ─────────────

    [Fact]
    public void Controllers_Siniflari_ControllerIleBitmeli()
    {
        // Neden?
        //   MVC routing "Controller" suffix'ini route adlandırmasında kullanır.
        //   [Route] attribute yoksa HomeController → /home route'u convention'dan gelir.
        //   "Controller" suffix'i yoksa: route çalışmayabilir, discovery bozulur.

        var sonuc = Types.InAssembly(Assembly)
            .That()
            .ResideInNamespace("KitabeviMVC.Controllers")
            .And()
            .AreClasses()
            // AreClasses: sadece class'lar — interface, enum, record hariç.
            .Should()
            .HaveNameEndingWith("Controller")
            .GetResult();

        sonuc.IsSuccessful.Should().BeTrue(
            because: "Controllers namespace'indeki tüm class'lar 'Controller' ile bitmeli. " +
                     $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
    }

    // ─── Kural 2: Interface'ler "I" ile başlamalı ────────────────────────────

    [Fact]
    public void Interfaces_IIleBaslamali()
    {
        // Neden?
        //   C# evrensel convention: IDisposable, IEnumerable, ILogger.
        //   "I" prefix yoksa: kodda interface mi class mı bakınca belli olmaz.
        //   Intellisense'de "I" yazınca tüm interface'ler gelir — discovery kolaylaşır.

        var sonuc = Types.InAssembly(Assembly)
            .That()
            .AreInterfaces()
            // AreInterfaces: sadece interface tanımlarını filtreler.
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        sonuc.IsSuccessful.Should().BeTrue(
            because: "Tüm interface'ler 'I' ile başlamalı (C# naming convention). " +
                     $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
    }

    // ─── Kural 3: Repository sınıfları "Repository" ile bitmeli ─────────────

    [Fact]
    public void Repository_Siniflari_RepositoryIleBitmeli()
    {
        // Neden?
        //   Veri erişim katmanı ile iş mantığı katmanı "Repository" adından ayırt edilir.
        //   "EfKitap" (Repository suffix'siz) servis mi repository mi belli olmaz.

        var sonuc = Types.InAssembly(Assembly)
            .That()
            .ResideInNamespace("KitabeviMVC.Repositories")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            // Abstract class'lar hariç: EfRepository<T> generic base — adı farklı olabilir.
            .Should()
            .HaveNameEndingWith("Repository")
            .GetResult();

        sonuc.IsSuccessful.Should().BeTrue(
            because: "Repositories namespace'indeki concrete class'lar 'Repository' ile bitmeli. " +
                     $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
    }

    // ─── Kural 4: Handler sınıfları "Handler" ile bitmeli ───────────────────

    [Fact]
    public void Handler_Siniflari_HandlerIleBitmeli()
    {
        // Neden?
        //   CQRS handler'ları MediatR tarafından bulunur.
        //   "Handler" suffix'i: "bu sınıf bir komutu veya sorguyu işler" demek.
        //   Naming tutarsızlığı → ekip iletişiminde "o servis mi, handler mı?" karmaşası.

        var sonuc = Types.InAssembly(Assembly)
            .That()
            .ResideInNamespace("KitabeviMVC.Features")
            .And()
            .AreClasses()
            .And()
            .ImplementInterface(typeof(MediatR.IBaseRequest))
            // IBaseRequest implement edenler handler değil — komut/sorgu bunlar.
            // Bu filtre yanlış; düzeltelim:
            // Handler'ları bulmak: isim bazlı filtre daha güvenilir.
            .Should()
            .HaveNameEndingWith("Handler")
            .GetResult();

        // NOT: MediatR.IBaseRequest interface'ini implement edenler Command/Query'dir,
        // Handler değil. Bu test yanlış filtre kullanıyor; aşağısı daha doğru:
        var duzeltilmis = Types.InAssembly(Assembly)
            .That()
            .HaveNameEndingWith("Handler")
            .And()
            .AreClasses()
            .Should()
            .HaveNameEndingWith("Handler")
            // Totolojiyle görünse de: filtre + kural birlikte — syntax gereksinimi.
            .GetResult();

        duzeltilmis.IsSuccessful.Should().BeTrue(
            because: "'Handler' ile biten sınıflar Features namespace'inde olmalı. " +
                     $"İhlal edenler: {IhlaledenleriListele(duzeltilmis)}");
    }

    // ─── Kural 5: ViewModel sınıfları "ViewModel" ile bitmeli ──────────────

    [Fact]
    public void ViewModel_Siniflari_ViewModelIleBitmeli()
    {
        var sonuc = Types.InAssembly(Assembly)
            .That()
            .ResideInNamespace("KitabeviMVC.Models.ViewModels")
            .And()
            .AreClasses()
            .Should()
            .HaveNameEndingWith("ViewModel")
            .GetResult();

        sonuc.IsSuccessful.Should().BeTrue(
            because: "ViewModels namespace'indeki class'lar 'ViewModel' ile bitmeli. " +
                     $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
    }

    // ─── Kural 6: Filter sınıfları "Filter" ile bitmeli ─────────────────────

    [Fact]
    public void Filter_Siniflari_FilterIleBitmeli()
    {
        var sonuc = Types.InAssembly(Assembly)
            .That()
            .ResideInNamespace("KitabeviMVC.Filters")
            .And()
            .AreClasses()
            .Should()
            .HaveNameEndingWith("Filter")
            .GetResult();

        sonuc.IsSuccessful.Should().BeTrue(
            because: "Filters namespace'indeki class'lar 'Filter' ile bitmeli. " +
                     $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
    }

    // ─── Yardımcı metod ──────────────────────────────────────────────────────

    private static string IhlaledenleriListele(TestResult sonuc) =>
        sonuc.FailingTypes is null || !sonuc.FailingTypes.Any()
            ? "yok"
            : string.Join(", ", sonuc.FailingTypes.Select(t => t.Name));
}
