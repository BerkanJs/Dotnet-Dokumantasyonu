# Gün 41 — Integration Testing: WebApplicationFactory

## Unit Test Neden Yetmez?

Birim testler bileşenleri izole test eder — mükemmel ama eksik.
Gerçek hatalar çoğunlukla bileşenler **arası** geçişlerde çıkar:

**Gerçek senaryo — fintech şirketi:**
Unit testler %95 coverage ile tümü geçiyor. Ama production'da:
- `[Authorize]` attribute controller'a eklendi
- Middleware sırası yanlış — authentication, authorization'dan sonra çalışıyor
- JWT token geçerli ama 401 dönüyor

Bu hata unit testte **görünmez** — controller mock'lanmış, pipeline test edilmemiyor.
Integration test gerçek HTTP pipeline'ı çalıştırsaydı, ilk commit'te yakalanırdı.

---

## Test Piramidi

```
        /\
       /  \
      / E2E\        ← Selenium/Playwright (yavaş, kırılgan, pahalı)
     /──────\
    /        \
   /Integration\   ← WebApplicationFactory, TestContainers (orta hız)
  /────────────\
 /              \
/   Unit Tests   \  ← xUnit + Moq + InMemory (hızlı, izole)
/────────────────\
```

**Ne zaman integration test?**
- HTTP routing, middleware, model binding test edilecekse
- Controller → Service → Repository → DB zinciri doğrulanacaksa
- Authentication/Authorization flow test edilecekse
- API kontrakt (status code, Content-Type, Location header) doğrulanacaksa

---

## WebApplicationFactory Nedir?

`Microsoft.AspNetCore.Mvc.Testing` paketi, gerçek ASP.NET Core uygulamasını
**in-process** olarak başlatır — gerçek HTTP sunucusu başlatmadan.

```
Test kodu
    │
    ▼
HttpClient (test factory'den)
    │
    ▼ HTTP isteği (in-process, network yok)
    │
    ▼
ASP.NET Core Pipeline
  ├── UseRouting
  ├── UseAuthentication
  ├── UseAuthorization
  ├── MapControllers
    │
    ▼
KitapApiController
    │
    ▼
KitapEkleCommandHandler (MediatR)
    │
    ▼
KitabeviDbContext (InMemory — gerçek SQL değil)
```

Gerçek pipeline, gerçek DI container, gerçek routing — sadece DB InMemory.

---

## TestWebApplicationFactory Kurulumu

`KitabeviMVC.Tests/Infrastructure/TestWebApplicationFactory.cs`:

```csharp
public class TestWebApplicationFactory : WebApplicationFactory<Program>
// WebApplicationFactory<Program>: Program sınıfını entry point olarak kullanır.
// Program: KitabeviMVC/Program.cs — public partial class Program { } olmalı.
// "partial": test assembly Program sınıfını görebilmeli.
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseEnvironment("Testing"): IHostEnvironment.EnvironmentName = "Testing"
        // Program.cs'de: if (!app.Environment.IsEnvironment("Testing")) → Hangfire atlanır.
        // Neden önemli: Hangfire SQL Server connection string ister — test ortamında yok.

        builder.ConfigureServices(services =>
        {
            // 1. Gerçek DbContext'i kaldır
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<KitabeviDbContext>));
            // SingleOrDefault: Program.cs'de kayıtlı SQL Server DbContext konfigürasyonu.

            if (descriptor is not null)
                services.Remove(descriptor);
                // Remove: gerçek SQL Server bağlantısını sil.

            // 2. InMemory ile değiştir
            services.AddDbContext<KitabeviDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            // Guid.NewGuid(): her WebApplicationFactory örneği kendi DB'sini alır.
            // IClassFixture ile paylaşıldığında: tüm testler aynı DB'yi kullanır.
            // Izolasyon için: her teste kendi factory vermek daha güvenli ama yavaş.

            // 3. Seed veri ekle
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KitabeviDbContext>();

            db.Database.EnsureCreated();
            // EnsureCreated: InMemory DB şemasını oluşturur — migration gereksiz.

            if (!db.Kitaplar.Any())
            // Idempotent kontrol: seed birden fazla çalışırsa duplicate olmasın.
            {
                db.Kitaplar.AddRange(
                    new Kitap { Id = 1, Baslik = "Clean Code",    Fiyat = 89.90m,  StokAdedi = 10 },
                    new Kitap { Id = 2, Baslik = "Domain Driven", Fiyat = 120m,    StokAdedi = 5  },
                    new Kitap { Id = 3, Baslik = "Sapiens",       Fiyat = 75m,     StokAdedi = 20 }
                );
                db.SaveChanges();
            }
        });
    }
}
```

---

## IClassFixture — Factory Paylaşımı

