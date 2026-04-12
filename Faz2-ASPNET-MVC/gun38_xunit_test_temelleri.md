# Gün 38 — Test Temelleri: xUnit v3 ve AAA Pattern

---

## 1. Unit Test Nedir?

```
Unit Test — tek bir birimi (class, method) izole ederek doğrular
  Dış bağımlılıklar yok: DB, HTTP, dosya sistemi yok
  Hızlı: milisaniyeler içinde çalışır
  Deterministic: her çalıştırmada aynı sonuç

Neyi test eder?
  ✓ Domain / iş kuralları  → Kitap fiyatı negatif olamaz
  ✓ Hesaplamalar           → İndirim oranı doğru uygulandı mı?
  ✓ Koşullu dallar         → Stok 0 iken sipariş oluşturulabilmeli mi?
  ✓ Hata senaryoları       → Geçersiz giriş doğru exception fırlatıyor mu?

Neyi test etmez?
  ✗ SQL sorgularının doğruluğu    → integration test işi
  ✗ HTTP endpoint'lerinin davranışı → WebApplicationFactory işi
  ✗ Migration sonrası DB şeması    → TestContainers işi
```

---

## 2. Test Pyramid

```
         /\
        /E2E\          az — yavaş, kırılgan, pahalı
       /------\
      /        \
     / Integr. \       orta — gerçek DB/HTTP, saniyeler
    /------------\
   /              \
  /   Unit Tests   \   çok — hızlı, izole, milisaniyeler
 /------------------\

Oran (genel öneri):
  Unit        ~70%  → iş mantığı, hızlı geri bildirim
  Integration ~20%  → servis/DB etkileşimi
  E2E          ~10% → kritik kullanıcı akışları

Ters piramit (fazla E2E, az unit) ne demek?
  → Test suite yavaş → geliştiriciler testi atlar → hatalar production'a sızar
```

---

## 3. xUnit v3 Kurulumu

xUnit, .NET ekosisteminin standart test framework'üdür. NUnit ve MSTest alternatifleri de var, ancak xUnit en yaygın kullanılan ve en temiz izolasyon modeline sahip olanıdır.

```bash
# Mevcut solution'a test projesi ekle
dotnet new xunit -n KitabeviMVC.Tests -o Faz2-ASPNET-MVC/KitabeviMVC.Tests
# xunit şablonu: xUnit + xunit.runner.visualstudio paketlerini otomatik ekler

dotnet sln add Faz2-ASPNET-MVC/KitabeviMVC.Tests/KitabeviMVC.Tests.csproj
# solution'a ekle: dotnet test ile birlikte çalışsın

# Test projesinden ana projeye referans ver
dotnet add Faz2-ASPNET-MVC/KitabeviMVC.Tests/KitabeviMVC.Tests.csproj \
    reference Faz2-ASPNET-MVC/KitabeviMVC/KitabeviMVC.csproj
# referans vermeseydin: test projesi test edilecek sınıflara erişemez
```

```xml
<!-- KitabeviMVC.Tests/KitabeviMVC.Tests.csproj — şablon sonrası içerik -->

<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <!-- IsPackable=false: NuGet paketi olarak yayınlanmasın — bu proje sadece test amaçlı -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <!-- Test.Sdk: dotnet test altyapısını sağlar; bu olmadan testler keşfedilmez -->

    <PackageReference Include="xunit" Version="2.9.2" />
    <!-- xunit: [Fact], [Theory] attribute'ları ve assertion motoru -->

    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <!-- runner.visualstudio: VS Test Explorer ve dotnet test CLI entegrasyonu -->
    <!-- PrivateAssets=all: bu paket sadece test altyapısı için, bağımlılık olarak yayılmasın -->
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\KitabeviMVC\KitabeviMVC.csproj" />
  </ItemGroup>

</Project>
```

---

## 4. AAA Pattern — Arrange, Act, Assert

Her test üç bölümden oluşur. Bu yapı testi okunabilir ve bakımı kolay kılar.

