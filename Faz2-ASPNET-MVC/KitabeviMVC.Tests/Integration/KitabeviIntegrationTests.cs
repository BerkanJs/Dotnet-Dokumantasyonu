using FluentAssertions;
using KitabeviMVC.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace KitabeviMVC.Tests.Integration;

// ─────────────────────────────────────────────────────────────────────
// KitabeviWebApplicationFactory — Test ortamı yapılandırması
//
// WebApplicationFactory<Program>: Program.cs'teki tüm konfigürasyonu
// alır, test için özelleştirir.
//
// public partial class Program {} → Program.cs'in sonunda tanımlı.
// Bu olmadan factory Program sınıfını göremez.
//
// Yapılan override'lar:
//   1. SQL Server → InMemory DB (gerçek bağlantı gerekmez)
//   2. Environment = "Testing" (Development'a özgü Hangfire dashboard açılmaz)
// ─────────────────────────────────────────────────────────────────────
public class KitabeviWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // SQL Server DbContext kaydını kaldır
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<KitabeviDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            // InMemory ile değiştir — gerçek DB bağlantısı gerekmez
            // Her test çalıştırmasında ayrı DB → izolasyon
            services.AddDbContext<KitabeviDbContext>(options =>
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        });
    }
}

// ─────────────────────────────────────────────────────────────────────
// Integration Testleri
//
// Unit test: sınıf izole test edilir, pipeline çalışmaz.
// Integration test: gerçek HTTP isteği gider, tüm pipeline çalışır.
//
// TestServer: bellekte çalışan sunucu, port açılmaz.
// HttpClient: TestServer'a istek atar.
//
// Ne test edilir?
//   - Routing doğru çalışıyor mu? (/kitaplar → KitapController.Liste)
//   - Middleware'ler çalışıyor mu? (auth redirect, hata yakalama)
//   - Status code'lar doğru mu? (200, 302, 404)
//
// Not: HTML içeriği test edilmez — view render hatalarından kaçınmak için.
// Status code + header testi yeterli ve kararlı.
// ─────────────────────────────────────────────────────────────────────
public class KitabeviIntegrationTests : IClassFixture<KitabeviWebApplicationFactory>
{
    private readonly HttpClient _client;

    // IClassFixture: factory bir kez oluşturulur, tüm testler paylaşır.
    // Her test için yeni HttpClient → cookie izolasyonu.
    public KitabeviIntegrationTests(KitabeviWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            // false → redirect'leri takip etme, 302 olduğu gibi gelsin
            // true olsaydı /kitaplar/ekle → /hesap/giris → 200 gelirdi,
            // ama biz redirect'in kendisini test etmek istiyoruz
            AllowAutoRedirect = false
        });
    }

    // ─── Public route'lar — auth gerekmez ────────────────────────────

    [Fact]
    public async Task Get_KitapListesi_200Dondurur()
    {
        // Act
        var yanit = await _client.GetAsync("/kitaplar");

        // Assert
        yanit.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_KitapDetay_VarOlanId_200Dondurur()
    {
        // KitapServisi Singleton in-memory — Id=1 her zaman var
        var yanit = await _client.GetAsync("/kitaplar/detay/1");

        yanit.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_KitapDetay_YokOlanId_404Dondurur()
    {
        var yanit = await _client.GetAsync("/kitaplar/detay/9999");

        yanit.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_AnaSayfa_200Dondurur()
    {
        var yanit = await _client.GetAsync("/");

        yanit.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─── Korumalı route'lar — auth gerekir ───────────────────────────

    [Fact]
    public async Task Get_KitapEkleFormu_GirisYapilmamissa_GiriseSayfasinaRedirectEder()
    {
        // [Authorize(Policy = "KitapEkleme")] → giriş yapılmamışsa 302
        var yanit = await _client.GetAsync("/kitaplar/ekle");

        // 302 Found
        yanit.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Location header /hesap/giris'i içermeli
        yanit.Headers.Location?.ToString().Should().Contain("/hesap/giris");
    }

    [Fact]
    public async Task Get_KitapDuzenle_GirisYapilmamissa_GiriseSayfasinaRedirectEder()
    {
        var yanit = await _client.GetAsync("/kitaplar/duzenle/1");

        yanit.StatusCode.Should().Be(HttpStatusCode.Redirect);
        yanit.Headers.Location?.ToString().Should().Contain("/hesap/giris");
    }

    // ─── CSRF koruması ────────────────────────────────────────────────

    [Fact]
    public async Task Post_KitapEkle_GirisYapilmamis_LogineSayfasinaRedirectEder()
    {
        // [Authorize] middleware [ValidateAntiForgeryToken]'dan ÖNCE devreye girer.
        // Giriş yapılmamış kullanıcı AntiForgery kontrolüne ulaşamadan
        // login sayfasına yönlendirilir (302).
        // Bu test pipeline sıralamasını doğrular.
        var formVerisi = new FormUrlEncodedContent(
        [
            new("Baslik",    "Test Kitap"),
            new("Yazar",     "Test Yazar"),
            new("Fiyat",     "50"),
            new("Kategori",  "Roman"),
            new("StokAdedi", "5")
        ]);

        // Act
        var yanit = await _client.PostAsync("/kitaplar/ekle", formVerisi);

        // Assert — [Authorize] önce → 302 Login redirect
        yanit.StatusCode.Should().Be(HttpStatusCode.Redirect);
        yanit.Headers.Location?.ToString().Should().Contain("/hesap/giris");
    }

    [Fact]
    public async Task Post_KitapSil_GirisYapilmamis_LogineSayfasinaRedirectEder()
    {
        // Aynı sebep: [Authorize(Policy = "KitapSilme")] önce çalışır
        var yanit = await _client.PostAsync("/kitaplar/sil/1", new StringContent(""));

        yanit.StatusCode.Should().Be(HttpStatusCode.Redirect);
        yanit.Headers.Location?.ToString().Should().Contain("/hesap/giris");
    }

    // ─── Route yok → 404 ─────────────────────────────────────────────

    [Fact]
    public async Task Get_VarOlmayanRoute_404Dondurur()
    {
        var yanit = await _client.GetAsync("/bu/route/yok");

        yanit.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Content-Type kontrolü ────────────────────────────────────────

    [Fact]
    public async Task Get_KitapListesi_HtmlIcerikDondurur()
    {
        var yanit = await _client.GetAsync("/kitaplar");

        yanit.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
    }
}
