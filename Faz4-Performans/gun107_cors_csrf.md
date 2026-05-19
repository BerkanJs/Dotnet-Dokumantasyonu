# Gün 107 — API Security 2: CORS ve CSRF

---

## Bu Ders Neden Var?

İki güvenlik mekanizması anlatacağız: **CORS** ve **CSRF**. İsimleri benzediği için sürekli karıştırılıyor ama birbirinden çok farklı problemleri çözüyorlar.

- **CORS** → "Bu farklı domain'in API'mı çağırmasına izin vereyim mi?" sorusunun cevabı
- **CSRF** → "Kullanıcının bilgisi olmadan başka bir site adına işlem yaptırılabilir mi?" saldırısının önlenmesi

İkisi de tarayıcı güvenliği ile ilgili — backend güvenliği değil. Backend her zaman doğrulama yapmalı, CORS ve CSRF tarayıcı tarafında ek koruma katmanı.

---

## Same-Origin Policy — Önce Bunu Anla

CORS'u anlamak için önce **Same-Origin Policy (SOP)**'yi anlaman lazım. Bu, tarayıcının temel güvenlik kuralı.

### Tanım

Bir web sayfası **yalnızca kendi origin'inden** veri okuyabilir. Farklı origin'lerdeki kaynaklara erişimi sınırlıdır.

**Origin nedir?** Protocol + domain + port üçlüsü:
- `https://app.com` ve `https://app.com/page` → aynı origin
- `https://app.com` ve `http://app.com` → farklı origin (protocol farklı)
- `https://app.com` ve `https://api.app.com` → farklı origin (subdomain farklı)
- `https://app.com:443` ve `https://app.com:8080` → farklı origin (port farklı)

### Bu Kural Neden Var?

Şu kötü senaryoyu düşün — SOP olmasaydı:

1. Kullanıcı `banka.com`'da giriş yaptı, oturumu açık
2. Yan sekmede `kotu-site.com`'u açtı
3. `kotu-site.com`'un JavaScript'i `banka.com/api/bakiye`'ye istek atıyor
4. Tarayıcı otomatik olarak banka cookie'lerini gönderiyor (aynı domain'e gönderdiği gibi)
5. `kotu-site.com` senin bakiye bilgine erişebiliyor → felaket

SOP bu felaketi önler. `kotu-site.com` JavaScript'i `banka.com`'a istek atabilir ama **yanıtı okuyamaz**. Tarayıcı yanıtı saklar, JS'ye vermez.

**Önemli detay:** SOP isteği engellemiyor. İstek gidiyor, yanıt geliyor — ama JavaScript yanıtı göremiyor. (Bu detay CSRF için kritik olacak.)

---

## CORS — Same-Origin Policy'yi Bilerek Gevşetmek

SOP çok katı. Ama bazen meşru olarak farklı origin'e istek atmak istiyorsun:

- `app.com` frontend'i `api.app.com` backend'ini çağırıyor → farklı origin
- `frontend.com` SPA'sı `api.partner.com`'a entegre oluyor → farklı origin

Bu meşru senaryolar için **CORS (Cross-Origin Resource Sharing)** var. CORS demek: "Ben farklı origin'e izin veriyorum, JavaScript yanıtı okuyabilir."

### CORS Nasıl Çalışır?

Server, yanıtına özel header'lar ekleyerek "bu kaynağı şu origin'lerden çağırabilirler" der.

```
Tarayıcı isteği gönderir:
  Origin: https://frontend.com

Server yanıtı döner:
  Access-Control-Allow-Origin: https://frontend.com
  Access-Control-Allow-Methods: GET, POST
  Access-Control-Allow-Headers: Content-Type, Authorization
```

Tarayıcı header'ları görür: "Server bu origin'e izin veriyor" → JavaScript'in yanıtı görmesine izin verir.

İzin yoksa: yanıt geldi, ama tarayıcı "Cross-Origin Request Blocked" hatası verir, JS yanıtı göremez.

### Simple Request vs Preflight Request

Tüm cross-origin istekler aynı değil. İki kategori var:

**Simple request:** Basit istekler. Tarayıcı direkt gönderir.
- GET veya POST
- Standart header'lar
- Content-Type: `text/plain`, `application/x-www-form-urlencoded`, veya `multipart/form-data`

