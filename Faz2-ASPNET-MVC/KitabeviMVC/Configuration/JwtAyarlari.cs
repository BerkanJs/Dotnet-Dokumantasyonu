namespace KitabeviMVC.Configuration;

// appsettings.json → "Jwt" bölümüne karşılık gelen sınıf.
// Property isimleri JSON key'leriyle birebir eşleşmeli.
public class JwtAyarlari
{
    // Gizli anahtar — imzalama için kullanılır.
    // Gerçek değer asla appsettings.json'a yazılmaz;
    // ortam değişkeni veya dotnet user-secrets ile gelir.
    public string SecretKey { get; init; } = "";

    // Token kaç dakika geçerli?
    public int ExpiryMinutes { get; init; }

    // Token'ı kim üretiyor?
    public string Issuer { get; init; } = "";
}
