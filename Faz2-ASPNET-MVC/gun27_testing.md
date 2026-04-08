# Gün 27 — Testing: Unit Test ve Integration Test

---

## 1. Neden Test Yazılır?

Kod çalışıyor gibi görünebilir. Ama "çalışıyor gibi görünmek" ile "doğru çalıştığı kanıtlanmış" arasındaki fark test katmanıdır.

```
Test yok → değişiklik yaptın → bir şeyleri bozup bozmadığını bilmiyorsun
Test var  → değişiklik yaptın → 3 saniyede öğreniyorsun
```

**Somut faydalar:**

- **Regresyon koruması:** Yeni özellik eklerken eski işlevselliği bozmadığını kanıtlar.
- **Tasarım baskısı:** Test yazması zor kod, gerçekte de kötü tasarlanmış koddur. Test yazılabilirlik, sınıfların iyi ayrıştırıldığının göstergesidir.
- **Canlı dokümantasyon:** Test adları, kodun ne yaptığını açıklar — comment'ten daha güvenilir.
- **Güvenli refactoring:** Davranışı bozmadan iç yapıyı değiştirebilirsin.

---

## 2. Test Piramidi

```
         /\
        /  \          E2E (End-to-End)
       / UI \         → Selenium, Playwright
      /──────\        → Yavaş, pahalı, kırılgan
     /        \
    / Entegras-\      Integration Test
   / yon Test   \     → WebApplicationFactory, TestServer
  /──────────────\    → Orta hız, gerçek HTTP
 /                \
/   Unit Test      \  → xUnit + Moq
/────────────────────\ → Hızlı, izole, çok sayıda

Kural: En çok unit test, az integration test, çok az E2E.
```

**Bu günün kapsamı:** Unit test + Integration test. E2E ayrı bir konudur.

---

## 3. .NET Test Ekosistemi

```
Test framework: xUnit       → en yaygın, NUnit / MSTest alternatifleri var
Mock kütüphanesi: Moq       → bağımlılıkları sahte nesnelerle değiştir
Assert kütüphanesi: FluentAssertions → okunabilir assertion'lar
Integration: Microsoft.AspNetCore.Mvc.Testing → gerçek HTTP pipeline'ı test et
```

**Neden xUnit?**

- Her test metodunu ayrı class instance'ında çalıştırır → test izolasyonu
- `[Fact]` (parametresiz), `[Theory]` + `[InlineData]` (parametreli) ayrımı temiz
- .NET ekibinin kendi projelerinde kullandığı framework

**Paket kurulumu (test projesi için):**

```xml
<PackageReference Include="xunit" Version="2.9.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.*" />
<PackageReference Include="Moq" Version="4.20.*" />
<PackageReference Include="FluentAssertions" Version="6.12.*" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.*" />
```

---

## 4. Test Projesi Yapısı

```
KitabeviMVC.Tests/
├── KitabeviMVC.Tests.csproj
├── Services/
│   ├── KitapServisiTests.cs          → iş mantığı unit testleri
│   └── CachedKitapServisiTests.cs    → cache decorator unit testleri
├── Controllers/
│   └── KitapControllerTests.cs       → controller unit testleri
└── Integration/
    └── KitabeviIntegrationTests.cs   → gerçek HTTP pipeline testleri
```

Test projesi ana projeye **proje referansı** ile bağlanır (`<ProjectReference>`), dependency olarak değil.

---

## 5. Temel Kavramlar: Arrange — Act — Assert

Her unit test üç bölümden oluşur:

```csharp
[Fact]
public void HepsiniGetir_BosListeDegilse_KitapDondurur()
{
    // Arrange — test ortamını hazırla, bağımlılıkları kur
    var servis = new KitapServisi();

    // Act — test edilen davranışı tetikle
    var sonuc = servis.HepsiniGetir();

    // Assert — sonuç beklenenle örtüşüyor mu?
    sonuc.Should().NotBeEmpty();
}
```

**AAA disiplini neden önemli?**

```
Testin ne yaptığı 3 bölüme bakarak anlaşılır.
Test başarısız olunca "Arrange mı yanlış? Act mı? Assert mı?" diye ayrıştırabilirsin.
```

---

## 6. Fake, Stub, Mock — Fark Nedir?