**Preflight (önyoklama) request:** Karmaşık istekler. Tarayıcı önce "izin var mı?" diye soruyor.
- PUT, DELETE, PATCH gibi metodlar
- Özel header'lar (Authorization, X-Custom-...)
- Content-Type: `application/json` (modern API'ların çoğu)

**Preflight nasıl çalışır?**

```
1. Tarayıcı OPTIONS isteği gönderir:
   OPTIONS /api/kitaplar
   Origin: https://frontend.com
   Access-Control-Request-Method: DELETE
   Access-Control-Request-Headers: Authorization

2. Server yanıt verir:
   Access-Control-Allow-Origin: https://frontend.com
   Access-Control-Allow-Methods: GET, POST, DELETE
   Access-Control-Allow-Headers: Authorization
   Access-Control-Max-Age: 3600   ← bu yanıtı 1 saat cache'le

3. Tarayıcı: "OK, izin var" → asıl DELETE isteğini gönderir
```

**Performans etkisi:** Her cross-origin istek için 2 round-trip (önce OPTIONS, sonra asıl istek). `Max-Age` ile preflight cache'lenir — bir saat boyunca tekrar sormaz.

### ASP.NET Core'da CORS Kurulumu

```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins("https://app.com", "https://staging.app.com")
              // ne yapar → sadece bu origin'lere izin ver
              // bunu yazmasaydık → hiçbir cross-origin istek geçemez

              .WithMethods("GET", "POST", "PUT", "DELETE")
              // ne yapar → izin verilen HTTP metodları
              // belirsiz bırakırsan → varsayılan sadece simple methods

              .WithHeaders("Content-Type", "Authorization")
              // ne yapar → client'ın gönderebileceği header'lar
              // Authorization buradaysa JWT gönderebilir

              .AllowCredentials();
              // ne yapar → cookie ve Authorization header'ı gönderilebilir
              // bunu yazmasaydık → cookie'siz, kimliksiz istekler
    });
});

app.UseCors("FrontendPolicy");
// ne yapar → policy'yi uygula
// UseRouting'den sonra, UseAuthentication'dan önce gelmeli
```

### CORS'ta Yapılan Yaygın Hatalar

**Hata 1: AllowAnyOrigin + AllowCredentials birlikte**
```csharp
policy.AllowAnyOrigin().AllowCredentials();   // ❌ Tarayıcı reddeder
```
Cookie ile herkesi kabul etmek = tüm internete oturum bilgisi sızıntısı. Tarayıcılar bunu engelliyor.

**Hata 2: Production'da AllowAnyOrigin**
```csharp
policy.AllowAnyOrigin();   // ❌ Production'da yapma
```
"Çalıştırmak için" diye herkese açtın — herkes API'ndan veri okuyabilir. Sadece development'ta yapılabilir, o da geçici.

**Hata 3: CORS'u backend güvenliği zannetmek**
CORS bir tarayıcı koruması. Sunucudan sunucuya yapılan istekleri etkilemez. CORS'u atlamak için tarayıcı dışından (curl, Postman, başka backend) istek atmak yeterli — CORS hiçbir şey yapmaz.

**Bu yüzden:** CORS, authentication değildir. CORS yetmez, backend yine authorization yapmalı.

---

## CSRF — Cross-Site Request Forgery

### Saldırı Nasıl Çalışır?

CSRF, kullanıcının bilgisi olmadan başka bir site adına işlem yaptırma saldırısı.

**Klasik senaryo:**

1. Kullanıcı `banka.com`'da giriş yaptı, oturum cookie'si var
2. Yan sekmede `kotu-site.com`'u açtı
3. `kotu-site.com`'da gizli bir form var:
   ```html
   <form action="https://banka.com/para-gonder" method="POST">
       <input name="hesap" value="saldirgan-hesabi" />
       <input name="tutar" value="10000" />
   </form>
   <script>document.forms[0].submit();</script>
   ```
4. Form otomatik gönderiliyor → tarayıcı `banka.com`'a istek atıyor
5. **Tarayıcı banka cookie'lerini otomatik ekliyor** (her isteğe ekler)
6. `banka.com` cookie'yi görüyor: "Bu Berkan, geçerli oturum" → işlemi yapıyor
7. Para gitti. Berkan farkında değil.

**Önemli:** Saldırgan cookie'yi göremiyor, çalmıyor. Sadece kullanıcının tarayıcısını kullanarak istek tetikliyor. Çünkü tarayıcı cookie'leri otomatik ekliyor.

### SOP Bunu Neden Engellemiyor?

Yukarıda dedik ki "SOP isteği engellemiyor, JS yanıtı okuyamıyor sadece." CSRF tam olarak bu boşluğu kullanıyor:

- Saldırganın yanıtı okumasına gerek yok
- Sadece isteği göndermesi yeterli (mesela "para gönder" işlemi)
- Yan etki yapıldığı anda saldırı tamamlandı

Bu yüzden SOP CSRF'ye karşı koruma değil. Ek mekanizma lazım.

### CSRF Koruması: Anti-Forgery Token

Çözüm: server her form/işlem için **rastgele bir token** üretir. İşlem isteği geldiğinde bu token'ı bekler.

**Akış:**

1. Kullanıcı `banka.com/transfer` formunu açar → server form ile birlikte gizli `csrf_token=abc123` üretir
2. Kullanıcı formu doldurur, gönderir → token form ile birlikte gönderilir
3. Server token'ı kontrol eder: "Ben bu kullanıcıya bu token'ı verdim mi?" → evet → işlemi yap

**Saldırgan açısından:**
- `kotu-site.com` kullanıcının `banka.com`'daki token'ını bilmiyor
- Bilemez de — SOP nedeniyle `banka.com`'dan token'ı okuyamıyor (yanıtı göremiyor)
- Token olmadan istek gönderince → server reddediyor

Token bilinmesi gereken bir "secret". Cookie otomatik gönderilir ama token JavaScript ile elle eklenir.

### ASP.NET Core'da Anti-Forgery

ASP.NET Core MVC otomatik destekliyor:

```csharp
// Form'da:
<form method="post">
    @Html.AntiForgeryToken()   // gizli input olarak token ekler
    <input name="tutar" />
</form>

// Controller'da:
[HttpPost]
[ValidateAntiForgeryToken]   // token'ı doğrula
public IActionResult Transfer(decimal tutar) { ... }
```

API için:
```csharp
builder.Services.AddAntiforgery(opt =>
{
    opt.HeaderName = "X-CSRF-TOKEN";
    // ne yapar → token'ı header'da bekler (SPA'lar için)
});

// SPA token'ı header'da gönderir:
// X-CSRF-TOKEN: abc123...
```

---

## SPA'da CSRF Neden Daha Az Sorun?

Modern SPA'larda CSRF riski **daha düşük** ama sıfır değil. Neden?

### SPA'larda Token'ın Yeri

SPA'lar genellikle JWT kullanıyor. JWT'yi nerede saklıyorsun?

**Senaryo A: JWT localStorage / memory'de**
- Token'ı her istekte JavaScript ekliyor (Authorization header)
- Tarayıcı otomatik eklemiyor
- `kotu-site.com` JS'i token'a erişemez (SOP)
- **CSRF imkansız** — saldırgan token'ı bilmiyor, gönderemiyor
- Ama XSS açığı varsa token çalınabilir (Gün 108)

**Senaryo B: JWT veya session cookie'de**
- Tarayıcı her istekte otomatik ekliyor
- **CSRF riski var** — eski sorun aynen geçerli
- Anti-forgery token gerekli

**Modern best practice:**
- Access token → memory'de (JS değişkeni)
- Refresh token → HttpOnly + Secure + SameSite=Strict cookie

`SameSite=Strict` cookie attribute'u CSRF'ye karşı modern koruma — cookie sadece aynı site'den gelen isteklerde gönderilir.

### SameSite Cookie — Modern CSRF Koruması

Cookie tanımlanırken `SameSite` özelliği üç değer alabilir:

**Strict:** Cookie SADECE aynı site'den gelen isteklerde gönderilir.
- Saldırgan sitesinden istek gönderse → cookie yok → CSRF imkansız
- Ama UX zor: başka siteden link tıklayıp giriş bile yapamazsın (cookie yok)

**Lax (modern varsayılan):** "Güvenli" cross-site isteklerde cookie gönderilir.
- GET istekleri (link tıklama) → cookie gönderilir ✓
- POST/PUT/DELETE → cookie gönderilmez (CSRF koruması)
- Çoğu senaryo için doğru denge

**None:** Tüm cross-site isteklerde cookie gönderilir.
- CSRF korumasız (eski davranış)
- Sadece `Secure` flag ile birlikte kullanılabilir (HTTPS şart)

```csharp
// ASP.NET Core'da:
builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.Cookie.SameSite = SameSiteMode.Lax;
    opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
```

Modern tarayıcılar varsayılan olarak `Lax` kullanıyor. Yine de explicit set etmen güvenli.

---

## CORS vs CSRF — Karıştırılan Konular

Net bir karşılaştırma:

| | CORS | CSRF |
|---|------|------|
| **Ne çözer** | Cross-origin erişim izni | Yetkisiz işlem tetikleme |
| **Yön** | Server'ın "izin veriyorum" demesi | Server'ın "bu istek meşru mu?" sorması |
| **Tarayıcı yapıyor** | Yanıtı bloklar | Tarayıcı bir şey yapmaz, server token kontrol eder |
| **Korunan** | Veri okuma (response) | Yan etkili işlemler (POST/PUT/DELETE) |
| **Kim için** | Frontend developer | Backend developer |
| **Atlanır mı** | Tarayıcı dışından (curl, Postman) atlanır | SameSite Strict + token ile zorlaşır |

**Ortak nokta:** İkisi de tarayıcı temelli. Backend her zaman bağımsız doğrulama yapmalı.

---

## Doğru Güvenlik Mimarisi

Tek başına ne CORS ne de CSRF yeter. Katmanlı güvenlik gerekli:

**Backend her zaman:**
1. Authentication zorunlu (JWT veya cookie kontrolü)
2. Authorization zorunlu (kullanıcı bu işlemi yapmaya yetkili mi?)
3. Input validation (gelen veri geçerli mi?)

**Buna ek tarayıcı koruması:**
4. CORS — sadece izin verilen origin'lerden istek kabul et
5. CSRF token veya SameSite cookie — yetkisiz işlem tetikleme önle
6. CSP (Content Security Policy) — XSS koruması (Gün 108)
7. HTTPS zorunluluğu — middle-man saldırılarına karşı

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de cookie auth + ASP.NET Core MVC anti-forgery otomatik çalışıyor. Form'larda `[ValidateAntiForgeryToken]` ile koruma var. 500 kullanıcıda yeterli.

50K kullanıcıda + SPA + mobil app + 3. parti entegrasyonlar geldiğinde:
- API'ya frontend.com'dan erişim → CORS gerekli
- JWT memory'de tutuluyor → CSRF doğal olarak azalıyor
- SameSite cookie ile ek koruma
- Refresh token rotation (Gün 106)

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| CORS yapılandırma | Sadece web app — gereksiz | Frontend ayrı domain'de — zorunlu |
| Anti-forgery (MVC) | [ValidateAntiForgeryToken] yeterli | Cookie auth varsa zorunlu |
| SameSite=Lax cookie | Modern varsayılan | Standart |
| JWT memory'de + Refresh cookie | Karmaşık | Modern SPA için doğru yaklaşım |
| CORS'u backend güvenliği saymak | Tehlikeli yanılgı | Tehlikeli yanılgı |

---

## Kontrol Soruları

1. Same-Origin Policy nedir? İsteği mi engelliyor, yanıtı mı? Bu detay CSRF'de neden önemli?
2. CORS preflight (OPTIONS) ne zaman tetiklenir? Hangi istekler simple sayılır?
3. `AllowAnyOrigin()` ile `AllowCredentials()` neden birlikte kullanılamaz?
4. CSRF saldırısı neden çalışıyor? Tarayıcı cookie'leri otomatik göndermesinin rolü ne?
5. SPA'da JWT memory'de tutulursa CSRF neden imkansız hale gelir?
6. SameSite cookie'nin Strict, Lax, None değerleri arasındaki fark nedir?
7. CORS neden backend güvenliği değildir? Hangi durumlarda atlanabilir?
8. CORS ile CSRF arasındaki temel fark nedir? Hangi probleme çare oluyorlar?
