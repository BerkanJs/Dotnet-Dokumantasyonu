# Gün 20 — Authentication & Authorization

---

## 1. İki Farklı Soru — İki Farklı Kavram

Bir kitabevine girdiğini düşün. Kapıda güvenlik görevlisi seni tanıması için kimliğini soruyor. Kimliğini gösterdin — **sen kimsin** sorusu cevaplandı. Bu **Authentication** (kimlik doğrulama).

İçeri girdin, rafları serbestçe gezebiliyorsun. Ama kasiyere geçip faturayı iptal etmek istediğinde görevli "bu işlemi sadece müdür yapabilir" diyor. **Bu işlemi yapma iznim var mı** sorusu cevaplandı. Bu **Authorization** (yetkilendirme).

```
Authentication → Kim olduğunu kanıtla     (kimlik doğrulama)
Authorization  → Ne yapabileceğini kontrol et  (yetkilendirme)
```

Önemli sıra: **önce authentication, sonra authorization.** Kim olduğunu bilmeden neye izin vereceğini bilemezsin.

Spring Security'de bu ayrım `AuthenticationManager` ve `AccessDecisionManager` ile yapılırdı. ASP.NET Core'da aynı sorumluluğu middleware sıralaması üstlenir: `UseAuthentication()` önce gelir, `UseAuthorization()` sonra.

---

## 2. Cookie Authentication — Klasik Web Akışı

Bir kullanıcı giriş formu doldurup "Giriş Yap"a bastığında ne olur?

```
Kullanıcı: email + şifre gönder
    ↓
Sunucu: bilgileri doğrula → doğruysa şifreli cookie oluştur
    ↓
Tarayıcı: cookie'yi sakla, her sonraki istekte otomatik gönder
    ↓
Sunucu: cookie'yi oku, kimin geldiğini anla, isteği işle
```

Cookie yaklaşımı klasik MVC uygulamalarında kullanılır — kullanıcı tarayıcıda oturum açar, kapanana kadar oturum devam eder. Kitabevi gibi admin paneli olan bir MVC projesi için doğal seçimdir.

```
Spring'deki karşılık:
  Spring Security → formLogin() + session yönetimi
  ASP.NET Core   → AddCookie() + .SignInAsync()
```

---

## 3. JWT Bearer — API Odaklı Yaklaşım

Cookie yaklaşımı tarayıcı tabanlı uygulamalar için idealdir. Ama bir mobil uygulama veya başka bir servis senin API'nı çağıracaksa cookie kullanamaz.

Bunun için JWT (JSON Web Token) kullanılır. JWT, sunucunun imzaladığı, içinde kullanıcı bilgileri taşıyan bir token'dır. Tarayıcı yerine mobil uygulama veya başka servis bu token'ı HTTP header'ında taşır:

```
Authorization: Bearer eyJhbGci...
```

Sunucu token'ı alır, imzayı doğrular, içinden kullanıcı bilgilerini okur — cookie veritabanına gitmesine gerek kalmaz.

```
Cookie → Sunucu session'ı saklar, tarayıcı sadece id taşır
JWT    → Sunucuda durum yok, tüm bilgi token'ın içinde
```

---

## 4. JWT Anatomisi — Header.Payload.Signature

JWT görünürde uzun bir string'dir ama üç bölüme ayrılır, nokta ile ayrılmış:

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9
.
eyJ1c2VySWQiOiI0MiIsImVtYWlsIjoiYWxpQGtpdGFiZXZpLmNvbSIsInJvbGUiOiJBZG1pbiIsImV4cCI6MTcxNjQ2MDAwMH0
.
SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c
```

Her bölüm Base64 ile kodlanmıştır (şifreli değil — sadece formatlı). Üç bölüm:

**Header** — hangi algoritma kullanıldı:
```json
{
  "alg": "HS256",
  "typ": "JWT"
}
```

**Payload** — kullanıcı hakkındaki bilgiler (Claims):
```json
{
  "userId": "42",
  "email": "ali@kitabevi.com",
  "role": "Admin",
  "exp": 1716460000
}
```

**Signature** — sunucunun imzası. Header + Payload + gizli anahtar birleştirilerek üretilir. Token değiştirilirse imza bozulur, sunucu reddeder.

Kritik noktar: Payload içeriği **şifrelenmez**, sadece kodlanır. Token'ı ele geçiren payload'ı okuyabilir — bu yüzden JWT içine şifre veya hassas bilgi koyma.

---

## 5. ClaimsPrincipal ve ClaimsIdentity — ASP.NET'in Kullanıcı Modeli

Kullanıcı giriş yaptıktan sonra ASP.NET Core, o kullanıcıyı `ClaimsPrincipal` nesnesi olarak temsil eder. Her action'dan `User` property'si ile erişilir.

```
ClaimsPrincipal → Kullanıcının tamamı (birden fazla kimliği olabilir)
  └─ ClaimsIdentity → Bir kimlik kaynağı (Cookie, JWT, Google vs.)
       └─ Claim → Kimlikle ilgili tek bir bilgi parçası
