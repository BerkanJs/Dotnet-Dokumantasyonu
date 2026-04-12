# Gün 39 — Test Doubles ve Moq

## Neden Test Double Kullanılır?

Gerçek dünya sistemleri birbirine bağımlı bileşenlerden oluşur.
`KitapController` → `IKitapServisi` → `EfKitapServisi` → `KitabeviDbContext` → SQL Server.

Bir controller testinde gerçek veritabanına bağlanmak şu sorunları yaratır:

| Sorun | Açıklama | Etkisi |
|-------|----------|--------|
| **Yavaşlık** | Ağ + disk I/O her test için tekrar | 1 test: 50ms → 500 test: 25 saniye |
| **Kırılganlık** | DB connection, test sırası, transaction state | CI'da random failure |
| **İzolasyon yok** | Test A veritabanı bozarsa Test B de bozulur | Flaky testler |
| **Kontrol yok** | Hata senaryosu simüle etmek zor | Exception path test edilemez |

Test Double bu bağımlılıkları **sahte ama kontrollü** nesnelerle değiştirir.
Birim testinin konusu: bağımlılıkların davranışı değil, **test edilen sınıfın mantığı**.

---

## Test Double Türleri (Gerard Meszaros Terminolojisi)

Gerard Meszaros 2007'de *xUnit Test Patterns* kitabında 5 test double türünü tanımladı.
Bu terimleri Moq gibi kütüphaneler hâlâ temel alır.

### 1. Dummy — Doldurucu

Parametre listesini doldurmak için gerekli ama hiç kullanılmaz.

```csharp
// Gerçek senaryo: KitapController oluşturmak için IKitapServisi gerekiyor
// ama test sadece URL routing'i kontrol ediyor — servis hiç çağrılmayacak.

IKitapServisi dummy = null!;
// Ya da: var dummy = new Mock<IKitapServisi>().Object;
// .Object: Mock<T>'nin implement ettiği interface örneğini verir.

var controller = new KitapController(dummy, NullLogger<KitapController>.Instance);
// NullLogger: ASP.NET Core'un built-in dummy'si — log yazmaz, exception atmaz.
// Dummy: bir nesneye ihtiyaç var ama davranışı önemli değil.
```

**Ne zaman kullanılır?** Sınıfın constructor'ı zorunlu parametre istiyor ama test o parametreyi kullanmıyorsa.

---

### 2. Stub — Önceden Ayarlanmış Yanıt

Belirli bir çağrıya belirli bir yanıt döndürür. Çağrıların kaç kez yapıldığını doğrulamaz.

```csharp
// Senaryo: Bir e-ticaret sitesinde ürün sayfasını test ediyoruz.
// Fiyat servisi gerçekte dış API çağrısı yapıyor — test ortamında bunu istemiyoruz.

var fiyatStub = new Mock<IFiyatServisi>();

fiyatStub
    .Setup(s => s.FiyatGetirAsync(42))
    // Setup: "Bu metod bu parametreyle çağrılırsa..." — koşul tanımı.
    .ReturnsAsync(99.90m);
    // ReturnsAsync: async Task<decimal> döndüren method için.
    // Returns: sync dönen method için.

// Stub sadece yanıt verir — "kaç kez çağrıldı" ile ilgilenmez.
// Verify çağrısı yok → bu onu Mock'tan ayıran temel fark.
```

**Gerçek hayat örneği:** Ödeme sistemi testi.
Gerçek Iyzico API'yi çağırmak yerine başarılı ödeme yanıtı döndüren stub kullanırsınız.
Test her çalıştığında gerçek para kesilmez, banka sunucusuna bağımlılık olmaz.

---

### 3. Mock — Çağrı Doğrulayan

Hem yanıt verir hem de belirli çağrıların yapıldığını doğrular.
Doğrulama yapılmadan Mock, Stub'dan farksızdır.