```csharp
// Tests/Domain/KitapFiyatTests.cs

public class KitapFiyatTests
{
    [Fact]
    public void IndirimUygula_YuzdeElli_FiyatYariyaIner()
    {
        // ─── Arrange — hazırlık ────────────────────────────────────────────
        var kitap = new Kitap { Baslik = "Clean Code", Fiyat = 100m };
        // test verisini burada hazırlarsın
        // Arrange bölümü uzarsa: Builder/ObjectMother pattern düşün

        // ─── Act — davranışı tetikle ───────────────────────────────────────
        kitap.IndirimUygula(50);
        // test ettiğin tek şeyi çağır
        // Act'te birden fazla satır varsa: muhtemelen iki farklı şeyi test ediyorsun

        // ─── Assert — doğrula ─────────────────────────────────────────────
        Assert.Equal(50m, kitap.Fiyat);
        // bunu yazmadan testi bitirip bıraksaydık: test her zaman geçer — anlamsız
    }
}
```

```
AAA'yı bozan yaygın hatalar:

  Multiple Act:
    kitap.IndirimUygula(50);    ← Act 1
    kitap.IndirimUygula(10);    ← Act 2  → iki ayrı test yaz
    Assert.Equal(45m, kitap.Fiyat);

  Assert olmadan test:
    [Fact]
    public void BirseyYap() { servis.Calistir(); }   ← yeşil geçer, hiçbir şey kanıtlamaz

  Arrange içinde Assert:
    var sonuc = servis.Calistir();
    Assert.NotNull(sonuc);      ← bu zaten Act/Assert, Arrange değil
    sonuc.IslemYap();
```

---

## 5. [Fact] — Tek Senaryo Testi

```csharp
// Tests/Domain/KitapTests.cs

public class KitapTests
{
    [Fact]
    public void YeniKitap_FiyatSifir_IsGecerli()
    {
        // Arrange
        var kitap = new Kitap { Baslik = "Test", Fiyat = 0m };

        // Act
        var gecerli = kitap.FiyatGecerliMi();

        // Assert
        Assert.True(gecerli);
        // Assert.True: bool beklentisi — False bekliyorsan Assert.False
    }

    [Fact]
    public void YeniKitap_FiyatNegatif_IsGecersiz()
    {
        var kitap = new Kitap { Baslik = "Test", Fiyat = -1m };

        var gecerli = kitap.FiyatGecerliMi();

        Assert.False(gecerli);
    }

    [Fact]
    public void IndirimUygula_NegatifOran_ArgumentExceptionFirlatir()
    {
        var kitap = new Kitap { Baslik = "Test", Fiyat = 100m };

        // Act + Assert birleşik: exception bekliyorsan Assert.Throws kullan
        var exception = Assert.Throws<ArgumentException>(() => kitap.IndirimUygula(-10));
        // bunu yazmadan try/catch yazsaydık: exception fırlatılmazsa test geçer — yanlış pozitif

        Assert.Contains("negatif", exception.Message, StringComparison.OrdinalIgnoreCase);
        // exception mesajının içeriğini de doğrulayabilirsin
    }
}
```

---

## 6. [Theory] + [InlineData] — Parametrik Test

Aynı davranışı farklı girdilerle test etmek için `[Theory]` kullanılır. Her `[InlineData]` ayrı bir test çalıştırması oluşturur.

```csharp
// Tests/Domain/KitapIndirimTests.cs

public class KitapIndirimTests
{
    [Theory]
    [InlineData(100,  0,  100)]   // indirim yok
    [InlineData(100, 10,   90)]   // %10 indirim
    [InlineData(100, 50,   50)]   // %50 indirim
    [InlineData(200, 25,  150)]   // farklı başlangıç fiyatı
    public void IndirimUygula_DogruHesaplar(decimal baslangic, int yuzde, decimal beklenen)
    // parametreler InlineData sırasıyla method parametrelerine eşlenir
    {
        var kitap = new Kitap { Fiyat = baslangic };

        kitap.IndirimUygula(yuzde);

        Assert.Equal(beklenen, kitap.Fiyat);
    }
    // [Fact] ile yazsaydık: 4 ayrı test method'u — kod tekrarı
    // [Theory] ile: 4 satır InlineData = 4 bağımsız test çalıştırması

    [Theory]
    [InlineData(101)]   // sınır aşımı
    [InlineData(-1)]    // negatif
    [InlineData(0)]     // sıfır — geçerli mi geçersiz mi, iş kuralına göre değişir
    public void IndirimUygula_GecersizOranlar_ExceptionFirlatir(int gecersizYuzde)
    {
        var kitap = new Kitap { Fiyat = 100m };

        Assert.Throws<ArgumentException>(() => kitap.IndirimUygula(gecersizYuzde));
    }
}
```