```

Claim, anahtar-değer çiftinden oluşur:

| ClaimType               | Değer                    | Anlamı           |
|-------------------------|--------------------------|------------------|
| ClaimTypes.Name         | "ali@kitabevi.com"       | Kullanıcı adı    |
| ClaimTypes.Role         | "Admin"                  | Rolü             |
| ClaimTypes.NameIdentifier | "42"                   | Veritabanı id'si |
| "departman"             | "Muhasebe"               | Özel claim       |

Filter konusundan (Gün 19) hatırlarsın: `context.HttpContext.User.Identity?.Name` ile kullanıcı adını okuyorduk. O `User` buradaki `ClaimsPrincipal` nesnesidir.

Spring Security'de `SecurityContextHolder.getContext().getAuthentication()` ile yapılan şeyin karşılığıdır.

---

## 6. [Authorize] — Sayfaları Koru

Controller veya action'a `[Authorize]` eklenince o endpoint giriş gerektiriyor demektir. Giriş yapılmamışsa login sayfasına yönlendirilir.

```csharp
// Hiçbir parametre yok → sadece "giriş yapmış olmak" yeterli.
[Authorize]
public IActionResult ProfilSayfasi() { ... }

// Sadece Admin rolündekiler girebilir.
[Authorize(Roles = "Admin")]
public IActionResult AdminPaneli() { ... }

// Giriş yapılmış olsa da bu endpoint herkese açık.
[AllowAnonymous]
public IActionResult Anasayfa() { ... }
```

`[Authorize(Roles = "Admin")]` basit ama yeterli değil büyük projelerde. "Admin rolü varsa erişebilir" kuralı zamanla "şu rolden birini taşıyorsa veya şu claim varsa veya hesap doğrulanmışsa" gibi karmaşıklaşır. Bunun için Policy kullanılır.

---

## 7. Policy-Based Authorization — Esnek Kural Tanımlama

Policy, bir erişim kuralına verilen isimdir. Kuralı bir kez tanımlarsın, istediğin yerde isimiyle kullanırsın.

```csharp
// Program.cs
builder.Services.AddAuthorization(options =>
{
    // "KitapEkleme" adında bir kural tanımla:
    // Kullanıcının "Editor" veya "Admin" rolünden birini taşıması yeterli.
    options.AddPolicy("KitapEkleme", policy =>
        policy.RequireRole("Editor", "Admin"));

    // "SadeceTurkiye" adında başka bir kural:
    // "ulke" claim'i "TR" değerini taşımalı.
    options.AddPolicy("SadeceTurkiye", policy =>
        policy.RequireClaim("ulke", "TR"));

    // "Onayli" adında bir kural:
    // Giriş yapmış olmalı VE "emailOnaylandi" claim'i "true" olmalı.
    options.AddPolicy("Onayli", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("emailOnaylandi", "true");
    });
});
```

Kullanımı:

```csharp
[Authorize(Policy = "KitapEkleme")]
public IActionResult KitapEkleFormu() { ... }

[Authorize(Policy = "Onayli")]
public IActionResult SiparisVer() { ... }
```

Spring Security'deki `hasRole("ADMIN")`, `hasAuthority("EDIT_BOOK")` gibi ifadelerin karşılığıdır — ama kural tanımları `WebSecurityConfigurerAdapter` yerine `AddAuthorization` içinde toplanır.

---

## 8. Resource-Based Authorization — "Bu Kaydın Sahibi misin?"

Policy yeterli olmadığı durumlar var. Örnek: kullanıcı kendi siparişini görebilir ama başkasınınkini göremez.

```
Policy:           Kullanıcı Admin mi?        → evet/hayır (statik kural)
Resource-based:   Kullanıcı bu siparişin
                  sahibi mi?                 → hangi kayda baktığına göre değişir
```

Bu için `IAuthorizationService` kullanılır — controller içinde manuel olarak yetki kontrolü yapılır:

```csharp
public class SiparisController : Controller
{
    private readonly ISiparisRepository _siparisRepo;
    private readonly IAuthorizationService _authService;

    public SiparisController(
        ISiparisRepository siparisRepo,
        IAuthorizationService authService)
    {
        _siparisRepo = siparisRepo;
        _authService = authService;
    }