```csharp
// Senaryo: Stok düşüldüğünde e-posta bildirim gönderilmeli.
// E-posta servisi gerçekten e-posta atmadan test edelim.

var emailMock = new Mock<IEmailServisi>();

var siparis = new SiparisServisi(emailMock.Object);
siparis.SiparisKaydet(new Siparis { MusteriEmail = "ali@example.com" });

// Assert: e-posta servisi belirli parametrelerle ÇAĞRILDI MI?
emailMock.Verify(
    e => e.GonderAsync(
        It.Is<string>(email => email.Contains("@")),
        // It.Is<T>: özel koşul — email "@" içermeli.
        "Siparişiniz alındı",
        It.IsAny<string>()
        // It.IsAny<T>: herhangi bir değer kabul et — içeriğini önemsemiyoruz.
    ),
    Times.Once
    // Times.Once: tam bir kez çağrılmalı.
    // Times.Never: hiç çağrılmamalı.
    // Times.Exactly(n): tam n kez.
    // Times.AtLeastOnce: en az bir kez.
);
```

**Ne zaman Mock, ne zaman Stub?**
- Test **sonucuna** (return value) odaklanıyorsa → Stub
- Test **davranışa** (metod çağrısı gerçekleşti mi?) odaklanıyorsa → Mock

---

### 4. Fake — Gerçekçi Hafif Uygulama

Gerçek implementasyonla aynı mantığı çok daha basit şekilde uygular.
In-memory veritabanı, dosya sistemi yerine dictionary — bunlar Fake'tir.

```csharp
// FakeKitapRepository: gerçek EfKitapRepository gibi davranır ama List<Kitap> kullanır.
// Projemizde: KitabeviMVC.Tests/Fakes/FakeKitapRepository.cs

public class FakeKitapRepository : IKitapRepository
{
    private readonly List<Kitap> _veri = new();
    private int _sonId = 0;

    public Task<List<Kitap>> GetAllAsync() =>
        Task.FromResult(_veri.ToList());
    // .ToList(): kopya döndürür — dışarıdan liste değiştirilemez.
    // Task.FromResult: async method signature'ı karşılamak için.

    public Task EkleAsync(Kitap kitap)
    {
        kitap.Id = ++_sonId;     // Auto-increment Id simülasyonu
        _veri.Add(kitap);
        return Task.CompletedTask;
    }

    // ... diğer metodlar
}
```

**Fake vs Mock farkı:**
- Mock: her testte davranış sıfırdan Setup edilir — `new Mock<IKitapRepository>()`
- Fake: gerçek nesne gibi durumu tutar — birden fazla metod çağrısı arasında tutarlı

**Gerçek hayat:** React'ın test render'ı, SQLite in-memory, H2 database (Java Spring) hepsi Fake.

---

### 5. Spy — Gizlice İzleyen

Gerçek nesnenin üzerine oturur, çağrıları kaydeder ama davranışını değiştirmez.
Moq'da `CallBase = true` ile yapılır.

```csharp
// Senaryo: Gerçek implementasyonu test ederken kaç kez çağrıldığını da görmek istiyoruz.

var spy = new Mock<IKitapServisi> { CallBase = true };
// CallBase = true: gerçek implementasyon çağrılır — tamamen sahte değil.
// Spy: gerçek nesne + çağrı kayıt defteri.

// Bu pattern nadiren kullanılır — genellikle Fake veya Mock yeter.
// Integration testte gerçek kod çalışırken yan etki izlemek için kullanılır.
```

---

## Moq: Kapsamlı API Rehberi

Moq, .NET'te en yaygın kullanılan mock kütüphanesidir.
Java'da Mockito'nun .NET karşılığı olarak düşünülebilir.

### Mock Oluşturma

```csharp
// Temel kullanım
var mock = new Mock<IKitapServisi>();
IKitapServisi nesne = mock.Object;
// mock: yapılandırma ve doğrulama için wrapper.
// mock.Object: interface'i implement eden gerçek nesne — DI'ya inject edilir.

// Strict mode: Setup edilmemiş çağrı exception fırlatır
var strictMock = new Mock<IKitapServisi>(MockBehavior.Strict);
// Strict: her çağrı önceden Setup edilmeli — sıkı kontrol.
// Loose (default): Setup edilmemiş çağrı default değer döndürür (null, 0, false).
// Gerçek projede: Strict genellikle fazla kırılgan — Loose tercih edilir.
```

---

### Setup ve Returns

