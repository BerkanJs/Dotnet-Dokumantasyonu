# Gün 44 — TDD ve Test Stratejisi

## TDD Nedir?

Test-Driven Development (Test Güdümlü Geliştirme): önce test yaz, sonra kod.

Bu sıraya itiraz sezgisel görünür: "test edilecek kod yok ki test nasıl yazılır?"
Yanıt: test, kodun **nasıl davranması gerektiğini** tarif eder — implementasyonu değil.

**Geleneksel akış:**
1. Gereksinimleri anla
2. Kodu yaz
3. Test yaz (çoğunlukla atlanır veya geç yazılır)

**TDD akışı:**
1. Gereksinimleri anla
2. Önce testi yaz (derleme hatası — henüz kod yok)
3. Minimum kodu yaz (test geçer)
4. Refactor (test hâlâ geçiyor)

---

## Red → Green → Refactor Döngüsü

```
   ┌─────────────────────────────────────────────────────┐
   │                                                     │
   │    RED ──────────────► GREEN ──────────────► REFACTOR
   │     │                   │                      │   │
   │   Test yaz           Minimum              Tasarımı │
   │   Derleme            kod yaz              temizle  │
   │   hatası veya        Test geçiyor         Test hâlâ│
   │   test başarısız                          geçiyor  │
   │                                               │    │
   └───────────────────────────────────────────────┘    │
         ▲                                              │
         └──────────────────────────────────────────────┘
                        Tekrar et
```

### Neden bu sıra?

**RED — test önce:**
- Gereksinimi test olarak ifade edersiniz → düşünmeye zorlar.
- "Kitap fiyatı negatif olamaz" → `StokDus_NegatifFiyat_ArgumentException`
- Test koda specification görevi görür.

**GREEN — minimum kod:**
- Sadece testi geçirecek kadar kod yazılır.
- Gold plating (fazla özellik) engellenir.
- Tasarım kararları ertelenir — henüz bilinmiyor.