Test edilen kod dışa bağımlıdır (DB, cache, dış API). Bu bağımlılıkları gerçek sistemle değil sahte nesnelerle değiştiririz:

```
Fake   → gerçek ama basitleştirilmiş implementasyon (in-memory liste gibi)
Stub   → belirli çağrılar için sabit değer döndürür, davranışı kontrol etmiyoruz
Mock   → hem sabit değer döndürür hem de "bu metod kaç kez çağrıldı?" diye sorabiliriz
```

**Moq ile mock oluşturma:**

```csharp
// IKitapServisi'nin gerçek implementasyonu yerine mock kullan
var mockServis = new Mock<IKitapServisi>();

// "HepsiniGetir() çağrılınca şunu döndür" davranışını tanımla
mockServis
    .Setup(s => s.HepsiniGetir())
    .Returns(new List<KitapListeViewModel> { /* ... */ });

// Mock'tan interface instance'ı al
var servis = mockServis.Object;
```

---

## 7. KitapServisi Unit Testleri

`KitapServisi` dışa bağımlı değil (in-memory liste). Bu yüzden mock gerekmez — doğrudan new'lenebilir.

**Test senaryoları:**

```
HepsiniGetir:
  ✓ Başlangıçta kitap listesi boş değil
  ✓ Dönen liste alfabetik sıralı

KategoriyeGoreGetir:
  ✓ Var olan kategori için doğru kitapları döndürür
  ✓ Yok olan kategori için boş liste döndürür

BulById:
  ✓ Var olan ID için doğru kitabı döndürür
  ✓ Yok olan ID için null döndürür

Ekle:
  ✓ Kitap eklendikten sonra HepsiniGetir listede görünür
  ✓ Eklenen kitabın ID'si > 0

Guncelle:
  ✓ Var olan kitap güncellenir, true döner
  ✓ Yok olan ID için false döner

Sil:
  ✓ Var olan kitap silinir, true döner
  ✓ Silinen kitap artık listede yok
  ✓ Yok olan ID için false döner

BaslikVarMi:
  ✓ Mevcut başlık için true döner
  ✓ Yok olan başlık için false döner
  ✓ haricId ile kendi kendini hariç tutar (güncelleme senaryosu)
```

Bkz. [Tests/Services/KitapServisiTests.cs](KitabeviMVC.Tests/Services/KitapServisiTests.cs)

---

## 8. CachedKitapServisi Unit Testleri

`CachedKitapServisi` iki bağımlılığa sahip: `IKitapServisi` ve `IMemoryCache`. İkisi de mock'lanır.

**Test edilmesi gerekenler:**

```
HepsiniGetir — cache hit:
  ✓ Cache'te veri varsa IKitapServisi.HepsiniGetir() hiç çağrılmaz
  ✓ Cache'ten gelen veri döndürülür

HepsiniGetir — cache miss:
  ✓ Cache'te veri yoksa IKitapServisi.HepsiniGetir() 1 kez çağrılır
  ✓ Dönen veri cache'e yazılır

Ekle:
  ✓ Ekle sonrasında cache.Remove TumKitaplarKey ile çağrılır
  ✓ Ekle sonrasında KategoriKey ile de Remove çağrılır

BaslikVarMi:
  ✓ Her zaman IKitapServisi.BaslikVarMi'ye delege eder (mock doğrulama)
  ✓ Cache'e hiç yazılmaz / okunmaz
```

**IMemoryCache mock'lama — dikkat:**

`IMemoryCache.TryGetValue` extension metod değil, interface metodudur; doğrudan mock'lanabilir. Ancak `_cache.Set(...)` ve `GetOrCreate(...)` extension metoddur — bunlar mock'lanamaz. Test sırasında ya gerçek `MemoryCache` kullanılır ya da `TryGetValue`'yu kontrol etmek için `MockBehavior.Strict` ile sadece çağrılan metodlar mock'lanır.

```
Pratik öneri: IMemoryCache için gerçek MemoryCache instance'ı kullan
(new MemoryCache(new MemoryCacheOptions())) — harici bağımlılık yok, hızlı, güvenilir.
Mock yerine gerçek implementasyon kullanmak "state-based test" denir.
```

Bkz. [Tests/Services/CachedKitapServisiTests.cs](KitabeviMVC.Tests/Services/CachedKitapServisiTests.cs)

