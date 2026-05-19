# Gün 111 — Identity Server, Keycloak ve OpenID Connect (Mikroservis Auth)

---

## Bu Ders Neden Önemli?

Gün 106'da OAuth 2.0 ve JWT'yi gördük. Tek bir uygulamada kullanıcı doğrulama için yeterli. Ama gerçek dünyada işler daha karmaşık:

- 5 farklı microservice var, hepsi auth yapmak zorunda
- Web + mobil + 3. parti developer'lar — hepsi farklı flow gerektiriyor
- Şirket içinde "Single Sign-On" (SSO) — bir kez giriş yap, tüm uygulamalara erişebil
- Identity yönetimi merkezi olmalı — her serviste ayrı kullanıcı tablosu olmasın

Bunların hepsini çözmek için **merkezi bir Identity Provider (IdP)** gerekiyor. Bugün IdP konseptini, OpenID Connect protokolünü ve iki popüler IdP'yi (IdentityServer, Keycloak) ele alacağız.

---

## OAuth 2.0 vs OpenID Connect — Karıştırılan İki Protokol

Bu iki protokol sürekli karıştırılıyor. Net ayıralım:

### OAuth 2.0 — Authorization Protocol

OAuth bir **yetkilendirme** protokolü. "Bu uygulama benim Google Drive'ıma erişebilir mi?" sorusuna cevap verir.

OAuth sana **access token** verir. Access token diyor ki: "Bu token taşıyana, X kaynağına erişme yetkisi verilmiştir."

**Önemli detay:** Access token kullanıcıyı tanımlamak için DEĞİL. Sadece erişim yetkisini taşır. Token içinde kullanıcı adı, email gibi bilgiler olabilir ama bu opsiyonel.

### OpenID Connect (OIDC) — Authentication Layer

OIDC, OAuth 2.0 üzerine kurulan bir **kimlik doğrulama** katmanı. "Bu kullanıcı kim?" sorusuna cevap verir.

OIDC sana **id_token** verir. Id_token tam olarak kullanıcıyı tanımlar — email, isim, profil resmi gibi standart claim'ler içerir.

### id_token vs access_token

| | id_token | access_token |
|---|----------|--------------|
| **Amaç** | "Bu kullanıcı kim?" | "Bu kaynağa erişim yetkisi var" |
| **İçerik** | Kullanıcı bilgisi (sub, email, name) | Yetki bilgisi (scope, audience) |
| **Format** | Her zaman JWT | Çoğunlukla JWT (opaque da olabilir) |
| **Tüketici** | Client uygulaması (frontend) | Resource API (backend) |
| **Doğrulama** | Client doğrular | API doğrular |

**Örnek senaryo:**

Kullanıcı `app.com`'a Google ile giriş yapıyor. OIDC ile:

1. `app.com` Google'a yönlendirir
2. Kullanıcı Google'da giriş yapar, izin verir
3. Google iki token döner:
   - **id_token:** "Bu Berkan, email'i berkan@gmail.com" → `app.com` frontend'ine "merhaba Berkan" diyor
   - **access_token:** "Bu token taşıyan Google Drive'a erişebilir" → `app.com` Drive API'sına istek atarken kullanıyor

Sadece OAuth olsaydı id_token olmazdı — `app.com` kullanıcının kim olduğunu access_token'dan tahmin etmeye çalışırdı (kurallı standart değil). OIDC bunu standartlaştırdı.

### Kullanım Kuralı