---

## 7. Test Class Isolation — Her Test Bağımsız Çalışır

xUnit'in en kritik tasarım kararı: her `[Fact]` için sınıfın **yeni bir örneği** oluşturulur. Testler arasında paylaşılan state yoktur.

```csharp
// Tests/Isolation/IzolasyonTests.cs

public class IzolasyonTests
{
    private readonly List<string> _log = new();
    // her test çağrısında bu list yeniden oluşturulur
    // NUnit/MSTest'te [SetUp] ile bunu elle yapman gerekirdi

    [Fact]
    public void IlkTest_LogEklenir()
    {
        _log.Add("ilk");
        Assert.Single(_log);   // 1 eleman: geçer
    }

    [Fact]
    public void IkinciTest_LogBosBaslar()
    {
        // _log burada boş — IlkTest'in eklediği "ilk" burada yok
        Assert.Empty(_log);    // geçer — xUnit yeni instance oluşturdu
        // NUnit'te TestFixture tek instance'dır; IlkTest çalıştıktan sonra
        // burada Assert.Single geçerdi — test sırası bağımlılığı oluşurdu
    }
}
```

```
xUnit vs NUnit/MSTest — lifecycle farkı:

  NUnit [TestFixture]:
    TestClass instance bir kez oluşturulur
    [SetUp] her testten önce çalışır — elle temizlik gerekir
    Testler arası state sızabilir → sıraya bağımlı testler oluşabilir

  xUnit [Fact]:
    Her test için new TestClass() — izolasyon garanti
    Constructor = Setup, IDisposable.Dispose = Teardown
    [SetUp] attribute'u yok — kasıtlı tasarım kararı
```

---

## 8. Constructor ve IDisposable — Setup / Teardown

```csharp
// Tests/Services/KitapServisTests.cs

public class KitapServisTests : IDisposable
// IDisposable: her testten sonra temizlik gerekiyorsa implement et
{
    private readonly KitabeviDbContext _context;
    private readonly EfKitapServisi   _servis;

    public KitapServisTests()
    // Constructor = [SetUp] — her test öncesi çalışır
    {
        var options = new DbContextOptionsBuilder<KitabeviDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            // Guid.NewGuid(): her test için ayrı DB adı — testler birbirinin verisini görmez
            // sabit isim kullansaydık: testler paralel çalışırken çakışabilir
            .Options;

        _context = new KitabeviDbContext(options);
        _servis  = new EfKitapServisi(_context);
    }

    [Fact]
    public async Task GetAllAsync_BosDatabaseDe_BosListeDoner()
    {
        var sonuc = await _servis.GetAllAsync();

        Assert.Empty(sonuc);
    }

    [Fact]
    public async Task GetAllAsync_UcKitapVar_UcElemanDoner()
    {
        // Arrange
        _context.Kitaplar.AddRange(
            new Kitap { Baslik = "A", Fiyat = 10m },
            new Kitap { Baslik = "B", Fiyat = 20m },
            new Kitap { Baslik = "C", Fiyat = 30m }
        );
        await _context.SaveChangesAsync();

        // Act
        var sonuc = await _servis.GetAllAsync();

        // Assert
        Assert.Equal(3, sonuc.Count);
    }

    public void Dispose()
    // Teardown — her testten sonra çalışır
    {
        _context.Dispose();
        // bunu yazmadan bıraksaydık: in-memory context belleği tutmaya devam eder
        // in-memory DB için kritik değil ama gerçek bağlantılarda connection leak olurdu
    }
}
```

---

## 9. ITestOutputHelper — Test İçi Loglama

