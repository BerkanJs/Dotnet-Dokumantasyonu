using KitabeviMVC.Tests.Infrastructure;
using System.Text;
using System.Text.Json;

namespace KitabeviMVC.Tests.Integration;

// ─────────────────────────────────────────────────────────────────────────────
// KitapApiController Integration Testleri
//
// Bu testler gerçek HTTP pipeline'ını test eder:
//   Routing → Model Binding → Filter → Controller → Servis → InMemory DB
//
// Unit test ile YAKALANMAYAN durumlar burada yakalanır:
//   ✓ Routing yanlışlığı ([Route] attribute hatası)
//   ✓ Model binding sorunu (JSON property adı uyumsuzluğu)
//   ✓ DI kaydı eksikliği ("No service for type...")
//   ✓ [ApiController] validation pipe'ı çalışıyor mu?
//
// Seed verisi: TestWebApplicationFactory'de tanımlı (3 kitap, Id: 1, 2, 3)
// ─────────────────────────────────────────────────────────────────────────────
public class KitapApiControllerTests : IClassFixture<TestWebApplicationFactory>
// IClassFixture<T>: factory bir kez oluşturulur, tüm testler paylaşır (performans).
// Her test için yeni factory: her test 3-5 saniye setup süresi → yavaş.
{
    private readonly HttpClient _client;
    private const string BaseUrl = "/api/v1.0/kitaplar";
    // API versioning: Program.cs'te "api/v{version:apiVersion}/kitaplar" route'u tanımlı.
    // v1.0: varsayılan versiyon — yazmak zorundayız çünkü URL path versioning kullanılıyor.

    public KitapApiControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        // CreateClient(): in-process transport ile HTTP client oluşturur.
        // Gerçek Kestrel/network yok — test hızlı, port çakışması yok.
    }

    // ─── GET /api/v1.0/kitaplar ───────────────────────────────────────────────

    [Fact]
    public async Task GET_TumKitaplar_200DonderVeListeBosDegil()
    {
        // ─── Act ─────────────────────────────────────────────────────────────
        var yanit = await _client.GetAsync(BaseUrl);

        // ─── Assert ──────────────────────────────────────────────────────────
        yanit.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Kitap listesi endpoint'i 200 OK döndürmeli.");

        var icerik = await yanit.Content.ReadAsStringAsync();
        icerik.Should().NotBeNullOrEmpty();
        // JSON içeriği boş değil mi? Ham string kontrolü — JSON parse etmeden hızlı kontrol.
    }

    [Fact]
    public async Task GET_TumKitaplar_ContentType_Json()
    {
        // ─── Act ─────────────────────────────────────────────────────────────
        var yanit = await _client.GetAsync(BaseUrl);

        // ─── Assert ──────────────────────────────────────────────────────────
        yanit.Content.Headers.ContentType?.MediaType
             .Should().Be("application/json",
                because: "API JSON döndürmeli ([ApiController] bunu otomatik ayarlar).");
    }

    // ─── GET /api/v1.0/kitaplar/{id} ─────────────────────────────────────────

    [Fact]
    public async Task GET_MevcutId_200DonderVeDogruKitap()
    {
        // Seed verisi: Id=1 "Clean Code" mevcut.
        // ─── Act ─────────────────────────────────────────────────────────────
        var yanit = await _client.GetAsync($"{BaseUrl}/1");

        // ─── Assert ──────────────────────────────────────────────────────────
        yanit.StatusCode.Should().Be(HttpStatusCode.OK);

        var json    = await yanit.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(json);
        // JsonDocument.Parse: tam deserialize yerine hafif JSON okuma.

        var baslik = jsonDoc.RootElement.GetProperty("baslik").GetString();
        baslik.Should().Be("Clean Code",
            because: "Seed verisinde Id=1 'Clean Code' olarak tanımlı.");
    }

    [Fact]
    public async Task GET_YanlisId_404Doner()
    {
        // ─── Act ─────────────────────────────────────────────────────────────
        var yanit = await _client.GetAsync($"{BaseUrl}/99999");

        // ─── Assert ──────────────────────────────────────────────────────────
        yanit.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "Olmayan kitap için 404 dönmeli.");
        // Unit test ile test edilemez: routing + controller'ın NotFound() çağrısı pipeline içinde.
    }

    // ─── POST /api/v1.0/kitaplar ─────────────────────────────────────────────

    [Fact]
    public async Task POST_GecerliBody_201DonderVeLocationHeader()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var yeniKitap = new
        {
            baslik    = "Yeni Test Kitabı",
            yazar     = "Test Yazarı",
            fiyat     = 75.0,
            kategori  = "Test",
            stokAdedi = 5
        };
        // Anonim nesne: DTO sınıfı oluşturmak gerekmez — hızlı test verisi.

        var json    = JsonSerializer.Serialize(yeniKitap);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        // StringContent: body'yi string olarak gönderir.
        // Content-Type: "application/json" — [ApiController] bu olmadan 415 Unsupported Media Type dönebilir.

        // ─── Act ─────────────────────────────────────────────────────────────
        var yanit = await _client.PostAsync(BaseUrl, content);

        // ─── Assert ──────────────────────────────────────────────────────────
        yanit.StatusCode.Should().Be(HttpStatusCode.Created,
            because: "Başarılı oluşturmada 201 Created dönmeli (200 OK değil).");

        yanit.Headers.Location.Should().NotBeNull(
            because: "201 Created yanıtı Location header içermeli: yeni kaynağın URL'i.");
        // CreatedAtAction(nameof(Detay), new { id = yeniId }, ...) bu header'ı otomatik ekler.
    }

    [Fact]
    public async Task POST_BoşBaslik_400Doner()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        var gecersizKitap = new
        {
            baslik    = "",       // boş — [Required] validation hatası
            yazar     = "Yazar",
            fiyat     = 50.0,
            kategori  = "Roman",
            stokAdedi = 3
        };

        var content = new StringContent(
            JsonSerializer.Serialize(gecersizKitap),
            Encoding.UTF8,
            "application/json");

        // ─── Act ─────────────────────────────────────────────────────────────
        var yanit = await _client.PostAsync(BaseUrl, content);

        // ─── Assert ──────────────────────────────────────────────────────────
        yanit.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "[ApiController] otomatik model validation uygular; geçersiz model 400 döner.");
        // Unit test ile test edilemez: [ApiController] attribute'u pipeline seviyesinde çalışır.
    }

    [Fact]
    public async Task POST_AyniBaslikTekrar_409Doner()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        // Seed verisi: "Clean Code" zaten var.
        var tekrarKitap = new
        {
            baslik    = "Clean Code",  // duplicate başlık
            yazar     = "Başka Yazar",
            fiyat     = 90.0,
            kategori  = "Yazılım",
            stokAdedi = 2
        };

        var content = new StringContent(
            JsonSerializer.Serialize(tekrarKitap),
            Encoding.UTF8,
            "application/json");

        // ─── Act ─────────────────────────────────────────────────────────────
        var yanit = await _client.PostAsync(BaseUrl, content);

        // ─── Assert ──────────────────────────────────────────────────────────
        yanit.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "Aynı başlıkta kitap varsa 409 Conflict dönmeli.");
    }

    // ─── DELETE /api/v1.0/kitaplar/{id} ──────────────────────────────────────

    [Fact]
    public async Task DELETE_MevcutId_204Doner()
    {
        // ─── Arrange ────────────────────────────────────────────────────────
        // Önce yeni bir kitap oluştur — seed verisini silmek diğer testleri etkileyebilir.
        var yeniKitap = new
        {
            baslik    = "Silinecek Kitap",
            yazar     = "Y",
            fiyat     = 10.0,
            kategori  = "Test",
            stokAdedi = 1
        };
        var postYanit = await _client.PostAsync(BaseUrl,
            new StringContent(JsonSerializer.Serialize(yeniKitap), Encoding.UTF8, "application/json"));
        postYanit.StatusCode.Should().Be(HttpStatusCode.Created);

        // Location header'dan yeni kitabın URL'ini al
        var locationUrl = postYanit.Headers.Location!.ToString();
        // Location: /api/v1.0/kitaplar/4 — ID'yi URL'den alıyoruz.

        // ─── Act ─────────────────────────────────────────────────────────────
        var deleteYanit = await _client.DeleteAsync(locationUrl);

        // ─── Assert ──────────────────────────────────────────────────────────
        deleteYanit.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "Başarılı silmede body yok → 204 No Content.");
        // 200 değil 204: REST standardı — silme başarılıysa body döndürülmez.
    }

    [Fact]
    public async Task DELETE_YanlisId_404Doner()
    {
        // ─── Act ─────────────────────────────────────────────────────────────
        var yanit = await _client.DeleteAsync($"{BaseUrl}/99999");

        // ─── Assert ──────────────────────────────────────────────────────────
        yanit.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