- **Kullanıcı giriş yaptırıyorsan** → OIDC (id_token alıyorsun)
- **API erişimi yetkilendiriyorsan** → OAuth (access_token alıyorsun)
- Çoğu modern senaryo: ikisi birden (OIDC, OAuth'un üzerinde çalıştığı için tek istekte ikisi gelir)

---

## Identity Provider (IdP) — Merkezi Auth Sunucusu

### Neden Merkezi IdP?

5 microservice'in olduğu bir senaryo düşün:
- CatalogService (ürün listesi)
- OrderService (sipariş)
- PaymentService (ödeme)
- UserService (kullanıcı yönetimi)
- NotificationService (bildirim)

Her serviste kendi kullanıcı tablosu, kendi auth logic'i olsa:
- 5 ayrı login ekranı (kabus)
- 5 ayrı şifre yönetimi (kullanıcı şifre unutuyor, her birinde reset)
- Şifre politikası değiştirmek istiyorsun → 5 ayrı yerde
- MFA eklemek istiyorsun → 5 ayrı entegrasyon

**Çözüm:** Tek bir auth sunucusu — Identity Provider. Tüm servisler IdP'ye yönlendirir.

```
Kullanıcı → app.com → "Giriş yap" → IdP'ye yönlendirir
                                     ↓
                     Kullanıcı IdP'de şifresini girer
                                     ↓
                     IdP token üretir, app.com'a yollar
                                     ↓
app.com bu token ile microservice'lere istek atar
   token "kullanıcı Berkan, scope: orders" bilgisini taşıyor
```

Tüm microservice'ler tek bir token'a güvenir. Kullanıcı yönetimi tek bir yerde.

### IdP'nin Yaptığı İşler

- Kullanıcı kayıt, giriş, şifre yönetimi
- Token üretme ve doğrulama
- MFA (multi-factor auth)
- Social login (Google, Facebook ile giriş)
- SSO (Single Sign-On) — bir kez giriş, tüm uygulamalar
- Audit log (kim ne zaman giriş yaptı)
- Şifre politikaları, hesap kilitleme
- Role/permission yönetimi

Bunları kendin yazmak yerine hazır IdP kullanmak akıllıca. Birkaç popüler seçenek var.

---

## Duende IdentityServer — .NET Native

**IdentityServer** .NET ekosisteminin en bilinen IdP'si. Eski adı sadece "IdentityServer4", şimdi Duende IdentityServer.

### Özellikler

- .NET projesi — ASP.NET Core üstüne kurulu
- Tam OAuth 2.0 ve OIDC desteği
- ASP.NET Core Identity ile entegre (kullanıcı tablosu)
- Esnek — her şey programatik olarak yapılandırılır
- Lisans: Duende ticari lisans (10K $/yıl üstü gelirde ücretli)

### Minimum Kurulum

```csharp
// Program.cs — IdentityServer projesi:
builder.Services.AddIdentityServer()
    .AddInMemoryClients(Config.Clients)
    .AddInMemoryApiResources(Config.ApiResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddDeveloperSigningCredential();   // production'da gerçek sertifika!

app.UseIdentityServer();

// Config.cs:
public static class Config
{
    public static IEnumerable<Client> Clients => new[]
    {
        new Client
        {
            ClientId = "order-service",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "catalog.read" }
        },
        new Client
        {
            ClientId = "spa-frontend",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,         // SPA — secret saklayamaz
            RedirectUris = { "https://app.com/callback" },
            AllowedScopes = { "openid", "profile", "catalog.read", "orders.write" }
        }
    };
}
```

### IProfileService — Kullanıcı Bilgisini Token'a Ekleme

Token'a kullanıcı bilgisi nasıl giriyor? IdentityServer'ın `IProfileService` arayüzü ile:

```csharp
public class CustomProfileService : IProfileService
{
    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var userId = context.Subject.GetSubjectId();
        var user = await _userManager.FindByIdAsync(userId);

        context.IssuedClaims.AddRange(new[]
        {
            new Claim("email", user.Email),
            new Claim("tenantId", user.TenantId),
            new Claim("role", user.Role)
        });
        // ne yapar → token'a custom claim'ler ekler
        // bu claim'ler kullanıcının token'ında görünür
        // microservice'ler token'ı doğrularken bu bilgilere erişir
    }
}
```

### Ne Zaman IdentityServer?

- .NET ekosisteminde kalıyorsan
- Tam programatik kontrol istiyorsan
- ASP.NET Core Identity ile entegrasyon önemliyse
- Lisans maliyetini karşılayabiliyorsan

---

## Keycloak — Open-Source Enterprise IdP

**Keycloak** Red Hat'in açık kaynak IdP'si. Java tabanlı ama dil-agnostik (her dilden client çalışıyor).

### Özellikler

- Tamamen ücretsiz, açık kaynak
- Web tabanlı admin UI (kullanıcı/client yönetimi)
- Hazır kullanıma — Docker ile dakikalar içinde ayakta
- Çok dil/protokol desteği — OIDC, SAML, LDAP
- Social login dahili
- Enterprise özellikler: realm, role mapping, identity brokering

### Keycloak Kavramları

**Realm:** Mantıksal izolasyon birimi. "Şirket A" realm'i, "Şirket B" realm'i ayrı kullanıcı havuzları. Multi-tenant yapı için ideal.

**Client:** Keycloak'a bağlanan uygulama (SPA, mobil app, microservice). Her client kendi yapılandırması (allowed grant types, redirect URLs, scopes).

**Role:** Kullanıcı yetkisi. Realm-level (tüm uygulamalarda geçerli) veya client-level (sadece o uygulamada geçerli).

**User:** Kullanıcı kaydı. Realm içinde benzersiz.

### Docker'da Hızlı Kurulum

```bash
docker run -p 8080:8080 \
  -e KEYCLOAK_ADMIN=admin \
  -e KEYCLOAK_ADMIN_PASSWORD=admin \
  quay.io/keycloak/keycloak:latest start-dev
```

`http://localhost:8080` → admin panel açılır.

**Adımlar:**
1. Realm oluştur: "kitap-app"
2. Client oluştur: "spa-frontend" (Public, Authorization Code + PKCE)
3. Kullanıcı oluştur: "berkan", şifre belirle
4. Role oluştur: "admin", "user"
5. Kullanıcıya role ata

Bu kadar. Şimdi uygulaman bu Keycloak'a bağlanıp giriş yapabilir.

### Ne Zaman Keycloak?

- Multi-language ekibin var (Java, .NET, Python karışık)
- Ücretsiz çözüm istiyorsan
- Enterprise özellikler (SAML, LDAP) gerekiyorsa
- Hazır UI ile kullanıcı yönetimi istiyorsan
- DevOps ekibinin Docker/Kubernetes bilgisi var

---

## Microservice'te JWT Validation

IdP token üretti. Şimdi microservice'in (mesela CatalogService) bu token'ı doğrulaması lazım. ASP.NET Core'da bu iş `AddJwtBearer` ile:

```csharp
// CatalogService Program.cs:
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://keycloak.app.com/realms/kitap-app";
        // ne yapar → IdP adresi
        // ASP.NET Core bu adresten otomatik public key çekiyor (/.well-known/openid-configuration)

        options.Audience = "catalog-api";
        // ne yapar → token bu API için mi üretilmiş kontrol eder
        // bunu kontrol etmezsen → başka API için üretilen token bu API'da da geçerli olur

        options.RequireHttpsMetadata = true;   // production'da true zorunlu
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1)
            // ne yapar → server saatleri arası 1 dk fark tolere edilir
            // varsayılan 5 dk — sıkı güvenlik için 0 yapılır
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CatalogRead", policy =>
        policy.RequireClaim("scope", "catalog.read"));
});

app.UseAuthentication();
app.UseAuthorization();
```

Endpoint'te:
```csharp
[Authorize(Policy = "CatalogRead")]
[HttpGet("urunler")]
public async Task<IActionResult> Get() { ... }
```

### Bu Validation Nasıl Çalışıyor?

1. Client istek atıyor: `Authorization: Bearer eyJhbGc...`
2. ASP.NET Core JWT'yi alıyor
3. Header'daki algoritmayı okuyor
4. IdP'den public key'i alıp (önbelleğe alır, sürekli istemez) imzayı doğruluyor
5. Issuer, audience, expire kontrol ediyor
6. Hepsi OK → kullanıcının claim'leri `HttpContext.User`'a yerleştirilir
7. Authorization policy kontrol ediliyor

Her şey **lokal** oluyor — JWT'nin güzelliği bu. IdP'ye her istekte ulaşmaya gerek yok, sadece public key bir kez alınıyor.

---

## Token Introspection vs Local Validation

İki yaklaşım var, biri yukarıdaki, diğeri farklı:

### Local JWT Validation (Yukarıda Gördük)

- API JWT'yi kendisi doğruluyor (imza, expire, claim'ler)
- IdP'ye sorgu yok
- Hızlı (0 network call)
- Stateless

