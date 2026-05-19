# Gün 103 — Data Protection API ve Şifreleme Temelleri

---

## Bu Ders Neden Var?

Uygulamanda şifrelenmesi gereken veriler var: e-posta doğrulama token'ı, şifre sıfırlama linki, cookie içeriği, kullanıcıya özel geçici URL'ler. Bunları düz metin olarak saklamak veya göndermek güvenlik açığı.

.NET bunu çözmek için **Data Protection API** sağlıyor. Kendi şifreleme algoritmanı yazmana gerek yok — framework hazır, güvenli, key yönetimi dahil.

Ama şifreleme ile hashing farklı şeyler. Şifreyi geri çözebilirsin, hash'i çözemezsin. Hangi veri için hangisi kullanılır — bunu bilmek kritik.

---

## Şifreleme vs Hashing — Temel Fark

Bu ikisi sürekli karıştırılıyor. Net ayıralım:

### Şifreleme (Encryption)

Veriyi bir anahtarla karıştırırsın, aynı anahtarla geri çözersin. **İki yönlü** — orijinal veriye dönebilirsin.

**Analoji:** Kasaya para koydun, şifreyi biliyorsun. Şifreyi girince parayı geri alırsın.

**Ne zaman kullan:**
- Veriyi sonra tekrar okuman lazım: token, cookie, API key, kredi kartı numarası (PCI standartları gereği)
- E-posta doğrulama linki — link içindeki userId'yi geri çözmek zorundasın

### Hashing

Veriyi tek yönlü bir fonksiyondan geçirirsin. Sonucu görürsün ama **geriye dönemezsin.** Orijinal veri kaybolur.

**Analoji:** Etle kıyma yaptın. Kıymadan eti geri oluşturamazsın.

**Ne zaman kullan:**
- Şifre saklama — şifrenin kendisini bilmene gerek yok, sadece "girilenle eşleşiyor mu?" sorusuna cevap ver
- Veri bütünlüğü kontrolü — dosya hash'i değişmişse dosya bozulmuş

| | Şifreleme | Hashing |
|---|---|---|
| Yön | İki yönlü (encrypt ↔ decrypt) | Tek yönlü (hash → geri dönemez) |
| Anahtar var mı? | Evet | Hayır (salt var ama anahtar değil) |
| Kullanım | Token, cookie, hassas veri saklama | Şifre saklama, bütünlük kontrolü |
| Aynı girdi = aynı çıktı? | Evet (aynı key ile) | Hayır (salt ile her seferinde farklı hash) |

---

## Data Protection API — .NET'in Built-in Şifreleme Sistemi

### Ne İşe Yarar?

ASP.NET Core'un kendi iç mekanizmalarında (cookie authentication, antiforgery token, TempData) şifreleme için kullandığı sistem. Sen de aynı sistemi kendi verilerini şifrelemek için kullanabilirsin.

**Neden kendi şifreleme kodunu yazma:**
- Kriptografi zor — bir bit hata = güvenlik açığı
- Key yönetimi, rotasyon, storage — hepsini sen düşünmek zorunda kalırsın
- Data Protection API bunları hazır veriyor

### IDataProtector — Temel Kullanım

```csharp
public class TokenService
{
    private readonly IDataProtector _protector;

    public TokenService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("EmailConfirmation");
        // "EmailConfirmation" → purpose string
        // ne yapar → bu amaç için ayrı bir key türetir
        // neden önemli → "EmailConfirmation" ile şifrelenen veri
        //   "PasswordReset" protector'ı ile ÇÖZÜLEMEz (amaç izolasyonu)
        // bunu yazmasaydık → tüm şifrelemeler aynı key'i kullanır,
        //   e-posta token'ı ile şifre reset token'ı aynı key ile çözülür (güvenlik riski)
    }

    public string GenerateToken(int userId)
    {
        var payload = $"{userId}:{DateTime.UtcNow:O}";
        return _protector.Protect(payload);
        // ne yapar → payload'ı şifreler, base64 string döner
        // çıktı: "CfDJ8N..." gibi uzun bir string (URL'de veya e-postada gönderilir)
        // bunu yazmasaydık → userId düz metin olarak gider, herkes okuyabilir ve değiştirebilir
    }

    public int? ValidateToken(string token)
    {
        try
        {
            var payload = _protector.Unprotect(token);
            // ne yapar → şifreli metni çözer, orijinal payload'ı döner
            var parts = payload.Split(':');
            return int.Parse(parts[0]);
        }
        catch (CryptographicException)
        {
            // Token geçersiz — bozulmuş, manipüle edilmiş veya farklı key ile oluşturulmuş
            return null;
            // neden exception → şifresi çözülemeyen veri = güvenilmez, reddet
        }
    }
}
```

