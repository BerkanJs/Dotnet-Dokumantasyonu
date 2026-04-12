# Gün 43 — Architecture Testing: NetArchTest

## Neden Architecture Test?

Kod tabanı büyüdükçe mimari kurallar kişilere bağımlı hale gelir.
"Controller doğrudan DbContext kullanmamalı" yazan bir kuralı kod review'de gözden kaçırmak kolaydır.
Üç sprint sonra aynı anti-pattern 12 yerde çoğalmış olabilir.

**Gerçek vaka:**
Bir e-ticaret projesinde repository pattern uygulanıyordu.
Yeni geliştirici bir controller'a DbContext inject etti — code review fark etmedi.
6 ay sonra: 8 controller doğrudan DbContext kullanıyor.
Refactor: 2 haftalık sprint.

Architecture test olsaydı: ilk PR'da CI başarısız olurdu.
Düzeltme maliyeti: 5 dakika.

---

## Architecture Test Nedir?

**Mimari kural:** "Controller → Service → Repository → DbContext zinciri dışına çıkılamaz"
**Architecture test:** Bu kuralı otomatik ve sürekli doğrulayan test.

NetArchTest, bir assembly içindeki tip bağımlılıklarını analiz ederek
LINQ benzeri bir API ile kural tanımlamanızı sağlar:

```csharp
// İngilizce oku: "Assembly içindeki tipler, Controller namespace'inde olanlar,
//                class olanlar, EF Core namespace'ine bağımlı OLMAMALI"
Types.InAssembly(Assembly)
    .That()
    .ResideInNamespace("KitabeviMVC.Controllers")
    .And()
    .AreClasses()
    .ShouldNot()
    .HaveDependencyOn("Microsoft.EntityFrameworkCore")
    .GetResult()
    .IsSuccessful
```

---

## Kurulum

```xml
<!-- KitabeviMVC.Tests/KitabeviMVC.Tests.csproj -->
<PackageReference Include="NetArchTest.Rules" Version="1.3.2" />
```

```csharp
// KitabeviMVC.Tests/Architecture/ArchitectureTestBase.cs
using System.Reflection;
using NetArchTest.Rules;

public abstract class ArchitectureTestBase
// abstract: doğrudan örneklenemez — alt sınıflar kullanır.
{
    protected static readonly Assembly Assembly = typeof(Program).Assembly;
    // typeof(Program): KitabeviMVC projesinin entry point sınıfı.
    // .Assembly: o sınıfın bulunduğu assembly → KitabeviMVC.dll
    // static readonly: her test çalışmasında yeniden yüklenmez.
    // Bunu yazmassaydık: her test sınıfında Assembly.GetAssembly(typeof(Program)) tekrar ederdi.
}
```

---

## Bağımlılık Yönü Kuralları

`KitabeviMVC.Tests/Architecture/BagimlilikTests.cs`:

### Kural 1: Controller → EF Core Yasak

```csharp
[Fact]
public void Controllers_EfCore_DogrudenBagimliOlamaz()
{
    // Neden bu kural?
    //   Controller EF Core bağımlıysa: servis katmanı bypass edilmiş.
    //   İş mantığı controller'a sızar → test edilemez God Class.
    //   Refactor maliyeti artar: controller birim test edilemez.
    //
    // Kabul edilebilir:  Controller → IKitapServisi → EfKitapServisi → DbContext
    // Kabul edilemez:    Controller → DbContext (servis atlanmış)

    var sonuc = Types.InAssembly(Assembly)
        .That()
        .ResideInNamespace("KitabeviMVC.Controllers")
        // ResideInNamespace: tam namespace eşleşmesi + alt namespace'ler.
        // "KitabeviMVC.Controllers.V1" de dahil olur.
        .And()
        .AreClasses()
        // AreClasses: sadece class — interface, enum, record hariç.
        .ShouldNot()
        .HaveDependencyOn("Microsoft.EntityFrameworkCore")
        // HaveDependencyOn: bu namespace'den herhangi bir tip referans var mı?
        // using Microsoft.EntityFrameworkCore; → bağımlılık sayılır.
        .GetResult();

    sonuc.IsSuccessful.Should().BeTrue(
        because: $"Controller'lar EF Core'a doğrudan bağımlı olmamalı. " +
                 $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
    // IhlaledenleriListele: hangi sınıf ihlal ediyor? — hata mesajında göster.
}
```

### Kural 2: Controller → Repository Yasak