```csharp
// KitabeviMVC.Tests/Integration/KitapApiControllerTests.cs
public class KitapApiControllerTests : IClassFixture<TestWebApplicationFactory>
// IClassFixture<T>: xUnit bu fixture'ı test class ömrünce paylaşır.
// TestWebApplicationFactory: bir kez oluşturulur — tüm testler aynı factory'yi kullanır.
// Her testte factory yeniden oluşturulursa: ASP.NET Core başlatma overhead'i tekrar eder.
{
    private readonly HttpClient _client;
    private const string BaseUrl = "/api/v1.0/kitaplar";

    public KitapApiControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        // CreateClient(): gerçek HttpClient döner, gerçek HTTP çağrısı yapar.
        // BaseAddress otomatik ayarlanır: http://localhost/
    }
```

**IClassFixture vs IAsyncLifetime:**
- `IClassFixture<T>`: factory class ömrünce yaşar, tüm testlerle paylaşılır
- `IAsyncLifetime`: her test için async setup/teardown — daha izole ama yavaş

---

## Test Örnekleri — HTTP Kontrakt

### GET All

```csharp
[Fact]
public async Task GetAll_SeedVeriyle_200VeListeDoner()
{
    // ─── Act ──────────────────────────────────────────────────────────────
    var yanit = await _client.GetAsync(BaseUrl);

    // ─── Assert ───────────────────────────────────────────────────────────
    yanit.StatusCode.Should().Be(HttpStatusCode.OK);
    // .Be(HttpStatusCode.OK): 200 status code.
    // Bunu yazmassaydık: 500 error da "başarılı" sayılabilir.

    var kitaplar = await yanit.Content
        .ReadFromJsonAsync<List<KitapListeDto>>();
    // ReadFromJsonAsync<T>: JSON body'yi deserialize et.
    // System.Net.Http.Json paketi — .NET 5+ dahili.

    kitaplar.Should().NotBeNull();
    kitaplar.Should().HaveCount(3);
    // Seed verisi 3 kitap ekledi — kontrakt: 3 eleman dönmeli.
}

[Fact]
public async Task GetAll_ContentType_JsonDoner()
{
    var yanit = await _client.GetAsync(BaseUrl);

    yanit.Content.Headers.ContentType!.MediaType
        .Should().Be("application/json");
    // MediaType: "application/json; charset=utf-8" değil, sadece type kısmı.
    // API kontrakt: HTML veya XML değil, JSON döndürülmeli.
}
```

### GET by ID

```csharp
[Fact]
public async Task GetById_GecerliId_200VeKitapDoner()
{
    var yanit = await _client.GetAsync($"{BaseUrl}/1");

    yanit.StatusCode.Should().Be(HttpStatusCode.OK);

    var kitap = await yanit.Content.ReadFromJsonAsync<KitapListeDto>();
    kitap.Should().NotBeNull();
    kitap!.Baslik.Should().Be("Clean Code");
    // Seed'deki ID=1 kitabın başlığı — kontrakt doğrulama.
}

[Fact]
public async Task GetById_YanlisId_404Doner()
{
    var yanit = await _client.GetAsync($"{BaseUrl}/9999");

    yanit.StatusCode.Should().Be(HttpStatusCode.NotFound);
    // 404: kayıt bulunamadığında doğru HTTP status dönüyor mu?
    // Controller: return NotFound() → 404
    // Eksik: return Ok(null) → 200 ama body null → kırık kontrakt.
}
```

### POST — Yeni Kayıt

```csharp
[Fact]
public async Task Post_GecerliKitap_201VeLocationDoner()
{
    // ─── Arrange ──────────────────────────────────────────────────────────
    var yeniKitap = new KitapEkleCommand(
        Baslik:     "Yeni Test Kitabı",
        Yazar:      "Test Yazarı",
        Fiyat:      49.90m,
        Kategori:   "Test",
        StokAdedi:  5
    );

    // ─── Act ──────────────────────────────────────────────────────────────
    var yanit = await _client.PostAsJsonAsync(BaseUrl, yeniKitap);
    // PostAsJsonAsync: objeyi JSON'a serialize edip POST atar.

    // ─── Assert ───────────────────────────────────────────────────────────
    yanit.StatusCode.Should().Be(HttpStatusCode.Created);
    // 201 Created: RESTful kontrakt — yeni kayıt oluşturuldu.
    // 200 OK döndürmek: yaygın hata — REST spec ihlali.

    yanit.Headers.Location.Should().NotBeNull();
    // Location header: "/api/v1.0/kitaplar/4" — oluşturulan kaynağın URL'si.
    // CreatedAtAction(...) kullanıldığında otomatik set edilir.
    // Eksik Location header: client yeni kaynağı nerede bulacağını bilemiyor.
}

[Fact]
public async Task Post_BosBaslik_400Doner()
{
    var gecersizKitap = new { Baslik = "", Fiyat = 50m };
    // Boş başlık: model validation hatası.

    var yanit = await _client.PostAsJsonAsync(BaseUrl, gecersizKitap);

    yanit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    // 400: validation hatası.
    // [Required] attribute → ModelState invalid → 400 otomatik.
}

[Fact]
public async Task Post_MukererBaslik_409Doner()
{
    // Aynı başlıkta kitap eklemeye çalış.
    var mukerrer = new KitapEkleCommand("Clean Code", "Birisi", 50m, "Yazılım", 1);
    // "Clean Code": seed'de zaten var.

    var yanit = await _client.PostAsJsonAsync(BaseUrl, mukerrer);

    yanit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    // 409 Conflict: duplicate kayıt.
    // Controller: iş kuralı ihlali → 409 döndürmeli.
}
```

