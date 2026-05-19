// GÜN 103 — Data Protection API ve Şifreleme
// IDataProtector: unprotect edebileceğin şifreler (token, cookie, magic link)
// Hashing: tek yönlü — şifre saklamak için (BCrypt, Argon2)

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace Ornekler.gun103;

// --- 1. IDataProtector: magic link, email verify token ---
public class TokenServisi
{
    private readonly IDataProtector _protector;

    public TokenServisi(IDataProtectionProvider provider)
    {
        // ne yapar: "email-verify" amacıyla sınırlı protector — başka amaçla kullanılamaz
        // bunu yazmasaydık: bir amaç için üretilen token başka yerde geçerli olabilirdi
        _protector = provider.CreateProtector("email-verify");
    }

    public string TokenUret(string email)
    {
        // ne yapar: email'i şifreler → URL-safe base64 token
        // bunu yazmasaydık: email adresi token içinde plain-text olurdu
        return _protector.Protect(email);
    }

    public string? TokenCoz(string token)
    {
        try
        {
            // ne yapar: token'ı çözer ve email'i döner
            // bunu yazmasaydık: token manipüle edilse bile fark edemezdik
            return _protector.Unprotect(token);
        }
        catch
        {
            return null; // geçersiz/manipüle edilmiş token
        }
    }
}

// --- 2. TimeLimitedDataProtector: belirli süre geçerli token ---
public class SureLiTokenServisi
{
    private readonly ITimeLimitedDataProtector _protector;

    public SureLiTokenServisi(IDataProtectionProvider provider)
    {
        // ne yapar: 15 dakika sonra geçersiz olan şifreli token
        // bunu yazmasaydık: şifre sıfırlama linkleri sonsuza kadar geçerli olurdu
        _protector = provider
            .CreateProtector("sifre-sifirla")
            .ToTimeLimitedDataProtector();
    }

    public string TokenUret(string kullaniciId)
    {
        return _protector.Protect(kullaniciId, lifetime: TimeSpan.FromMinutes(15));
    }

    public string? TokenCoz(string token)
    {
        try
        {
            // ne yapar: token çözer — 15 dakika geçmişse CryptographicException fırlatır
            // bunu yazmasaydık: süresi geçmiş token hâlâ geçerli sayılırdı
            return _protector.Unprotect(token);
        }
        catch (CryptographicException)
        {
            return null; // süresi geçmiş
        }
    }
}

// --- 3. Şifre hashleme: BCrypt ---
public class SifreServisi
{
    // ne yapar: şifreyi BCrypt ile hash'ler — her seferinde farklı salt
    // bunu yazmasaydık: MD5/SHA1 gibi hızlı hash'ler rainbow table saldırısına açık
    public string Hashle(string sifre)
    {
        // workFactor: 12 → ~250ms — brute force'u yavaşlatır
        return BCrypt.Net.BCrypt.HashPassword(sifre, workFactor: 12);
    }

    public bool Dogrula(string sifre, string hash)
    {
        // ne yapar: girilen şifreyi hash ile karşılaştırır
        // bunu yazmasaydık: şifreleri plain-text veya tersine çevrilebilir şekilde saklardık
        return BCrypt.Net.BCrypt.Verify(sifre, hash);
    }
}

// --- 4. AES-GCM: simetrik şifreleme (veritabanı alanı şifreleme) ---
public class AlanSifreleyici
{
    private readonly byte[] _anahtar;

    public AlanSifreleyici(string anahtarBase64)
    {
        // ne yapar: 256-bit (32 byte) anahtar — appsettings'den gelir, key vault'ta saklanmalı
        // bunu yazmasaydık: şifreleme anahtarı koda gömülü olurdu — güvenlik açığı
        _anahtar = Convert.FromBase64String(anahtarBase64);
    }

    public (byte[] sifrelenmis, byte[] nonce, byte[] tag) Sifrele(string metin)
    {
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 byte
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];    // 16 byte
        var veriler = System.Text.Encoding.UTF8.GetBytes(metin);
        var sifrelenmis = new byte[veriler.Length];

        RandomNumberGenerator.Fill(nonce); // ne yapar: kriptografik güvenli rastgele nonce üretir

        using var aes = new AesGcm(_anahtar, AesGcm.TagByteSizes.MaxSize);
        // ne yapar: veriyi şifreler + authentication tag üretir (manipülasyon tespiti)
        // bunu yazmasaydık: AES-CBC kullanmak zorunda kalırdık — authentication tag yok, padding oracle riski
        aes.Encrypt(nonce, veriler, sifrelenmis, tag);

        return (sifrelenmis, nonce, tag);
    }

    public string Coz(byte[] sifrelenmis, byte[] nonce, byte[] tag)
    {
        var cozulmus = new byte[sifrelenmis.Length];
        using var aes = new AesGcm(_anahtar, AesGcm.TagByteSizes.MaxSize);

        // ne yapar: şifre çözer ve authentication tag doğrular
        // bunu yazmasaydık: manipüle edilmiş veriyi fark edemezdik
        aes.Decrypt(nonce, sifrelenmis, tag, cozulmus);
        return System.Text.Encoding.UTF8.GetString(cozulmus);
    }
}

// Program.cs:
// builder.Services.AddDataProtection()
//     .PersistKeysToAzureBlobStorage(...)    // key'leri Azure'da sakla
//     .ProtectKeysWithAzureKeyVault(...);    // key'leri Azure Key Vault ile koru