    public async Task<IActionResult> Detay(int id)
    {
        var siparis = await _siparisRepo.GetByIdAsync(id);
        if (siparis is null) return NotFound();

        // "SiparisGoruntule" policy'si bir IAuthorizationHandler'a bağlı.
        // Handler içinde: siparis.KullaniciId == User'ın id'si mi? kontrolü yapılır.
        var sonuc = await _authService.AuthorizeAsync(User, siparis, "SiparisGoruntule");

        if (!sonuc.Succeeded)
            return Forbid(); // 403 — giriş yapmış ama yetkisi yok

        return View(siparis);
    }
}
```

`IAuthorizationHandler` ise policy'yi kaynak üzerinde nasıl değerlendireceğini bilir:

```csharp
// "SiparisGoruntuleHandler" → "SiparisGoruntule" policy'sini değerlendiren sınıf.
// Jenerik parametre: <SiparisGoruntuleRequirement, Siparis>
//   → hangi requirement için, hangi resource tipi üzerinde çalışıyor
public class SiparisGoruntuleHandler
    : AuthorizationHandler<SiparisGoruntuleRequirement, Siparis>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SiparisGoruntuleRequirement requirement,
        Siparis siparis) // resource: kontrol edilecek kayıt
    {
        // Kullanıcının id'si (ClaimTypes.NameIdentifier claim'inden)
        var kullaniciId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Siparişin sahibi mi?
        if (siparis.KullaniciId.ToString() == kullaniciId)
            context.Succeed(requirement); // geçti

        // Succeed çağırmazsak: başarısız — Forbid() devreye girer

        return Task.CompletedTask;
    }
}
```

---

## 9. OAuth 2.0 ve OpenID Connect — "Google ile Giriş"

Kullanıcıların kendi şifrelerini sana vermek yerine Google, GitHub, Microsoft gibi bir sağlayıcıya yönlendirilmesi OAuth 2.0 akışıdır.

```
Kullanıcı "Google ile Giriş Yap" tıklar
    ↓
Google giriş sayfasına yönlendirilir
    ↓
Kullanıcı Google'da giriş yapar
    ↓
Google senin uygulamanı onaylar → code döner
    ↓
Uygulamanın sunucusu code ile token alır
    ↓
Token içinden kullanıcı bilgilerini oku, oturum aç
```

**OAuth 2.0** → yetkilendirme protokolü (bir uygulamanın başka kaynaklara erişim izni alması)
**OpenID Connect** → OAuth 2.0 üzerine inşa edilmiş kimlik doğrulama katmanı (kim olduğunu kanıtlama)

Günlük dilde "OAuth ile giriş" derken çoğunlukla OpenID Connect kastedilir.

---

## 10. IdentityServer / Keycloak — Merkezi Kimlik Sunucusu

Büyük projelerde (özellikle mikroservis mimarisinde) her servis kendi authentication mantığını taşımaz. Bunun yerine merkezi bir **kimlik sunucusu** kullanılır.

```
Kullanıcı → Kimlik Sunucusu'nda giriş yapar → Token alır
Kullanıcı → Token ile API'lara gider → Her API token'ı doğrular
```

**IdentityServer** → .NET ekosisteminin popüler kimlik sunucusu. Duende şirketinin ürünü, ücretsiz sürümü var.
**Keycloak** → Red Hat'in açık kaynak kimlik sunucusu. Docker ile hızlı kurulur, Java tabanlıdır ama her platforma token üretir.

Faz 2'deki Kitabevi MVC projesi için bu düzeye gerek yok — cookie authentication yeterli. Mikroservis fazında (Faz 5) bu kavramlar devreye girer.

---

## 11. Dikkat Edilmesi Gerekenler

**Sıra önemli:** `Program.cs`'de `UseAuthentication()` mutlaka `UseAuthorization()`'dan önce gelmelidir. Ters sıra olursa `User` nesnesi boş kalır, tüm `[Authorize]` kontrolleri başarısız olur.

**JWT şifrelenmez:** JWT payload'ı Base64 ile kodlanmıştır ama şifrelenmemiştir. Token'ı ele geçiren içeriği okuyabilir — şifre, kredi kartı, kişisel veri JWT'ye yazılmamalı.

**Token süresini kısa tut:** JWT'nin avantajı sunucuda durum tutmaması — ama bu aynı zamanda token çalınırsa sunucu geçersiz kılamaz. Bu yüzden kısa ömürlü (15–60 dakika) access token + uzun ömürlü refresh token kombinasyonu kullanılır.

**Cookie vs JWT seçimi:** Klasik MVC (tarayıcı + sunucu) → Cookie. API (mobil, SPA, servisler arası) → JWT. İkisi aynı uygulamada bir arada kullanılabilir.

**`[Authorize]` vs `[Authorize(Roles = "Admin")]` vs Policy:** Tek role bağımlı kurallar zamanla value yönetilemez hale gelir. Birden fazla koşul varsa Policy tanımla — okunabilirliği ve bakımı çok daha kolaydır.

---

## 12. Kontrol Soruları

1. Authentication ve Authorization'ın sırası neden önemlidir? `UseAuthorization()` önce çalışırsa ne olur?

2. JWT payload'ı Base64 ile kodlanmış ama şifrelenmemiş. Bu neden önemli bir güvenlik kuralıdır?

3. Cookie authentication ve JWT'nin temel farkı nedir? Kitabevi MVC projesi için hangisi daha uygun? Neden?

4. `[Authorize(Roles = "Admin")]` yerine neden Policy tanımlamak daha iyi bir pratik kabul edilir?

5. Resource-based authorization ne zaman gerekir? `[Authorize]` attribute'u ile neden aynı sonucu elde edemezsin?

6. OAuth 2.0 ile OpenID Connect arasındaki fark nedir? "Google ile Giriş" hangi protokolü kullanır?