### DELETE

```csharp
[Fact]
public async Task Delete_GecerliId_204Doner()
{
    var yanit = await _client.DeleteAsync($"{BaseUrl}/2");

    yanit.StatusCode.Should().Be(HttpStatusCode.NoContent);
    // 204 No Content: silme başarılı, body yok.
    // 200 OK döndürmek teknik olarak doğru ama REST best practice: 204.
}

[Fact]
public async Task Delete_YanlisId_404Doner()
{
    var yanit = await _client.DeleteAsync($"{BaseUrl}/9999");

    yanit.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

---

## Test İzolasyonu Sorunu

`IClassFixture` ile tüm testler aynı DB'yi paylaşır.
POST testi veri ekler, DELETE testi veri siler — test sırası sonucu etkiler.

**Çözüm 1: Her test kendi factory'sini alır (yavaş ama izole)**

```csharp
public class IzoleKitapTests : IAsyncLifetime
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new TestWebApplicationFactory();
        _client  = _factory.CreateClient();
        // Her test yeni factory → yeni InMemory DB → tamamen izole.
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
```

**Çözüm 2: Her test benzersiz veri kullanır**

```csharp
// Suffix ile unique başlık — diğer testlerin verisine dokunmaz
var baslik = $"Test Kitabı {Guid.NewGuid()}";
var yanit = await _client.PostAsJsonAsync(BaseUrl, new { Baslik = baslik });
```

---

## Authentication Test Edilmesi

```csharp
// JWT korumalı endpoint testi
public class AuthTestleri : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task KorunanEndpoint_TokenSiz_401Doner()
    {
        var yanit = await _client.GetAsync("/api/v1.0/admin/kitaplar");

        yanit.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // [Authorize] attribute etkin mi? — pipeline testi.
        // Unit testte: controller mock'lanır, [Authorize] çalışmaz.
    }

    [Fact]
    public async Task KorunanEndpoint_GecerliToken_200Doner()
    {
        // Test JWT token oluştur
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtOlustur("admin"));

        var yanit = await _client.GetAsync("/api/v1.0/admin/kitaplar");

        yanit.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string TestJwtOlustur(string rol)
    {
        // Gerçek proje: Microsoft.IdentityModel.Tokens ile test JWT üret.
        // Test ortamı için: appsettings.Testing.json'da test secret key kullan.
        return "test.jwt.token";
    }
}
```

---

## appsettings.Testing.json

`KitabeviMVC.Tests/Infrastructure/appsettings.Testing.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

**Neden Warning?**
Test çıktısı EF Core SQL sorguları ile dolmamalı.
`Information` level'da EF Core her sorguyu loglar → 100 test = 1000 satır log.
`Warning`: sadece beklenmedik durumlar görünür.

`.csproj`'a CopyToOutputDirectory ekle:
```xml
<None Update="Infrastructure\appsettings.Testing.json">
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</None>
```

---

## Java Spring MockMvc Karşılaştırması

| Kavram | .NET WebApplicationFactory | Java Spring MockMvc |
|--------|---------------------------|---------------------|
| Test sunucu | In-process (ağ yok) | In-process (ağ yok) |
| Kurulum | `WebApplicationFactory<Program>` | `@WebMvcTest(Controller.class)` |
| HTTP client | `factory.CreateClient()` | `MockMvc.perform(...)` |
| DI override | `ConfigureServices()` | `@MockBean` |
| GET | `GetAsync(url)` | `get(url)` |
| POST JSON | `PostAsJsonAsync(url, obj)` | `post(url).contentType(JSON).content(json)` |
| Status assert | `.StatusCode.Should().Be(OK)` | `.andExpect(status().isOk())` |
| Body assert | `ReadFromJsonAsync<T>()` + FluentAssertions | `.andExpect(jsonPath("$.alan").value(...))` |
| Auth | `DefaultRequestHeaders.Authorization` | `with(user(...).roles(...))` |

---

## Özet

WebApplicationFactory'nin değeri şu soruyu yanıtlamaktır:
"Tüm bileşenler birlikte doğru çalışıyor mu?"

| Test türü | Ne test eder | Yavaş mu? |
|-----------|-------------|-----------|
| Unit test | Tek bileşen izole | Hayır (ms) |
| Integration (InMemory) | Pipeline + DI + routing | Biraz (~1s) |
| Integration (TestContainers) | Gerçek DB + pipeline | Daha fazla (~5s) |
| E2E | Tarayıcı + UI + backend | Çok (>30s) |

Integration test yatırımının en yüksek getirisi: **API kontrakt testleri**.
Bir endpoint 200 yerine 201, null yerine 404 dönüyorsa — integration test yakalar, unit test yakalamaz.

Bir sonraki adım: **TestContainers** ile gerçek SQL Server'a karşı test (Gün 42).
