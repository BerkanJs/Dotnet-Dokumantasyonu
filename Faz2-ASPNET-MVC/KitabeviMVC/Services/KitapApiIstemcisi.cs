using System.Net;
using System.Net.Http.Json;

namespace KitabeviMVC.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Senaryo: Dış bir fiyat karşılaştırma API'sine bağlanan Typed Client.
//
// Gerçek hayat örneği:
//   Kitabevimiz ISBN bazında dış tedarikçilerden fiyat alıyor:
//   "Bu kitabı biz 120₺'ye satıyoruz. Tedarikçi A 95₺ veriyor mu?"
//   Bu servis o soruyu yanıtlar — dış API ile konuşur.
//
// Neden Typed Client (IHttpClientFactory değil)?
//   → HTTP mantığı bu sınıfın içinde kapsüllenmiş — controller veya servis
//     HttpClient'ı hiç görmez, sadece domain metodlarını çağırır.
//   → Mock edilebilir: testlerde IKitapApiIstemcisi mock'lanır, gerçek HTTP çıkmaz.
//   → BaseAddress, timeout, default headers merkezi Program.cs'te yapılandırılır.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Dış kitap fiyat API'sine erişim sözleşmesi.</summary>
public interface IKitapApiIstemcisi
{
    /// <summary>ISBN bazında tedarikçi fiyatlarını getirir. Kitap bulunamazsa null döner.</summary>
    Task<List<DisFiyatDto>?> FiyatKarsilastirAsync(string isbn);

    /// <summary>Tedarikçide stok var mı? API erişilemiyor veya bulunamazsa false döner.</summary>
    Task<bool> StokSorgulaAsync(string isbn);
}

/// <summary>Bir tedarikçiden gelen fiyat bilgisi.</summary>
public record DisFiyatDto(
    string  Tedarikci,  // "Tedarikçi A", "Tedarikçi B" ...
    decimal Fiyat,      // tedarikçinin teklif fiyatı
    bool    Stokta      // bu tedarikçide stok var mı?
);

/// <summary>IKitapApiIstemcisi implementasyonu — gerçek HTTP çağrıları yapar.</summary>
public class KitapApiIstemcisi : IKitapApiIstemcisi
{
    private readonly HttpClient _client;
    // HttpClient: DI tarafından inject edilir — Program.cs'te AddHttpClient<KitapApiIstemcisi> ile yapılandırılır.
    // new HttpClient() KULLANILMAZ: socket exhaustion ve DNS stale sorunları (Gün 28).
    // Bunu inject etmeseydin: her çağrıda new HttpClient() açılır → prodüksiyon çökme riski.

    private readonly ILogger<KitapApiIstemcisi> _logger;
    // Logger: API hatalarını ve yavaş yanıtları loglamak için.

    public KitapApiIstemcisi(HttpClient client, ILogger<KitapApiIstemcisi> logger)
    {
        _client = client;
        _logger = logger;
        // Constructor injection: bağımlılıklar dışarıdan gelir → test edilebilir, değiştirilebilir.
    }

    public async Task<List<DisFiyatDto>?> FiyatKarsilastirAsync(string isbn)
    // async Task<T?>: I/O işlemi (HTTP) → async zorunlu; "?" → bulunamazsa null dönebilir.
    {
        if (string.IsNullOrWhiteSpace(isbn))
            throw new ArgumentException("ISBN boş olamaz.", nameof(isbn));
        // Guard clause: geçersiz girdi API'ye ulaşmadan erken hata verir.
        // Bunu yazmassaydık API "bad request" dönebilir, hata mesajı belirsiz olurdu.

        try
        {
            var yanit = await _client.GetAsync($"fiyatlar/{isbn}");
            // BaseAddress Program.cs'te set edilmiş: https://api.tedarikci.com/v1/
            // Tam URL: https://api.tedarikci.com/v1/fiyatlar/978-0-13-110362-7
            // Bunu tam URL olarak yazmak: BaseAddress değişirse her satırı güncellememiz gerekir.

            if (yanit.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("ISBN {Isbn} için dış fiyat bulunamadı.", isbn);
                return null;
                // 404: kitap bu API'de yok — exception değil null dön.
                // yanit.EnsureSuccessStatusCode() yazmak: 404'ü de exception'a çevirirdi — yanlış.
            }

            yanit.EnsureSuccessStatusCode();
            // 4xx/5xx durumlarında HttpRequestException fırlatır.
            // Bunu yazmassaydık 500 yanıtını başarılı sanıp boş liste deserialize etmeye çalışırdık.

            var fiyatlar = await yanit.Content.ReadFromJsonAsync<List<DisFiyatDto>>();
            // ReadFromJsonAsync: yanıt gövdesini otomatik JSON deserialize eder.
            // Manuel yol: var json = await yanit.Content.ReadAsStringAsync(); JsonSerializer.Deserialize(json);
            // ReadFromJsonAsync daha kısa ve hata toleranslı (Content-Type kontrolü yapar).

            return fiyatlar;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Dış fiyat API'sine erişilemedi. ISBN: {Isbn}", isbn);
            // Warning seviyesi: API geçici olarak erişilemiyor — kritik hata değil, tekrar denenecek.
            // Error yazsaydık: her geçici ağ kesintisi alarm tetikler — false positive.
            return null;
            // Null dön: çağıran kod "API erişilemiyor" durumunu null ile anlayabilir.
            // Bunu yazmasaydık exception üst katmana fırlatılır, kullanıcıya 500 dönülebilirdi.
        }
    }

    public async Task<bool> StokSorgulaAsync(string isbn)
    {
        try
        {
            var yanit = await _client.GetAsync($"stok/{isbn}");

            if (!yanit.IsSuccessStatusCode)
                return false;
            // API erişilemiyor veya hata → stok yok say (güvenli taraf: stok varsay'ma).
            // Bunu EnsureSuccessStatusCode() yapıp exception fırlatsaydık:
            // stok sorgulama her API hatasında kullanıcı işlemini durdurur — UX kötü.

            var sonuc = await yanit.Content.ReadFromJsonAsync<StokYanitiDto>();
            return sonuc?.Stokta ?? false;
            // null-coalescing: ReadFromJsonAsync null dönerse → false (stok yok).
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stok sorgulama başarısız. ISBN: {Isbn}", isbn);
            return false;
            // Her türlü hatada false: "bilemiyorum" → "stok yok" gibi davran.
        }
    }
}

// Stok yanıtı için iç DTO — sadece bu servis kullanır, dışa açılmasına gerek yok.
file record StokYanitiDto(bool Stokta);
// "file" modifier: sadece bu dosyada görünür, başka dosyalar bu sınıfı kullanamaz.
// Bunu "public" yapmak: iç implementasyon detayı dışa sızar, coupling artar.