Test içinde `Console.WriteLine` kullanılmaz — `ITestOutputHelper` enjekte edilir.

```csharp
// Tests/Domain/KitapLogTests.cs

public class KitapLogTests
{
    private readonly ITestOutputHelper _output;
    // xUnit test runner bu arayüzü otomatik enjekte eder

    public KitapLogTests(ITestOutputHelper output)
    {
        _output = output;
        // Console.WriteLine yazsaydık: dotnet test çıktısında görünmeyebilir
        // ITestOutputHelper: sadece başarısız testlerin çıktısını gösterir — gürültü az
    }

    [Fact]
    public void IndirimHesabi_AraDeğerleriLogla()
    {
        var kitap = new Kitap { Fiyat = 100m };

        _output.WriteLine($"Başlangıç fiyatı: {kitap.Fiyat}");
        // test geçerse bu satır dotnet test çıktısında görünmez
        // test başarısız olursa görünür — hata ayıklamayı kolaylaştırır

        kitap.IndirimUygula(20);

        _output.WriteLine($"İndirim sonrası: {kitap.Fiyat}");

        Assert.Equal(80m, kitap.Fiyat);
    }
}
```

---

## 10. Assert Sözlüğü

```csharp
// xUnit'te en sık kullanılan assert'ler

Assert.Equal(beklenen, gercek);           // eşitlik — primitive, string, koleksiyon
Assert.NotEqual(beklenmeyen, gercek);

Assert.True(kosul);                        // bool true
Assert.False(kosul);                       // bool false

Assert.Null(nesne);                        // null
Assert.NotNull(nesne);                     // null değil

Assert.Empty(koleksiyon);                  // 0 eleman
Assert.Single(koleksiyon);                 // tam 1 eleman
Assert.Equal(3, koleksiyon.Count);         // n eleman

Assert.Contains(eleman, koleksiyon);       // koleksiyonda var mı
Assert.DoesNotContain(eleman, koleksiyon);

Assert.Contains("alt", metin);             // string içeriyor mu
Assert.StartsWith("önEk", metin);
Assert.EndsWith("sonEk", metin);

Assert.Throws<ArgumentException>(() => ...);    // exception fırlatıyor mu
Assert.ThrowsAsync<ArgumentException>(async () => ...);  // async version

Assert.InRange(deger, alt, ust);           // deger ∈ [alt, ust]

// Koleksiyon eleman doğrulama
Assert.Collection(liste,
    eleman => Assert.Equal("A", eleman.Baslik),    // 1. eleman
    eleman => Assert.Equal("B", eleman.Baslik));   // 2. eleman
// sıra bağımlı, eleman sayısı da doğrulanır — listedeki her eleman için bir assertion lambda
```

---

## 11. Test İsimlendirme Kuralı

```
Format: MetodAdi_Senaryo_BeklenenSonuc

Örnekler:
  GetById_MevcutId_KitapDoner
  GetById_YanlisId_NullDoner
  IndirimUygula_YuzdeElliIndirim_FiyatYariyaIner
  IndirimUygula_NegatifYuzde_ArgumentExceptionFirlatir
  Ekle_GecerliKitap_DatabasedeKayitOlusur
  Ekle_AyniIsbn_DuplicateExceptionFirlatir

Kötü isimler — kaçın:
  Test1()                     → ne test ediyor belli değil
  IndirimTesti()              → senaryo yok, beklenti yok
  IndirimUygulaWorks()        → "works" hiçbir şey söylemiyor

Neden önemli?
  Test başarısız olduğunda:
    ✗ IndirimUygula_NegatifYuzde_ArgumentExceptionFirlatir
  → kod okumadan ne beklendiği, ne gittiği anlaşılır
```

---

## 12. Dizin Yapısı

```
KitabeviMVC.Tests/
├── Domain/
│   ├── KitapTests.cs              ← Kitap entity iş kuralları
│   └── KitapIndirimTests.cs       ← indirim hesaplama testleri
├── Services/
│   └── KitapServisTests.cs        ← EfKitapServisi unit testleri
├── Features/
│   └── KitapListeQueryTests.cs    ← CQRS handler testleri (Gün 39+)
└── KitabeviMVC.Tests.csproj
```