### Purpose String — Amaç İzolasyonu

Purpose string, şifreleme anahtarlarını birbirinden ayırır. Farklı amaçlar için farklı key türetilir.

```csharp
var emailProtector = provider.CreateProtector("EmailConfirmation");
var resetProtector = provider.CreateProtector("PasswordReset");
var cookieProtector = provider.CreateProtector("CookieAuth");

var encrypted = emailProtector.Protect("user:42");

emailProtector.Unprotect(encrypted);   // ✓ çalışır — aynı purpose
resetProtector.Unprotect(encrypted);   // ✗ CryptographicException — farklı purpose
// ne yapar → bir protector'ın şifrelediği veriyi başka protector çözemez
// neden önemli → e-posta token'ını manipüle edip şifre reset olarak kullanamazsın
```

**Alt amaçlar (sub-purpose) da olabilir:**
```csharp
var protector = provider.CreateProtector("Orders", "Receipts", "2026");
// ne yapar → iç içe purpose zinciri, çok spesifik izolasyon
// kullanım alanı → tenant bazlı veya yıl bazlı key ayrımı
```

---

## TimeLimitedDataProtector — Süreli Token

E-posta doğrulama linki 24 saat geçerli olsun — sonra çalışmasın:

```csharp
public class EmailTokenService
{
    private readonly ITimeLimitedDataProtector _protector;

    public EmailTokenService(IDataProtectionProvider provider)
    {
        _protector = provider
            .CreateProtector("EmailConfirmation")
            .ToTimeLimitedDataProtector();
        // ne yapar → normal protector'ı süreli protector'a dönüştürür
    }

    public string CreateToken(int userId)
    {
        return _protector.Protect(
            userId.ToString(),
            lifetime: TimeSpan.FromHours(24));
        // ne yapar → şifreli token oluşturur, 24 saat sonra otomatik geçersiz olur
        // süre token'ın İÇİNDE — Redis/DB'de expire takibi gerekmez
        // bunu yazmasaydık → token sonsuza kadar geçerli kalır (güvenlik riski)
    }

    public int? ValidateToken(string token)
    {
        try
        {
            var payload = _protector.Unprotect(token);
            return int.Parse(payload);
        }
        catch (CryptographicException)
        {
            return null;  // geçersiz veya süresi dolmuş — ikisi de aynı exception
        }
    }
}
```

**Ne zaman kullan:**
- E-posta doğrulama linki (24 saat)
- Şifre sıfırlama linki (1-2 saat)
- Geçici indirme URL'si (15 dakika)
- Tek kullanımlık davet linki

---

## Key Ring Yönetimi — Anahtarlar Nerede Saklanır?

Data Protection API arka planda şifreleme anahtarları (key ring) yönetir. Bu anahtarlar dosya sisteminde, Redis'te veya cloud key vault'ta saklanabilir.

### Varsayılan Davranış

```
Development'ta → %LOCALAPPDATA%\ASP.NET\DataProtection-Keys\ klasörü
Docker/Container → container silinince key'ler kaybolur! (sorun)
```

**Sorun:** Container yeniden başlarsa key'ler değişir → eski token'lar çözülemez → kullanıcı "link geçersiz" hatası alır.

### Kalıcı Key Storage