---

## 9. KitapController Unit Testleri

Controller test edilirken `IKitapServisi` mock'lanır, HTTP pipeline başlatılmaz (bu integration test konusu).

**Dikkat:** Controller `ControllerContext` gerektirir. Mock olmadan `TempData`, `ViewData`, `User` gibi property'ler null gelir.

```csharp
// Controller'a geçerli bir ControllerContext ver
var controller = new KitapController(mockServis.Object, mockLogger.Object, mockAuth.Object);
controller.ControllerContext = new ControllerContext
{
    HttpContext = new DefaultHttpContext()
};
```

**Test senaryoları:**

```
Liste (GET):
  ✓ 200 OK + View döner
  ✓ View model IReadOnlyList<KitapListeViewModel> tipinde

Detay (GET, var olan ID):
  ✓ 200 OK + View döner
  ✓ View model doğru ID'ye sahip

Detay (GET, yok olan ID):
  ✓ 404 NotFound döner

Ekle POST (geçerli model):
  ✓ KitapServisi.Ekle() 1 kez çağrılır
  ✓ RedirectToAction(Detay) döner

Ekle POST (çakışan başlık):
  ✓ KitapServisi.Ekle() çağrılmaz
  ✓ View tekrar döner (ModelState hatası var)
```

Bkz. [Tests/Controllers/KitapControllerTests.cs](KitabeviMVC.Tests/Controllers/KitapControllerTests.cs)

---

## 10. [Theory] ve [InlineData] — Parametreli Testler

Aynı davranışı farklı girdilerle test etmek için her input için ayrı `[Fact]` yazmak yerine:

```csharp
// Tek test metodu, üç farklı girdi ile çalışır
[Theory]
[InlineData("Roman",  2)]   // Roman kategorisinde 2 kitap var
[InlineData("Tarih",  2)]   // Tarih kategorisinde 2 kitap var
[InlineData("Kisisel", 1)]  // Kişisel kategorisinde 1 kitap var
[InlineData("Yok",    0)]   // Olmayan kategoride 0 kitap var
public void KategoriyeGoreGetir_DogruKitapSayisiniDondurur(string kategori, int beklenen)
{
    var servis = new KitapServisi();
    var sonuc = servis.KategoriyeGoreGetir(kategori);
    sonuc.Should().HaveCount(beklenen);
}
```

---

## 11. Integration Test — WebApplicationFactory

Unit test: sınıfları izole test eder, HTTP pipeline çalışmaz.
Integration test: gerçek HTTP isteği gönderilir, middleware, routing, controller, servis hepsi çalışır — ama gerçek DB yerine test ortamı kullanılır.

```
Gerçek HTTP isteği
       ↓
TestServer (in-memory, port açılmaz)
       ↓
Middleware pipeline (auth, routing, cache, filters)
       ↓
Controller → Servis → (test ortamına göre yapılandırılmış bağımlılıklar)
       ↓
HTTP yanıtı → test assertion'ları
```

**WebApplicationFactory:**

```csharp
// Program.cs'teki uygulama konfigürasyonunu alır, test için özelleştirir.
public class KitabeviWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Gerçek DB bağlantısını kaldır, in-memory ile değiştir
            // veya mevcut servisleri test stub'larıyla override et
        });
    }
}
```

**Test senaryoları:**

```
GET /kitaplar:
  ✓ 200 OK döner
  ✓ Yanıt body'si kitap listesi içerir

GET /kitaplar/detay/1:
  ✓ 200 OK döner

GET /kitaplar/detay/9999:
  ✓ 404 döner

GET /kitaplar/ekle (giriş yapılmamış):
  ✓ 302 → /hesap/giris?ReturnUrl=... yönlendirmesi

POST /kitaplar/ekle (CSRF token eksik):
  ✓ 400 döner (AntiForgery doğrulaması)
```

Bkz. [Tests/Integration/KitabeviIntegrationTests.cs](KitabeviMVC.Tests/Integration/KitabeviIntegrationTests.cs)

---

## 12. FluentAssertions — Okunabilir Assertion'lar