```csharp
var mock = new Mock<IKitapServisi>();

// 1. Sabit değer döndür
mock.Setup(s => s.GetAllAsync())
    .ReturnsAsync(new List<Kitap> { new Kitap { Baslik = "Test" } });

// 2. Parametre bağımlı değer döndür
mock.Setup(s => s.GetByIdAsync(1))
    .ReturnsAsync(new Kitap { Id = 1, Baslik = "Clean Code" });

mock.Setup(s => s.GetByIdAsync(999))
    .ReturnsAsync((Kitap?)null);
// 999: bulunamayan kitap → null döner. 404 senaryosu test edilir.

// 3. It.IsAny: herhangi bir değer
mock.Setup(s => s.GetByIdAsync(It.IsAny<int>()))
    .ReturnsAsync(new Kitap { Baslik = "Herhangi Kitap" });
// It.IsAny<int>(): 0, 1, -5, int.MaxValue — hepsi bu Setup'a girer.
// Dikkat: Daha spesifik Setup (GetByIdAsync(1)) her zaman önce gelir.

// 4. It.Is: özel koşul
mock.Setup(s => s.GetByIdAsync(It.Is<int>(id => id > 0)))
    .ReturnsAsync(new Kitap { Baslik = "Pozitif ID" });
// It.Is<int>(predicate): lambda koşulunu sağlayan değerler.
// Pozitif ID → kitap döner, negatif ID → Setup yok → null (Loose mode).

// 5. Exception fırlat
mock.Setup(s => s.GetByIdAsync(-1))
    .ThrowsAsync(new ArgumentException("ID negatif olamaz"));
// ThrowsAsync: async method exception fırlatır.
// Throws: sync method exception fırlatır.
// Senaryo: Geçersiz input durumunu test etmek.

// 6. Callback: çağrı anında yan etki
var cagrilanId = 0;
mock.Setup(s => s.GetByIdAsync(It.IsAny<int>()))
    .Callback<int>(id => cagrilanId = id)
    // Callback: Setup çalışmadan önce tetiklenir — çağrı argümanını yakala.
    .ReturnsAsync(new Kitap());

await mock.Object.GetByIdAsync(42);
Assert.Equal(42, cagrilanId);
// Callback: doğrulama zorsa argümanı sakla ve sonra assert et.
```

---

### Verify — Çağrı Doğrulama

```csharp
// Senaryo: KitapSilindi olayında NotificationServisi çağrılmalı
var notifMock = new Mock<INotificationServisi>();
var servis = new KitapYonetimServisi(notifMock.Object);

await servis.KitapSilAsync(1);

// Temel doğrulama
notifMock.Verify(
    n => n.GonderAsync("Kitap silindi: 1"),
    Times.Once
);

// Hiç çağrılmadığını doğrula
notifMock.Verify(
    n => n.GonderAsync(It.IsAny<string>()),
    Times.Never
);
// Senaryo: Kitap silinmezse (ID bulunamazsa) bildirim gönderilmemeli.

// En az bir kez
notifMock.Verify(
    n => n.GonderAsync(It.Is<string>(s => s.StartsWith("Kitap"))),
    Times.AtLeastOnce
);

// Tam N kez
notifMock.Verify(
    n => n.GonderAsync(It.IsAny<string>()),
    Times.Exactly(3)
);
// Senaryo: Toplu silme — 3 kitap silindiğinde 3 bildirim gönderilmeli.

// Tüm Setup'ların çağrıldığını doğrula
mock.VerifyAll();
// VerifyAll: mock üzerindeki tüm Setup'lar en az bir kez çağrıldı mı?
// Dikkat: Her Setup doğrulanır — fazla kısıtlayıcı olabilir.
```

---

### Property Setup

```csharp
var mock = new Mock<IKonfigurasyonServisi>();

// Property stub
mock.Setup(k => k.ApiUrl).Returns("https://api.example.com");
mock.Setup(k => k.ZamanAsimi).Returns(TimeSpan.FromSeconds(30));

// SetupProperty: değer atanabilir (get + set)
mock.SetupProperty(k => k.ApiUrl, "https://default.api.com");
mock.Object.ApiUrl = "https://test.api.com";
// SetupProperty: mock üzerindeki property'yi gerçek değişken gibi çalıştırır.
// Senaryo: Konfigürasyon değerini test içinde dinamik değiştirmek.
```

---

### Sequence: Sıralı Farklı Yanıtlar