```csharp
// Dosya sistemi (en basit):
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/keys"))
    .SetApplicationName("KitapApp");
// ne yapar → key'leri /keys klasörüne yazar, container restart'ta kaybolmaz
// SetApplicationName → birden fazla uygulama aynı key'leri paylaşabilir
// bunu yazmasaydık → her restart'ta yeni key üretilir, eski token'lar geçersiz olur

// Redis (distributed — birden fazla instance):
builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys")
    .SetApplicationName("KitapApp");
// ne yapar → key'ler Redis'te saklanır
// neden → 3 instance aynı key'i kullanmalı, yoksa bir instance'ın şifrelediğini diğeri çözemez

// Azure Key Vault (production-grade):
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(blobClient)
    .ProtectKeysWithAzureKeyVault(keyIdentifier, credential);
// ne yapar → key'ler Azure Blob'da, şifreleme anahtarı Key Vault'ta
// en güvenli → key'lere erişim Azure RBAC ile kontrol edilir
```

### Key Rotasyonu

Data Protection API varsayılan olarak her 90 günde yeni key üretir. Eski key'ler hâlâ okunabilir (unprotect) ama yeni veriler yeni key ile şifrelenir.

```csharp
builder.Services.AddDataProtection()
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
// ne yapar → 90 günde bir yeni key oluşturulur
// eski key'ler silinmez — eski token'lar hâlâ çözülebilir
// neden rotasyon → bir key ele geçirilse bile sadece 90 günlük veri risk altında
```

---

## Password Hashing — Şifre Nasıl Saklanır?

Şifreyi asla şifreleme (encryption) ile saklama — hash'le. Çünkü:
- Şifreleme geri çözülebilir → DB'ye erişen biri tüm şifreleri çözer
- Hash geri çözülemez → DB'ye erişse bile şifreleri okuyamaz

### ASP.NET Core Identity — IPasswordHasher

```csharp
// Identity kullanıyorsan otomatik:
var hasher = new PasswordHasher<Kullanici>();

// Hash oluştur (kayıt sırasında):
var hash = hasher.HashPassword(user, "GizliSifre123");
// ne yapar → PBKDF2 algoritması ile hash üretir (salt dahil)
// çıktı: "AQAAAAIAAYagAAAAEM..." — geri çözülemez
// her çağrıda farklı hash üretir (random salt sayesinde)

// Doğrulama (giriş sırasında):
var result = hasher.VerifyHashedPassword(user, storedHash, "GizliSifre123");
// result: Success, Failed, veya SuccessRehashNeeded
// ne yapar → girilen şifreyi aynı salt ile hash'ler, DB'dekiyle karşılaştırır
// SuccessRehashNeeded → eski algoritma ile hash'lenmiş, yeni algoritmayla güncelle
```

### PBKDF2 vs BCrypt vs Argon2

| Algoritma | Varsayılan | Güvenlik | Performans |
|-----------|-----------|----------|------------|
| **PBKDF2** | ASP.NET Core Identity varsayılanı | İyi | Hızlı (CPU-based) |
| **BCrypt** | Yaygın, kanıtlanmış | Çok iyi | Yavaş (bilerek) |
| **Argon2** | Modern standart (Password Hashing Competition kazananı) | En iyi | Memory-hard (GPU saldırısına dayanıklı) |

**Neden yavaş olsun istiyoruz?** Saldırgan saniyede milyonlarca hash denemesi yapıyor (brute-force). Hash yavaşsa → deneme sayısı düşer → şifre kırılması zorlaşır.

```csharp
// BCrypt kullanmak istersen (NuGet: BCrypt.Net-Next):
var hash = BCrypt.Net.BCrypt.HashPassword("GizliSifre123", workFactor: 12);
// workFactor → zorluk seviyesi, artırınca hashing yavaşlar
// 12 → yaklaşık 250ms per hash (brute-force'u çok yavaşlatır)

var isValid = BCrypt.Net.BCrypt.Verify("GizliSifre123", hash);
```

---

## Low-Level Kriptografi — Ne Zaman Gerekir?

Data Protection API çoğu senaryoyu çözer. Ama bazen daha düşük seviye kontrol lazım:

### AesGcm — Simetrik Şifreleme (Encrypt + Authentication)