```csharp
// xUnit Assert (standart)
Assert.NotNull(sonuc);
Assert.Equal(5, sonuc.Count);
Assert.True(sonuc.Any(k => k.Baslik == "1984"));

// FluentAssertions (okunabilir)
sonuc.Should().NotBeNull();
sonuc.Should().HaveCount(5);
sonuc.Should().Contain(k => k.Baslik == "1984");

// Hata mesajı da çok daha açık:
// "Expected collection to contain item matching k => k.Baslik == "1984" but no item was found."
```

**Sık kullanılan assertion'lar:**

```csharp
// Değer kontrolü
deger.Should().Be(42);
deger.Should().BeGreaterThan(0);
deger.Should().BeNull();
deger.Should().NotBeNull();

// Koleksiyon
liste.Should().NotBeEmpty();
liste.Should().HaveCount(3);
liste.Should().Contain(x => x.Id == 1);
liste.Should().BeInAscendingOrder(x => x.Baslik);

// String
metin.Should().StartWith("Gün");
metin.Should().Contain("cache");
metin.Should().BeNullOrEmpty();

// Action result (controller testleri)
sonuc.Should().BeOfType<ViewResult>();
sonuc.Should().BeOfType<RedirectToActionResult>()
     .Which.ActionName.Should().Be("Detay");
sonuc.Should().BeOfType<NotFoundResult>();
```

---

## 13. Moq Doğrulama — "Bu Metod Çağrıldı mı?"

Moq sadece sahte değer döndürmez, metod çağrılarını da doğrular:

```csharp
// Arrange
var mockServis = new Mock<IKitapServisi>();
mockServis.Setup(s => s.Ekle(It.IsAny<KitapFormViewModel>())).Returns(99);

// Act
controller.Ekle(model);

// Assert — Ekle metodunun tam olarak 1 kez çağrıldığını doğrula
mockServis.Verify(s => s.Ekle(It.IsAny<KitapFormViewModel>()), Times.Once);

// Remove hiç çağrılmadı mı?
mockCache.Verify(c => c.Remove(It.IsAny<object>()), Times.Never);

// Belirli parametre ile çağrıldı mı?
mockServis.Verify(s => s.BaslikVarMi("1984", 0), Times.Once);
```

---

## 14. Test İsimlendirme Kuralı

Test adı 3 soruyu yanıtlamalı:

```
{TestedMethod}_{Senaryo}_{BeklenenSonuc}

HepsiniGetir_CacheHit_ServisiHicCagirmaz
HepsiniGetir_CacheMiss_ServisibirKezCagirirVeCacheYazar
BulById_YokOlanId_NullDondurur
Ekle_GecerliModel_CacheInvalidateEder
BaslikVarMi_MevcutBaslik_TrueDondurur
```

Bu kurala uymak:
- Test raporu okunduğunda ne test edildiği anlaşılır
- Başarısız test adından sorunun nerede olduğu anlaşılır

---

## 15. Test Organizasyon Tavsiyeleri

**Her public metod için en az bir test:**

```
Happy path (başarılı senaryo)  → mutlaka
Edge case (sınır değer)        → önemli
Error case (hata yolu)         → kritik iş mantığı için
```

**Testler bağımsız olmalı:**

```
Test A'nın çalışması Test B'ye bağlı olmamalı.
Her test kendi Arrange'ini yapar.
Shared state → flaky (güvenilmez) testlere yol açar.
```

**Mock'u aşırı kullanma:**

```
Her bağımlılığı mock'lamak zorunlu değil.
Harici sistem yok, hızlı → gerçek implementasyonu kullan.
Mock ne zaman? → DB, dış API, saat, rastgelelik gibi dışa bağımlı şeyler için.
```

---

## 16. Kontrol Soruları

1. Unit test ve integration test arasındaki temel fark nedir? Hangisini ne zaman tercih edersin?

2. `[Fact]` ile `[Theory]` arasındaki fark nedir? `[InlineData]` ne işe yarar?

3. Moq'ta `Setup` ile `Verify` arasındaki fark nedir?

4. `CachedKitapServisi`'ni test ederken `IMemoryCache`'i mock'lamak yerine gerçek `MemoryCache` kullanmanın avantajı nedir?

5. `WebApplicationFactory` ne sağlar? Integration testte neden `TestServer` kullanılır?

6. `BaslikVarMi` metodunun neden cache'lenmediğini test perspektifinden açıkla — bu metod için yazacağın test nasıl görünür?