```csharp
// Senaryo: Retry mekanizması testi — ilk 2 çağrı başarısız, 3.'de başarılı.
var mock = new Mock<IDisFiyatApiIstemcisi>();

mock.SetupSequence(a => a.FiyatGetirAsync(It.IsAny<int>()))
    .ThrowsAsync(new HttpRequestException("Timeout"))  // 1. çağrı
    .ThrowsAsync(new HttpRequestException("Timeout"))  // 2. çağrı
    .ReturnsAsync(99.90m);                            // 3. çağrı başarılı

// SetupSequence: her çağrıda farklı sonuç — sıralı davranış.
// Retry policy testi için ideal: Polly veya AddStandardResilienceHandler.
```

---

## Projemizdeki Kullanım

### FakeKitapRepository ile KitapEkleCommand Testi

`KitabeviMVC.Tests/Application/KitapEkleCommandHandlerTests.cs` dosyasında
handler'ın iş mantığını izole test ediyoruz:

```csharp
public class KitapEkleCommandHandlerTests
{
    // Her test için taze InMemory DB — test izolasyonu.
    private static KitabeviDbContext YeniContext() =>
        new KitabeviDbContext(
            new DbContextOptionsBuilder<KitabeviDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                // Guid.NewGuid(): her test farklı DB adı → birbirini kirletmez.
                .Options);

    [Fact]
    public async Task Handle_GecerliKomut_KitapEklenir()
    {
        // ─── Arrange ──────────────────────────────────────────────────────────
        await using var db = YeniContext();
        var handler = new KitapEkleCommandHandler(db);
        // Handler: DbContext alıyor — IKitapRepository değil.
        // Moq yerine InMemory: handler EfCore metodlarını doğrudan kullanıyor.

        var komut = new KitapEkleCommand("Temiz Kod", "Robert Martin", 89.90m, "Yazılım", 50);

        // ─── Act ──────────────────────────────────────────────────────────────
        var id = await handler.Handle(komut, CancellationToken.None);

        // ─── Assert ───────────────────────────────────────────────────────────
        var eklenenKitap = await db.Kitaplar.FindAsync(id);
        eklenenKitap.Should().NotBeNull();
        eklenenKitap!.Baslik.Should().Be("Temiz Kod");
        // !: null-forgiving operator — Should().NotBeNull() null olmadığını garanti etti.
    }
}
```

**Neden Moq değil InMemory?**
Handler doğrudan `KitabeviDbContext` inject ediyor — interface değil concrete sınıf.
Moq abstract/interface mock'lar — concrete EF Core context'i mock'layamazsınız.
InMemory database gerçek EF Core davranışını simüle eder: SaveChanges, FindAsync.

---

### Moq ile Controller Testi

Controller'lar `IKitapServisi` interface'i alır — burada Moq mantıklı:

```csharp
public class KitapControllerTests
{
    [Fact]
    public async Task Index_KitaplarVar_ViewBageDoldurur()
    {
        // ─── Arrange ──────────────────────────────────────────────────────────
        var mockServis = new Mock<IKitapServisi>();
        mockServis
            .Setup(s => s.GetAllAsync())
            .ReturnsAsync(new List<KitapViewModel>
            {
                new() { Id = 1, Baslik = "Clean Code" },
                new() { Id = 2, Baslik = "DDD"        },
            });
        // Stub: servis çağrısı → sabit liste döner.
        // Controller'ın DB'ye erişmesine gerek yok.

        var controller = new KitapController(mockServis.Object);

        // ─── Act ──────────────────────────────────────────────────────────────
        var result = await controller.Index() as ViewResult;

        // ─── Assert ───────────────────────────────────────────────────────────
        result.Should().NotBeNull();
        var model = result!.Model as List<KitapViewModel>;
        model.Should().HaveCount(2);

        // Mock doğrulama: servis tam bir kez çağrıldı mı?
        mockServis.Verify(s => s.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task Detay_KitapBulunamadi_NotFoundDoner()
    {
        // ─── Arrange ──────────────────────────────────────────────────────────
        var mockServis = new Mock<IKitapServisi>();
        mockServis
            .Setup(s => s.GetByIdAsync(999))
            .ReturnsAsync((KitapViewModel?)null);
        // null döndür: 999 ID'li kitap yok → 404 testi.

        var controller = new KitapController(mockServis.Object);

        // ─── Act ──────────────────────────────────────────────────────────────
        var result = await controller.Detay(999);

        // ─── Assert ───────────────────────────────────────────────────────────
        result.Should().BeOfType<NotFoundResult>();
    }
}
```