```csharp
[Fact]
public void Controllers_Repository_DogrudenBagimliOlamaz()
{
    // Neden?
    //   Repository business logic barındırmaz — sadece veri erişim.
    //   Controller → Repository: validasyon, orchestration katmanı atlanır.

    var sonuc = Types.InAssembly(Assembly)
        .That()
        .ResideInNamespace("KitabeviMVC.Controllers")
        .ShouldNot()
        .HaveDependencyOn("KitabeviMVC.Repositories")
        .GetResult();

    sonuc.IsSuccessful.Should().BeTrue(
        because: $"Controller'lar Repository'ye doğrudan bağımlı olmamalı. " +
                 $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
}
```

### Kural 3: Services → Controllers Yasak

```csharp
[Fact]
public void Services_Controllers_BagimliOlamaz()
{
    // Neden?
    //   Bağımlılık yönü tek yönlü olmalı: yukarıdan aşağıya.
    //   Servisler controller'a bağımlı olursa:
    //     - Döngüsel bağımlılık riski
    //     - Servis standalone test edilemez
    //     - Servis, web katmanından bağımsız kullanılamaz (CLI, background job)

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
```

### Kural 4: Features → Data (Belgelenmiş Teknik Borç)

```csharp
[Fact]
public void Features_Data_Namespace_BagimliOlamaz()
{
    // NOT: Mevcut handler'lar DbContext'e doğrudan bağımlı (Gün 35 kararı).
    // Bu test şu an BAŞARISIZ — kasıtlı olarak bırakılmış.
    // Faz3'te: handler'lar IKitapRepository üzerinden çalışacak.
    //
    // Neden Skip yapmıyoruz?
    //   [Fact(Skip = "...")] → test çıktısında görünmez → teknik borç unutulur.
    //   Başarısız test → CI'da kırmızı → ekip farkında → çözüm baskısı.

    var sonuc = Types.InAssembly(Assembly)
        .That()
        .ResideInNamespace("KitabeviMVC.Features")
        .ShouldNot()
        .HaveDependencyOn("KitabeviMVC.Data")
        .GetResult();

    if (!sonuc.IsSuccessful)
    {
        // Assert.True(false, mesaj): testi başarısız say ama açıklayıcı mesaj ver.
        var ihlaller = IhlaledenleriListele(sonuc);
        Assert.True(false,
            $"[TEKNİK BORÇ] Features/Handler'lar doğrudan DbContext kullanıyor. " +
            $"Faz3'te IKitapRepository ile düzeltilecek. İhlal edenler: {ihlaller}");
    }
}
```

---

## İsimlendirme Kuralları

`KitabeviMVC.Tests/Architecture/NamingConventionTests.cs`:

### Kural 1: Controller Suffix

```csharp
[Fact]
public void Controllers_Siniflari_ControllerIleBitmeli()
{
    // Neden?
    //   MVC routing "Controller" suffix'ini route adlandırmasında kullanır.
    //   [Route] attribute yoksa HomeController → /home route'u convention'dan gelir.
    //   "Controller" suffix'i yoksa: route çalışmayabilir, discovery bozulur.
    //   Ekip iletişimi: "Book" mi yoksa "BookController" mı? — belli olmalı.

    var sonuc = Types.InAssembly(Assembly)
        .That()
        .ResideInNamespace("KitabeviMVC.Controllers")
        .And()
        .AreClasses()
        .Should()
        .HaveNameEndingWith("Controller")
        // HaveNameEndingWith: sınıf adının son kısmı "Controller" ile bitmeli.
        .GetResult();

    sonuc.IsSuccessful.Should().BeTrue(
        because: "Controllers namespace'indeki class'lar 'Controller' ile bitmeli. " +
                 $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
}
```

### Kural 2: Interface I Prefix

```csharp
[Fact]
public void Interfaces_IIleBaslamali()
{
    // Neden?
    //   C# evrensel convention: IDisposable, IEnumerable, ILogger.
    //   "I" prefix yoksa: kodda interface mi class mı bakınca belli olmaz.
    //   Intellisense'de "I" yazınca tüm interface'ler gelir — discovery kolaylaşır.
    //   Java'da: interface suffix yok — convention farklı. C#'ta "I" zorunlu değil ama standart.

    var sonuc = Types.InAssembly(Assembly)
        .That()
        .AreInterfaces()
        // AreInterfaces: sadece interface tanımlarını filtreler.
        .Should()
        .HaveNameStartingWith("I")
        // HaveNameStartingWith: "I" ile başlamalı.
        .GetResult();

    sonuc.IsSuccessful.Should().BeTrue(
        because: "Tüm interface'ler 'I' ile başlamalı (C# naming convention). " +
                 $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
}
```

### Kural 3: Repository Suffix