```csharp
// Ne zaman → başka sistemle veri paylaşımı, özel format, Data Protection yetersiz
using var aes = new AesGcm(key, tagSize: 16);
// key: 256-bit (32 byte) — güvenli rastgele üretilmeli
// AesGcm → hem şifreler hem kimlik doğrulama (authenticated encryption)

var nonce = new byte[12];              // her şifrelemede benzersiz olmalı
RandomNumberGenerator.Fill(nonce);
var ciphertext = new byte[plaintext.Length];
var tag = new byte[16];                // authentication tag

aes.Encrypt(nonce, plaintext, ciphertext, tag);
// ne yapar → plaintext'i şifreler + tag üretir (veri bozulmuşsa anlaşılır)

aes.Decrypt(nonce, ciphertext, tag, decrypted);
// ne yapar → tag doğruysa çözer, değilse exception (veri manipüle edilmiş)
```

**Ne zaman AesGcm kullan:**
- Başka platform/dil ile şifreli veri paylaşımı (Java, Python ile uyumlu standart)
- Özel şifreleme formatı gereksinimi
- Data Protection API'nin key ring yönetimi istemiyorsan

**Ne zaman KULLANMA:**
- Çoğu uygulama senaryosu → Data Protection API yeterli ve daha güvenli (key yönetimi dahil)
- Şifre saklama → hash kullan, şifreleme değil

### RSA — Asimetrik Şifreleme

```csharp
// Ne zaman → public key ile şifrele, private key ile çöz
// Kullanım: dijital imza, anahtar değişimi, sertifika
using var rsa = RSA.Create(2048);
var encrypted = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
var decrypted = rsa.Decrypt(encrypted, RSAEncryptionPadding.OaepSHA256);
// ne yapar → public key herkese verilebilir, sadece private key sahibi çözebilir
// ne zaman → JWT token imzalama, API key doğrulama, servisler arası güvenli iletişim
```

---

## Doğru Aracı Seç — Karar Tablosu

| Senaryo | Doğru araç | Yanlış araç |
|---------|-----------|-------------|
| E-posta doğrulama token'ı | Data Protection (TimeLimited) | Hash (geri çözemezsin) |
| Şifre saklama | Hash (PBKDF2/BCrypt/Argon2) | Encryption (geri çözülebilir = risk) |
| Cookie şifreleme | Data Protection (otomatik) | Manuel AES (gereksiz karmaşıklık) |
| Başka sisteme şifreli veri gönderme | AesGcm / RSA | Data Protection (key ring portatif değil) |
| JWT imzalama | RSA / HMAC | Data Protection (standart dışı) |
| Dosya bütünlüğü kontrolü | SHA256 hash | Encryption (gereksiz) |

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de:
- Şifre Identity ile hash'leniyor (PBKDF2) — bu doğru
- Ama e-posta doğrulama, şifre sıfırlama token'ları nasıl üretiliyor? Identity zaten Data Protection kullanıyor ama sen farkında değilsin
- Key storage düşünülmemiş → container restart'ta token'lar geçersiz olabilir

50K kullanıcıda: Redis'te key storage, key rotasyonu, süreli token'lar — bunlar production zorunluluğu.

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| Data Protection (temel) | Identity otomatik kullanıyor, yeterli | Key storage ve rotasyon yapılandır |
| Key storage (Redis/Vault) | Dosya sistemi yeterli | Redis veya Azure Key Vault zorunlu |
| TimeLimitedDataProtector | Süreli token'lar için kullan | Zorunlu — token süresi güvenlik |
| Password hashing (PBKDF2) | Identity varsayılanı yeterli | BCrypt/Argon2 düşünülebilir |
| Low-level crypto (AES/RSA) | Gereksiz | Özel entegrasyon varsa |

---

## Kontrol Soruları

1. Şifreleme ile hashing arasındaki temel fark nedir? Şifreyi neden hash'leriz, şifrelemeyiz?
2. Data Protection API'deki "purpose string" ne işe yarar? Purpose farklı olursa ne olur?
3. TimeLimitedDataProtector ne zaman kullanılır? Süre bilgisi nerede saklanır?
4. Container restart'ta key'ler kaybolursa ne olur? Nasıl çözülür?
5. Key rotasyonu nedir? Eski key ile şifrelenmiş veri hâlâ çözülebilir mi?
6. PBKDF2, BCrypt ve Argon2 arasındaki fark nedir? Neden yavaş algoritma istiyoruz?
7. AesGcm'deki "authentication tag" ne işe yarar?
