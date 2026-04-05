using KitabeviMVC.Configuration;
using Microsoft.Extensions.Options;

namespace KitabeviMVC.Services;

// ─────────────────────────────────────────────────────────
// IOptions<T> — uygulama başlarken bir kez okunur.
// Singleton servislerde kullanılır.
// Konfigürasyon dosyası değişse bile güncellenmez.
// ─────────────────────────────────────────────────────────
public class TokenServisi
{
    private readonly JwtAyarlari _ayarlar;

    public TokenServisi(IOptions<JwtAyarlari> options)
    {
        _ayarlar = options.Value;
    }

    public string TokenOlustur(string kullaniciAdi)
    {
        // Gerçek projede burada HMAC-SHA256 ile imzalı JWT üretilir.
        // Şimdilik ayarları loglayarak nasıl kullanıldığını gösteriyoruz.
        return $"[Token] Kullanıcı: {kullaniciAdi} | " +
               $"Issuer: {_ayarlar.Issuer} | " +
               $"Süre: {_ayarlar.ExpiryMinutes} dk";
    }
}

// ─────────────────────────────────────────────────────────
// IOptionsMonitor<T> — konfigürasyon dosyası değişince
// uygulama yeniden başlatılmadan güncellenir (hot reload).
// Singleton servislerde IOptions yerine bu kullanılır
// eğer ayarların canlı değişmesi gerekiyorsa.
// ─────────────────────────────────────────────────────────
public class TokenServisiCanlı
{
    private readonly IOptionsMonitor<JwtAyarlari> _monitor;

    public TokenServisiCanlı(IOptionsMonitor<JwtAyarlari> monitor)
    {
        _monitor = monitor;

        // appsettings.json değiştiğinde bu callback tetiklenir.
        _monitor.OnChange(yeniAyarlar =>
            Console.WriteLine($"[Konfigürasyon Değişti] Yeni Issuer: {yeniAyarlar.Issuer}"));
    }

    public string TokenOlustur(string kullaniciAdi)
    {
        // CurrentValue her çağrıda güncel değeri verir.
        var ayarlar = _monitor.CurrentValue;
        return $"[Token] Kullanıcı: {kullaniciAdi} | " +
               $"Issuer: {ayarlar.Issuer} | " +
               $"Süre: {ayarlar.ExpiryMinutes} dk";
    }
}

// ─────────────────────────────────────────────────────────
// IOptionsSnapshot<T> — her HTTP request'inde yeniden okunur.
// Scoped (request bazlı) servislerde kullanılır.
// Singleton servislerle inject edilemez.
// ─────────────────────────────────────────────────────────
public class TokenServisiScoped
{
    private readonly JwtAyarlari _ayarlar;

    public TokenServisiScoped(IOptionsSnapshot<JwtAyarlari> snapshot)
    {
        // Her request geldiğinde snapshot taze değeri okur.
        _ayarlar = snapshot.Value;
    }

    public string TokenOlustur(string kullaniciAdi)
    {
        return $"[Token] Kullanıcı: {kullaniciAdi} | " +
               $"Issuer: {_ayarlar.Issuer} | " +
               $"Süre: {_ayarlar.ExpiryMinutes} dk";
    }
}