```csharp
[Fact]
public void Repository_Siniflari_RepositoryIleBitmeli()
{
    var sonuc = Types.InAssembly(Assembly)
        .That()
        .ResideInNamespace("KitabeviMVC.Repositories")
        .And()
        .AreClasses()
        .And()
        .AreNotAbstract()
        // AreNotAbstract: abstract class'lar hariç.
        // EfRepository<T> gibi generic base class adı farklı olabilir.
        // Concrete implementation'lar: EfKitapRepository → KitapRepository olmalı.
        .Should()
        .HaveNameEndingWith("Repository")
        .GetResult();

    sonuc.IsSuccessful.Should().BeTrue(
        because: "Repositories namespace'indeki concrete class'lar 'Repository' ile bitmeli. " +
                 $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
}
```

### Kural 4: Handler Suffix

```csharp
[Fact]
public void Handler_Siniflari_HandlerIleBitmeli()
{
    // CQRS handler'ları MediatR tarafından bulunur.
    // "Handler" suffix'i: "bu sınıf bir komutu veya sorguyu işler" demek.

    // Doğru filtre: isim bazlı
    var sonuc = Types.InAssembly(Assembly)
        .That()
        .HaveNameEndingWith("Handler")
        .And()
        .AreClasses()
        .Should()
        .HaveNameEndingWith("Handler")
        // Totolojiyle görünse de: filtre + kural birlikte — NetArchTest syntax gereksinimi.
        .GetResult();

    sonuc.IsSuccessful.Should().BeTrue(
        because: "'Handler' ile biten sınıflar Features namespace'inde olmalı. " +
                 $"İhlal edenler: {IhlaledenleriListele(sonuc)}");
}
```

---

## NetArchTest API Referansı

### Filtreler (That() sonrası)

```csharp
.ResideInNamespace("KitabeviMVC.Controllers")    // namespace filtre
.AreClasses()                                     // sadece class
.AreInterfaces()                                  // sadece interface
.AreNotAbstract()                                 // abstract değil
.AreSealed()                                      // sealed class
.HaveNameEndingWith("Controller")                 // isim suffix filtre
.HaveNameStartingWith("I")                        // isim prefix filtre
.ImplementInterface(typeof(IDisposable))          // interface implement eden
.Inherit(typeof(ControllerBase))                  // belirli sınıftan miras alan
.HaveCustomAttribute<ObsoleteAttribute>()         // attribute'a sahip
```

### Kurallar (Should() / ShouldNot() sonrası)

```csharp
.HaveDependencyOn("Namespace.Adı")               // namespace'e bağımlı
.HaveNameEndingWith("Suffix")                     // isim kuralı
.HaveNameStartingWith("Prefix")                  // isim kuralı
.ResideInNamespace("Namespace")                   // namespace kuralı
.BeSealed()                                       // sealed olmalı
.NotBePublic()                                    // public olmamalı
.BeImmutable()                                    // immutable olmalı (readonly property)
```

### TestResult Kullanımı

```csharp
var sonuc = /* ... */ .GetResult();

sonuc.IsSuccessful      // bool: tüm tipler kuralı sağlıyor mu?
sonuc.FailingTypes      // IEnumerable<Type>?: kuralı ihlal eden tipler
sonuc.SucceedingTypes   // IEnumerable<Type>?: kuralı sağlayan tipler

// Yardımcı metod
private static string IhlaledenleriListele(TestResult sonuc) =>
    sonuc.FailingTypes is null || !sonuc.FailingTypes.Any()
        ? "yok"
        : string.Join(", ", sonuc.FailingTypes.Select(t => t.Name));
// t.Name: kısa sınıf adı — "KitapController"
// t.FullName: namespace dahil — "KitabeviMVC.Controllers.KitapController"
```

---

## Gelişmiş Kural Örnekleri

### Sealed Handler Kuralı

```csharp
[Fact]
public void Handler_Siniflari_SealedOlmali()
{
    // Neden sealed?
    //   MediatR handler'larından miras almak genellikle anti-pattern.
    //   sealed: "bu sınıf tasarımı tamamlandı, genişletme öngörülmüyor."
    //   Command pattern: her handler tek bir komuttan sorumlu — inheritance gerekmiyor.

    var sonuc = Types.InAssembly(Assembly)
        .That()
        .HaveNameEndingWith("Handler")
        .And()
        .AreClasses()
        .Should()
        .BeSealed()
        .GetResult();

    sonuc.IsSuccessful.Should().BeTrue(
        because: "Handler sınıfları sealed olmalı — inheritance öngörülmüyor.");
}
```

### Domain Modeli Temizliği