**REFACTOR — temizlik:**
- Test garanti ağı altında güvenle refactor.
- "Testler geçiyor → davranış bozulmadı" güvencesi.
- YAGNI (You Aren't Gonna Need It): sadece gereken yazılır.

---

## TDD Demonstrasyon: KitapStokServisi

`KitabeviMVC.Tests/Domain/TDDTests.cs` dosyasında bu döngü yorum satırlarıyla belgelendi:

### Döngü 1: Temel Stok Düşme

```csharp
// [RED]: Bu test önce yazıldı. StokDus metodu yoktu → derleme hatası.
[Fact]
public void StokDus_YeterlıStok_StokAdediDuser()
{
    var kitap  = new Kitap { StokAdedi = 10 };
    var servis = new KitapStokServisi(); // Derleme hatası: sınıf yok

    servis.StokDus(kitap, 3); // Derleme hatası: metod yok

    kitap.StokAdedi.Should().Be(7);
}

// [GREEN]: Minimum implementasyon
public class KitapStokServisi
{
    public void StokDus(Kitap kitap, int adet)
    {
        kitap.StokAdedi -= adet; // Minimum: test geçiyor.
    }
}

// [REFACTOR]: Guard clause'lar eklendi — test hâlâ geçiyor.
public void StokDus(Kitap kitap, int adet)
{
    if (adet <= 0)
        throw new ArgumentException("Adet sıfırdan büyük olmalı.", nameof(adet));
    // Guard: negatif adet sessizce kabul edilmesin.

    if (kitap.StokAdedi < adet)
        throw new InvalidOperationException($"Yetersiz stok. Mevcut: {kitap.StokAdedi}");
    // Guard: stok eksiye düşmesin.

    kitap.StokAdedi -= adet;
}
```

### Döngü 2: Hata Senaryosu Keşfi

TDD'nin güçlü yanı: hata senaryolarını **tasarım aşamasında** keşfettirir.

```csharp
// [RED]: "Stok 2 iken 5 düşürmeye çalışırsa ne olur?" — henüz davranış yok.
[Fact]
public void StokDus_YetersizStok_InvalidOperationFirlatir()
{
    var kitap  = new Kitap { StokAdedi = 2 };
    var servis = new KitapStokServisi();

    Action stokDus = () => servis.StokDus(kitap, 5);

    stokDus.Should()
        .Throw<InvalidOperationException>()
        .WithMessage("*Yetersiz stok*");
    // Wildcard: mesajın herhangi yerinde "Yetersiz stok" geçmeli.
    // Tam mesaj: kırılgan — format değişince test kırılır.
}

// [GREEN]: if (kitap.StokAdedi < adet) throw new InvalidOperationException(...)
// Test geçti — bu iş kuralı artık kod ve test olarak ikili güvence altında.
```

### Döngü 3: Edge Case Keşfi — [Theory]

```csharp
// [Theory] ile birden fazla veri seti — tek method, çok senaryo.
[Theory]
[InlineData(100,   0,  100)]    // %0 indirim → fiyat değişmez
[InlineData(100,  10,   90)]    // %10 indirim
[InlineData(100,  50,   50)]    // %50 indirim
[InlineData(150,  33, 100.5)]   // kesirli sonuç → 2 ondalık
public void IndirimliFiyat_DogruHesaplar(decimal fiyat, decimal yuzde, decimal beklenen)
{
    var servis = new KitapStokServisi();

    var sonuc = servis.IndirimliFiyatHesapla(fiyat, yuzde);

    sonuc.Should().Be(beklenen,
        because: $"{fiyat} * (1 - {yuzde}/100) = {beklenen} olmalı.");
}

// [Fact] ile yazsaydık: 4 ayrı test method — kod tekrarı.
// [Theory]: veri değişir, mantık aynı → parametrik test.
```

---

## Test Piramidi: Katman Başına Strateji

Her katman farklı test türü ister:

```
                    ┌──────────────┐
                    │     E2E      │  ← Az, yavaş, kırılgan
                    │ (Playwright) │    Kullanıcı senaryoları
                    └──────────────┘
                  ┌────────────────────┐
                  │    Integration     │  ← Orta, HTTP kontrakt
                  │ (WebApplicationFactory,│   Routing, middleware
                  │  TestContainers)   │
                  └────────────────────┘
              ┌────────────────────────────┐
              │       Architecture         │  ← Hızlı, NetArchTest
              │      (NetArchTest)         │    Katman kuralları
              └────────────────────────────┘
          ┌────────────────────────────────────┐
          │           Unit Tests               │  ← Çok, hızlı
          │  (xUnit + Moq + FluentAssertions)  │    İş mantığı
          └────────────────────────────────────┘
```

### Controller Katmanı — Integration Test

```csharp
// Controller birim testi: sınırlı değer.
// Controller: routing + servis çağrısı + result dönüşümü — üçü de integration test eder.

// Doğru: WebApplicationFactory ile HTTP seviyesinde test
[Fact]
public async Task Post_GecerliKitap_201VeLocationDoner()
{
    var yanit = await _client.PostAsJsonAsync("/api/v1.0/kitaplar", yeniKitap);
    yanit.StatusCode.Should().Be(HttpStatusCode.Created);
    yanit.Headers.Location.Should().NotBeNull();
}
// Bu test: routing ✓ model binding ✓ controller mantığı ✓ response format ✓
```

### Servis/Handler Katmanı — Unit Test

```csharp
// Handler iş mantığı: birim test idealdir.
// Bağımlılık: InMemory DB (EF Core interface değil concrete kullandığı için).

[Fact]
public async Task Handle_StokYetersiz_DomainExceptionFirlatir()
{
    await using var db = YeniInMemoryContext();
    db.Kitaplar.Add(new Kitap { Id = 1, StokAdedi = 2 });
    await db.SaveChangesAsync();

    var handler = new StokDusCommandHandler(db);
    var komut   = new StokDusCommand(KitapId: 1, Adet: 5);

    Func<Task> akt = () => handler.Handle(komut, CancellationToken.None);

    await akt.Should().ThrowAsync<DomainException>()
        .WithMessage("*Yetersiz stok*");
}
```

### Domain Katmanı — TDD ile Unit Test

```csharp
// Domain entity/value object: saf iş mantığı → TDD en değerli burada.
// Bağımlılık yok: mock, fake, DB gereksiz.

[Fact]
public void Kitap_NegatifFiyat_DomainExceptionFirlatir()
{
    Action akt = () => new Kitap { Fiyat = -10m };

    akt.Should().Throw<DomainException>()
        .WithMessage("*Fiyat negatif olamaz*");
}
// Domain: kurallar constructor/property setter'da uygulanır → her yerden güvence.
```

### Repository Katmanı — TestContainers

```csharp
// Repository: gerçek SQL davranışı test edilmeli.
// InMemory: UNIQUE constraint, RowVersion, LINQ to SQL farkları.

[Collection("SqlServer")]
public class EfKitapRepositoryTests
{
    [Fact]
    public async Task GetStokluKitaplar_SadecePozitifStok_Doner()
    {
        // Gerçek SQL: WHERE StokAdedi > 0 → SQL plan, index kullanımı test edilir.
        var stokluKitaplar = await _repository.GetStokluKitaplarAsync();

        stokluKitaplar.Should().OnlyContain(k => k.StokAdedi > 0);
    }
}
```

---

## Test Yazmak Ne Zaman Zor?

Testi yazmak zorlaşıyorsa bu genellikle **tasarım sorununa** işaret eder:

| Test Zorluğu | Tasarım Sinyali | Çözüm |
|-------------|-----------------|-------|
| Çok fazla mock gerekiyor | Sınıf çok fazla bağımlılık alıyor | Dependency Inversion, servis bölme |
| Test setup 50 satır | God class | Single Responsibility |
| Private metodu test etmek istiyorum | İç mantık çok karmaşık | Extract method, yeni sınıf |
| Metod yan etkileri: DB + email + log | Orchestration mantığı karmaşık | Command handler, CQRS |
| Test sırası önemli | Shared state | Test izolasyonu, before/after |

**TDD bunu önceden ortaya çıkarır:**
Testi yazmak zorsa → kod tasarımı yanlış → kod yazılmadan önce düzeltilebilir.

---

## Antipatternler

### 1. Test After (Sonradan Test)

```
Kod yaz → Test yaz (coverage için)

Sorun: Test, koda uyum sağlar — kuralı test etmez, kodu test eder.
Testin değeri azalır: "kod çalışıyor" teyidi, "kural doğrulama" değil.
```

### 2. Brittle Test — Kırılgan Test

```csharp
// YANLIŞ: implementasyon detayı test ediliyor
mockServis.Verify(s => s._dahiliCache.Get(1), Times.Once);
// _dahiliCache private metod → refactor'da değişirse test kırılır.

// DOĞRU: dışa görünen sonuç test ediliyor
var sonuc = await controller.Detay(1);
sonuc.Should().BeOfType<ViewResult>();
```

### 3. Test Logic — Testte Mantık

```csharp
// YANLIŞ: testte if/loop
foreach (var kitap in kitaplar)
{
    if (kitap.Kategori == "Yazılım")
        kitap.Fiyat.Should().BeGreaterThan(50m);
}
// Mantık yanlışsa: test yanlış senaryo test eder ama geçer.

// DOĞRU: FluentAssertions ile doğrudan
kitaplar
    .Where(k => k.Kategori == "Yazılım")
    .Should()
    .OnlyContain(k => k.Fiyat > 50m);
```

### 4. Flaky Test — Kararsız Test

```csharp
// YANLIŞ: DateTime.Now — her çalışmada farklı sonuç
kitap.EklemeTarihi.Should().Be(DateTime.Now);
// 1ms fark: test bazen geçer, bazen başarısız.

// DOĞRU: aralık kontrolü veya mock clock
kitap.EklemeTarihi.Should().BeCloseTo(DateTime.Now, precision: TimeSpan.FromSeconds(1));
// BeCloseTo: ±1 saniye tolerans.
```

### 5. Test Isolation Eksikliği

```csharp
// YANLIŞ: static shared state — testler arası kirlilik
private static int _sayac = 0;

[Fact]
public void Test1() { _sayac++; Assert.Equal(1, _sayac); }

[Fact]
public void Test2() { _sayac++; Assert.Equal(1, _sayac); } // Başarısız: _sayac = 2
// xUnit testleri sıralı çalışır ama sıra garanti değil.

// DOĞRU: her test kendi verisini oluşturur
[Fact]
public void Her_Test_Kendi_Verisini_Olusturur()
{
    var kitap = new Kitap { Id = Guid.NewGuid().GetHashCode() };
    // Her test farklı ID — diğer testleri etkilemez.
}
```

---

## Test Adlandırma Kuralı

İyi test adı üç soruyu yanıtlar:
1. **Ne test ediliyor?** — `StokDus`
2. **Koşul nedir?** — `YetersizStok`
3. **Beklenti nedir?** — `InvalidOperationFirlatir`

```
{Metod}_{Koşul}_{BeklenenSonuç}

✓ StokDus_YetersizStok_InvalidOperationFirlatir
✓ GetById_KayitYok_NullDoner
✓ Post_GecerliKitap_201VeLocationDoner
✓ IndirimliFiyat_NegatifYuzde_ArgumentExceptionFirlatir

✗ Test1
✗ StokDusTest
✗ ShouldThrowException
✗ TestStokDusMethod
```

**Neden önemli?**
Test başarısız olduğunda xUnit test adını gösterir.
`StokDus_YetersizStok_InvalidOperationFirlatir` → sorun anında belli.
`Test1` → hangi koşulda, neyin başarısız olduğu belli değil.

---

## Faz2 Test Mimarisi — Özet Tablo

| Test Dosyası | Tür | Kapsam | Araç |
|-------------|-----|--------|------|
| `Domain/TDDTests.cs` | Unit | KitapStokServisi iş kuralları | xUnit, FluentAssertions |
| `Domain/KitapFluentTests.cs` | Unit | FluentAssertions API demonstrasyon | FluentAssertions |
| `Application/KitapEkleCommandHandlerTests.cs` | Unit | CQRS handler mantığı | xUnit, InMemory DB |
| `Application/KitapListeQueryHandlerTests.cs` | Unit | Query handler, sıralama, DTO | xUnit, InMemory DB |
| `Integration/KitapApiControllerTests.cs` | Integration | HTTP API kontrakt | WebApplicationFactory |
| `Architecture/BagimlilikTests.cs` | Architecture | Katman bağımlılık yönü | NetArchTest |
| `Architecture/NamingConventionTests.cs` | Architecture | İsimlendirme kuralları | NetArchTest |

---

## TDD Faydaları — Gerçek Hayat

**Fintech şirketi örneği:**
Transfer servisini TDD ile yazarken:
- "Bakiye sıfır altına düşemez" kuralı → RED test
- Implementasyon: `if (bakiye < miktar) throw` → GREEN
- Refactor: domain exception, hata mesajı zenginleştirme

6 ay sonra yeni geliştirici aynı servisi değiştirdi.
Guard clause kaldırıldı (refactor sırasında gözden kaçtı).
Unit test anında RED → CI başarısız → PR merge edilmedi.
Production'a negatif bakiye yazan tek bir işlem bile ulaşmadı.

---

## Faz3'e Hazırlık

Faz2 sonunda test altyapısı kuruldu. Faz3'te:

1. **Repository pattern**: `IKitapRepository` inject edilerek handler'lar Moq ile test edilecek.
2. **Architecture test**: `Features → Data` kuralı geçecek (şu an teknik borç).
3. **TestContainers**: gerçek SQL Server testi production benzeri ortam.
4. **Performance test**: `BenchmarkDotNet` ile kritik sorgu performansı.

---

## Özet

| Kavram | Kısa Tarif |
|--------|-----------|
| TDD | Test önce → RED → GREEN → REFACTOR döngüsü |
| Unit test | Tek bileşen, izole, hızlı — iş mantığı |
| Integration test | Bileşenler arası, HTTP pipeline, DB |
| Architecture test | Katman kuralları otomatik doğrulama |
| Test Double | Dummy, Stub, Mock, Fake, Spy |
| [Fact] | Tek test senaryosu |
| [Theory] | Parametrik, çok veri seti |
| FluentAssertions | Okunabilir assertion, iyi hata mesajı |
| WebApplicationFactory | In-process HTTP pipeline testi |
| TestContainers | Gerçek DB, Docker container |
| NetArchTest | Assembly tip bağımlılığı kuralları |

Test yazmak kod yazmaktan farklı değil — sadece **sıra** farklı.
TDD bu sırayı tersine çevirir ve tasarımı iyileştirir.