---

## Java Mockito Karşılaştırması

| Kavram | Moq (.NET) | Mockito (Java) |
|--------|-----------|----------------|
| Mock oluştur | `new Mock<IServis>()` | `@Mock IServis servis` veya `mock(IServis.class)` |
| Nesneyi al | `mock.Object` | doğrudan `servis` alanı |
| Setup | `mock.Setup(s => s.Metod()).Returns(...)` | `when(servis.metod()).thenReturn(...)` |
| Async Setup | `.ReturnsAsync(...)` | `.thenReturn(CompletableFuture.completedFuture(...))` |
| Exception | `.Throws(new Ex())` | `.thenThrow(new Ex())` |
| Doğrulama | `mock.Verify(s => s.Metod(), Times.Once)` | `verify(servis, times(1)).metod()` |
| Any parametre | `It.IsAny<int>()` | `anyInt()` / `any(Tip.class)` |
| Koşul | `It.Is<int>(x => x > 0)` | `argThat(x -> x > 0)` |
| Callback | `.Callback<T>(arg => ...)` | `doAnswer(inv -> ...)` |
| Spy | `new Mock<T> { CallBase = true }` | `@Spy` veya `spy(gercekNesne)` |
| Fake | `FakeRepository : IRepository` | in-memory H2, `FakeRepository implements Repository` |
| Strict | `MockBehavior.Strict` | `STRICT_STUBS` (Mockito 2+) |
| Verify all | `mock.VerifyAll()` | `verifyNoMoreInteractions(mock)` |

---

## Test Double Seçim Kılavuzu

```
Bağımlılık türü?
├── Interface / Abstract class → Mock veya Stub (Moq)
├── Concrete EF Core DbContext → InMemory Database
├── Dış HTTP API → Mock<IHttpMessageHandler> veya WireMock
├── Dosya sistemi → Fake (MemoryStream, StringWriter)
└── Gerçek DB gereken test → TestContainers (bkz. Gün 42)

Test neyi doğruluyor?
├── Return değeri → Stub yeter
├── Metod çağrıldı mı? → Mock + Verify
├── Karmaşık durum yönetimi → Fake
└── Gerçek entegrasyon → Integration test (bkz. Gün 41)
```

---

## Antipatternler

### 1. Aşırı Mock — Everything Mocked

```csharp
// YANLIŞ: her şeyi mock'layınca testi ne doğrular?
var mockA = new Mock<IServisA>();
var mockB = new Mock<IServisB>();
var mockC = new Mock<IServisC>();
// Test sadece mock'ların birbiriyle konuştuğunu test ediyor.
// Gerçek davranış hiç test edilmiyor.
```

### 2. Brittle Test — Implementasyon Takip Etme

```csharp
// YANLIŞ: iç metod çağrılarını doğrulamak kırılgan test yaratır
mockServis.Verify(s => s._dahiliYardimciMetod(), Times.Once);
// _dahiliYardimciMetod refactor'da değişirse test kırılır.
// Test public davranışı doğrulamalı, implementasyon detayını değil.

// DOĞRU: dışarı görünen sonucu doğrula
result.Should().Be(beklenenSonuc);
```

### 3. Test Logic — Testte Koşul

```csharp
// YANLIŞ: testte if/loop — test mantığı yanlış olabilir
if (sonuc != null)
{
    sonuc.Baslik.Should().Be("Test");
}
// sonuc null ise assert hiç çalışmaz — hatalı test geçer!

// DOĞRU: önce null kontrolü assert et
sonuc.Should().NotBeNull();
sonuc!.Baslik.Should().Be("Test");
```

---

## Özet Tablo

| Double | Yanıt Verir | Çağrı Doğrular | Durum Tutar | Kullanım |
|--------|------------|----------------|-------------|----------|
| Dummy | Hayır | Hayır | Hayır | Constructor doldurucu |
| Stub | Evet | Hayır | Hayır | Sonuç odaklı test |
| Mock | Evet | Evet | Hayır | Davranış doğrulama |
| Fake | Evet | Hayır | Evet | Karmaşık durum yönetimi |
| Spy | Gerçek | Evet | Gerçek | Gerçek + izleme |

Bir sonraki adım: **FluentAssertions** ile assertion'ları okunabilir yazmak (Gün 40).