```csharp
[Fact]
public void Entities_InfrastructureBagimliOlamaz()
{
    // Neden?
    //   Domain entity'leri saf iş mantığı içermeli.
    //   EF Core attribute'ları ([Column], [Table]) entity'e girmemeli — Fluent API kullan.
    //   ASP.NET Core namespace'i entity'de olmamalı — katman karışımı.

    var sonuc = Types.InAssembly(Assembly)
        .That()
        .ResideInNamespace("KitabeviMVC.Models.Entities")
        .ShouldNot()
        .HaveDependencyOn("Microsoft.AspNetCore")
        .GetResult();

    sonuc.IsSuccessful.Should().BeTrue(
        because: "Entity'ler ASP.NET Core'a bağımlı olmamalı — domain saflığı.");
}
```

### ViewModel Immutability

```csharp
[Fact]
public void ViewModels_MutableOlabilir()
{
    // ViewModel'ler genellikle mutable — MVC model binding gerektirir.
    // Ama DTO'lar immutable olabilir — record kullan.
    // Bu test: namespace'deki class'lar var mı kontrol eder.
    var sonuc = Types.InAssembly(Assembly)
        .That()
        .ResideInNamespace("KitabeviMVC.Models.ViewModels")
        .And()
        .AreClasses()
        .Should()
        .HaveNameEndingWith("ViewModel")
        .GetResult();

    sonuc.IsSuccessful.Should().BeTrue();
}
```

---

## Java ArchUnit Karşılaştırması

| Kavram | NetArchTest (.NET) | ArchUnit (Java) |
|--------|-------------------|-----------------|
| Assembly/Jar seçimi | `Types.InAssembly(assembly)` | `JavaClasses classes = new ClassFileImporter().importPackages("com.kitabevi")` |
| Namespace filtre | `.ResideInNamespace("...")` | `.that().resideInAPackage("...")` |
| Class filtre | `.AreClasses()` | `.that().areClasses()` |
| Bağımlılık kuralı | `.ShouldNot().HaveDependencyOn("...")` | `.should().onlyDependOnClassesThat().resideInAPackage("...")` |
| İsim kuralı | `.HaveNameEndingWith("Controller")` | `.should().haveSimpleNameEndingWith("Controller")` |
| Sonuç al | `.GetResult().IsSuccessful` | `rule.check(classes)` (exception fırlatır) |
| Katman kuralı | Manuel zincirleme | `layeredArchitecture().layer("Controllers").definedBy("..controllers..")` |

**ArchUnit avantajı:** `layeredArchitecture()` built-in — onion/hexagonal hazır tanım.
**NetArchTest avantajı:** LINQ-style — C# developer'a doğal gelir.

---

## CI/CD Entegrasyonu

Architecture testleri unit testlerle aynı komutla çalışır:

```bash
dotnet test --filter "Category=Architecture"
# Tüm testler
dotnet test
```

```yaml
# GitHub Actions
- name: Test
  run: dotnet test --no-build --verbosity normal

# Architecture test başarısız → CI kırmızı → PR merge edilemez.
# Bu: kuralı "documentation" değil "enforcer" yapar.
```

---

## Teknik Borç Yönetimi

Architecture testinin güçlü yönü: ihlal olduğunda CI başarısız olur.
Ama bazı ihlaller geçici olarak kabul edilebilir (migration planı, legacy kod):

```csharp
// Yöntem 1: Yorumla belgele (tercih edilen)
if (!sonuc.IsSuccessful)
{
    Assert.True(false,
        "[TEKNİK BORÇ - Faz3] Açıklaması buraya. İhlal edenler: ...");
}

// Yöntem 2: Skip — görünmez olur, unutulur
[Fact(Skip = "Faz3'te düzeltilecek")]
public void Features_Data_BagimliOlamaz() { ... }
// Skip: test runner'da "Skipped" — CI geçer ama kural unutulur.
// Tercih edilmez: teknik borcu görünür tutmak daha iyi.

// Yöntem 3: Belirli tipi hariç tut
var sonuc = Types.InAssembly(Assembly)
    .That()
    .ResideInNamespace("KitabeviMVC.Features")
    .And()
    .DoNotHaveNameEndingWith("LegacyHandler")
    // DoNotHaveNameEndingWith: geçici hariç tutma.
    // LegacyHandler refactor edilene kadar kural dışı.
    .ShouldNot()
    .HaveDependencyOn("KitabeviMVC.Data")
    .GetResult();
```

---

## Özet

Architecture testing şu soruları otomatik yanıtlar:
- Bağımlılık yönü doğru mu? (Controller → Servis → Repository → DB)
- İsimlendirme convention'ları uygulanıyor mu?
- Domain modeli kirlenmiş mi?
- Teknik borç nerede?

Bu testler kod review'i değiştirmez — tamamlar.
"Bence bu yanlış" yerine "test başarısız" denir — nesnel, otomatik.

Bir sonraki adım: **TDD ve test stratejisi** ile tüm test türlerini bir araya getirmek (Gün 44).