**Dezavantaj:** Token iptal edilince anlaşılmaz. IdP'de "bu kullanıcıyı çıkar" deseler bile, token expire olana kadar geçerli kalır.

### Token Introspection

- API token'ı IdP'ye yolluyor: "Bu token geçerli mi?"
- IdP DB'sinden kontrol ediyor (active mi, revoke edilmiş mi)
- Cevap geliyor: geçerli/geçersiz

**Avantaj:** Anında iptal edilebilir token desteği.
**Dezavantaj:** Her istekte IdP'ye network call → 1-2ms ekleniyor + IdP yükü artıyor.

### Hangisi Ne Zaman?

- **Local validation:** Çoğu microservice. Performans öncelikli.
- **Introspection:** Çok hassas işlemler (ödeme, admin işlemleri). Token iptal hemen yansımalı.

**Karma yaklaşım:** Local validation yap + revoke list'i Redis'te tut + her isteğe Redis'ten "bu token revoke edilmiş mi?" sor. Network maliyeti düşük (Redis 0.5ms), tam IdP introspection gerekmiyor.

---

## Scope ve Audience Detayı

JWT içinde iki claim var ki authorization için kritik.

### Audience (aud)

"Bu token hangi API için üretildi?"

```json
{
  "aud": "catalog-api",
  "sub": "berkan",
  "scope": "catalog.read"
}
```