```
Test dosyası konumu:
  Ana proje: KitabeviMVC/Services/EfKitapServisi.cs
  Test dosyası: KitabeviMVC.Tests/Services/KitapServisTests.cs

  Ayna yapı → hangi test hangi kodu test ediyor hemen görülür
  Tests/ altında tek düz klasör bıraksaydın:
    50+ test dosyasında doğru testi bulmak için grep'e başvurursun
```

---

## 13. Testleri Çalıştırma

```bash
# Tüm testleri çalıştır
dotnet test

# Sadece test projesini çalıştır
dotnet test Faz2-ASPNET-MVC/KitabeviMVC.Tests/

# Belirli bir test class'ını çalıştır (--filter)
dotnet test --filter "FullyQualifiedName~KitapIndirimTests"

# Belirli bir test method'u
dotnet test --filter "FullyQualifiedName~IndirimUygula_YuzdeElli_FiyatYariyaIner"

# Ayrıntılı çıktı
dotnet test -v normal

# Başarısız testlerin çıktısını göster (ITestOutputHelper log'ları dahil)
dotnet test --logger "console;verbosity=detailed"
```

```
Çıktı örneği:
  Test run for KitabeviMVC.Tests.dll
  Starting test execution, please wait...
  A total of 1 test files matched the specified pattern.

  Passed!  - Failed: 0, Passed: 12, Skipped: 0, Total: 12, Duration: 47 ms
```

---

## 14. Java Köprüsü

```
JUnit 5                          →  xUnit
────────────────────────────────────────────────────────
@Test                            →  [Fact]
@ParameterizedTest + @ValueSource →  [Theory] + [InlineData]
@BeforeEach / @SetUp             →  Constructor  (otomatik)
@AfterEach  / @TearDown          →  IDisposable.Dispose
@BeforeAll  / @SetUpClass        →  IClassFixture<T>  (paylaşımlı setup)
@AfterAll   / @TearDownClass     →  ClassFixture'ın IDisposable.Dispose'u
@Disabled                        →  [Fact(Skip = "sebep")]
@DisplayName("açıklama")         →  method adı (xUnit'te ayrı attr yok)
Assertions.assertEquals(e, a)    →  Assert.Equal(e, a)
Assertions.assertThrows(...)     →  Assert.Throws<T>(...)
assertThat(x).isEqualTo(y)       →  FluentAssertions: x.Should().Be(y) (Gün 40)

En önemli fark:
  JUnit: test class bir kez oluşturulur, @BeforeEach her test öncesi
  xUnit: her test için new TestClass() — izolasyon constructor ile sağlanır
         [SetUp] attribute'u kasıtlı olarak yok
```

---

## 15. Özet

```
Unit Test temelleri
  İzole: DB, HTTP, dosya sistemi yok
  Hızlı: milisaniyeler — CI'da yüzlerce test çalışabilir
  Deterministic: her çalıştırmada aynı sonuç

AAA Pattern
  Arrange → test verisi hazırla
  Act     → test edilecek tek davranışı tetikle
  Assert  → beklentiyi doğrula

xUnit
  [Fact]              → tek senaryo
  [Theory]+[InlineData] → parametrik test — aynı davranış, farklı girdiler
  Constructor         → setup (her test için yeni instance)
  IDisposable.Dispose → teardown
  ITestOutputHelper   → test içi loglama (Console.WriteLine değil)

Test izolasyonu
  xUnit her [Fact] için yeni sınıf örneği oluşturur
  Testler arasında paylaşılan state yoktur
  InMemoryDatabase'de Guid.NewGuid() ile benzersiz isim → paralel güvenli

İsimlendirme
  MetodAdi_Senaryo_BeklenenSonuc
  Başarısız testin adı tek başına hatayı açıklamalı
```

---

## Sonraki Gün

Gün 39'da Test Doubles: Mock, Stub, Fake ve Spy türleri ile bağımlılıkları izole etme. Moq kütüphanesi ile `IKitapRepository` gibi arayüzleri sahte implementasyonla değiştirerek gerçek DB olmadan servis testleri yazma.