Her API kendi adına token bekler. CatalogService `aud=catalog-api` bekliyor. Kullanıcı bu CatalogService'in token'ını alıp PaymentService'e gönderse — PaymentService `aud=payment-api` bekliyor — token reddedilir.

**Neden önemli?** Catalog token'ı çalansa bile ödeme yapamaz. Servisler arası izolasyon.

### Scope

"Bu token'ın yetkileri neler?"

```json
{
  "scope": "catalog.read orders.write"
}
```

Aynı API içinde farklı yetkiler. Bir kullanıcı `catalog.read` ile sadece okuyabilir. Admin `catalog.read catalog.write` ile yazabilir de.

API endpoint'i policy ile scope kontrolü yapar:

```csharp
[Authorize(Policy = "CatalogWrite")]   // scope: catalog.write isteyen policy
[HttpPost("urunler")]
public IActionResult Ekle(UrunDto dto) { ... }
```

Token'da scope yoksa → 403 Forbidden.

---

## Servisler Arası Auth (M2M) — Client Credentials

Kullanıcı yok, OrderService CatalogService'i çağırıyor. Bu durumda OAuth Client Credentials akışı kullanılır (Gün 106).

### Akış

```csharp
// OrderService — bir HttpClient yapılandırması:
builder.Services.AddHttpClient("catalog", client =>
{
    client.BaseAddress = new Uri("https://catalog.api/");
})
.AddClientCredentialsTokenHandler("order-to-catalog");
// ne yapar → her istekten önce otomatik token al ve Bearer header'a ekler
// token önbelleğe alınır, expire olunca yenisi alınır
```

`order-to-catalog` token client tanımı:

```csharp
builder.Services.AddClientCredentialsTokenManagement()
    .AddClient("order-to-catalog", client =>
    {
        client.TokenEndpoint = "https://idp/connect/token";
        client.ClientId = "order-service";
        client.ClientSecret = "secret";
        client.Scope = "catalog.read";
    });
```

OrderService kullanım:
```csharp
var http = _httpFactory.CreateClient("catalog");
var response = await http.GetAsync("urunler/42");
// arka planda otomatik:
// 1. IdP'den token al (cache'lidir, expire olmadıkça yeniden almaz)
// 2. Authorization: Bearer ... header'ı ekle
// 3. CatalogService'e istek at
```

CatalogService gelen token'ı normal JWT validation ile doğruluyor. Token `client_credentials` flow ile alındığı için `sub` claim'i yok (kullanıcı yok) — yerine `client_id` var.

---

## API Gateway'de Auth — Merkezi mi Dağıtık mı?

Microservice'lere ulaşmadan önce bir **API Gateway** (Ocelot, YARP, Kong) var. Token validation'ı nerede yaparsın?

### Yaklaşım 1: Gateway'de Validate

- Gateway token'ı doğrular
- Geçersizse → istek microservice'e hiç gitmez
- Geçerliyse → token'ı microservice'e iletir veya açıp claim'leri header'a koyar

**Avantaj:**
- Microservice'ler auth logic'i yazmak zorunda değil
- Tek nokta — değişiklik kolay

**Dezavantaj:**
- Microservice gateway'siz erişilirse korumasız (internal network'e güveniyorsun)
- "Defense in depth" prensibine aykırı

### Yaklaşım 2: Her Microservice'te Validate

- Gateway sadece routing yapıyor
- Her microservice kendi JWT validation'ını yapıyor

**Avantaj:**
- Defense in depth — gateway atlansa bile microservice korur
- Microservice'ler bağımsız test edilebilir

**Dezavantaj:**
- Auth logic'i her serviste tekrar (kütüphane ile çözülür)
- Biraz daha fazla yük

### Modern Tavsiye: İkisi Birden

Gateway hızlı validation yapar (kötü istekleri eleyici filtre). Microservice de kendi validation'ını yapar (gerçek güvenlik katmanı). Defense in depth.

---

## Refresh Token Rotation — Tekrar

Gün 106'da detayını gördük, IdP bağlamında tekrar hatırlatma:

Her refresh token kullanıldığında IdP:
1. Eski refresh token'ı iptal eder
2. Yeni access token + yeni refresh token verir

Bu davranış IdP'de aktif edilmesi gereken bir özellik. Keycloak'ta: Realm Settings → Tokens → "Revoke Refresh Token" enable. IdentityServer'da: client config'inde `RefreshTokenUsage = TokenUsage.OneTimeOnly`.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de ASP.NET Core Identity cookie-based auth. Tek uygulama için yeterli. 500 kullanıcıda mükemmel.

50K kullanıcı + microservice + mobil + 3. parti developer ortamında:
- Cookie auth yetmiyor (mobil için zor)
- Her serviste auth tekrarı kabul edilemez
- SSO ihtiyacı oluşuyor (kullanıcı bir kez giriş yapsın, tüm uygulamalar)
- Merkezi IdP zorunlu hale geliyor

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| ASP.NET Core Identity (built-in) | Tek monolith için yeterli | Tek başına yetmez |
| OpenID Connect / IdP | Gereksiz (tek app) | Microservice varsa zorunlu |
| IdentityServer / Keycloak | Overkill | Standart yaklaşım |
| JWT Bearer in microservice | Az anlamlı | Her servis için zorunlu |
| Client Credentials (M2M) | İhtiyaç yok | Servisler arası şart |
| Token introspection | Gereksiz | Hassas işlemler için değerli |
| API Gateway auth | Gateway yoksa N/A | Defense in depth |

---

## Kontrol Soruları

1. OAuth 2.0 ile OpenID Connect arasındaki temel fark nedir? id_token vs access_token rolleri ne?
2. Identity Provider (IdP) kullanmanın avantajları neler? Her serviste ayrı auth yerine niye?
3. IdentityServer ile Keycloak arasındaki temel farklar neler? Hangisini ne zaman tercih edersin?
4. JWT'nin local validation'ı introspection'dan neden hızlı? Hangisi hangi senaryoda?
5. JWT'deki `aud` (audience) claim'i neden önemli? Doğrulanmasa ne risk var?
6. Scope ile role arasındaki fark nedir? OAuth/OIDC bağlamında ne anlama gelir?
7. Client Credentials flow ne zaman kullanılır? Authorization Code'dan farkı?
8. API Gateway'de mi yoksa her microservice'te mi token validation yapılmalı? Defense in depth açısından düşün.
