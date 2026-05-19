# Gün 106 — API Security 1: OAuth 2.0, JWT ve API Key Yönetimi

---

## Bu Hafta Ne Öğreneceğiz?

Müfredatta "Gün 106-110 — API Security Derinlemesine" tek başlık altında 5 günlük içerik var. Beş gün boyunca:

- **Gün 106 (bugün):** OAuth 2.0, JWT refresh token, API key yönetimi
- **Gün 107:** CORS ve CSRF
- **Gün 108:** SQL Injection, XSS, güvenlik header'ları
- **Gün 109:** OWASP API Security Top 10
- **Gün 110:** HybridCache (.NET 9)

Bugün kimlik doğrulama (authentication) ve token yönetimini ele alacağız.

---

## Authentication vs Authorization — Karıştırılan İki Kavram

Bu ikisini her gün karıştıran geliştiriciler görürsün. Net ayıralım.

**Authentication (Kimlik Doğrulama):** "Sen kimsin?" sorusunun cevabı. Kullanıcının iddia ettiği kişi olduğunu kanıtlama.
- Şifre gir, doğru mu? → authentication
- Token geçerli mi? → authentication
- Parmak izini tara → authentication

**Authorization (Yetkilendirme):** "Ne yapmaya iznin var?" sorusunun cevabı. Kimliği bilinen kullanıcının ne yapabileceğine karar verme.
- Admin mi, kullanıcı mı? → authorization
- Bu kaynağa erişebilir mi? → authorization
- Bu endpoint için yetkisi var mı? → authorization

**Sıralama önemli:** Önce authentication, sonra authorization. Kim olduğunu bilmeden ne yapabileceğine karar veremezsin.

**Analoji:** Apartman girişi. Kapıdaki güvenlik kimliğini ister (authentication — "Berkan Özçelik mi?"). Sonra "kaçıncı dairede oturuyorsun?" diye sorar (authorization — "5. kata çıkabilirsin ama 3. kata çıkamazsın").

---

## OAuth 2.0 Nedir?

OAuth 2.0 bir **authorization framework**'üdür. Adı yanıltıcı — "auth" var ama esasen yetkilendirme protokolü. Authentication'ı OAuth'un üstüne kurulu OpenID Connect (OIDC) yapıyor (Gün 111'de göreceğiz).

### OAuth Çözmek İçin Var Olduğu Problem

Senaryo: Bir fitness app yazdın. Kullanıcılar Google Drive'larındaki dosyaları yedeklesin istiyorsun. Ne yapacaksın?

**Yanlış yaklaşım:** Kullanıcıdan Google şifresini iste, sen kendin Google'a giriş yap.
- Senin uygulaman kullanıcının Google şifresini görür → güvenlik felaketi
- Kullanıcı uygulamana güvenmek zorunda
- Sadece dosya yedekleme istemen yetmiyor, mailler, kalender, her şeye erişimin var

**OAuth çözümü:**
1. Kullanıcı senin uygulamandan Google'a yönlendirilir
2. Google ekranında: "Fitness App'in Drive dosyalarına erişmesine izin veriyor musun?"
3. Kullanıcı "evet" der → Google sana bir **token** verir
4. Sen bu token ile sadece Drive'a (ve sadece dosyalara) erişebilirsin
5. Şifreyi hiç görmedin. Sadece sınırlı yetkin var. Kullanıcı istediğinde token'ı iptal edebilir.

OAuth bunu standartlaştıran protokol. Şirketler arası birbirine güvenmeden yetki devri.

---

## OAuth Grant Types — Hangi Senaryo İçin Hangisi?

OAuth'un farklı "akış"ları var. Hepsi token almak için ama farklı durumlara uygun.

### 1. Authorization Code (Web Uygulamaları İçin)

En yaygın akış. Server tarafı olan web uygulamaları kullanır.

**Akış:**
1. Kullanıcı senin uygulamanda "Google ile giriş yap" tıklar
2. Google'a yönlendirilir → kullanıcı izin verir
3. Google senin uygulamana bir **authorization code** gönderir (kısa ömürlü, tek kullanımlık kod)
4. Senin sunucun bu kodu Google'a tekrar gönderir → karşılığında **access token** alır
5. Access token ile API çağrıları yaparsın

**Neden kod aracılığı?** Token'ı doğrudan tarayıcıya göndermek tehlikeli (URL'de, history'de görünür). Kod gönderilir, sunucu güvenli ortamda kodu token'a çevirir.

**Ne zaman:** Sunucu tarafı olan web uygulamaları (ASP.NET Core, Django, Rails).

### 2. Authorization Code + PKCE (SPA ve Mobil İçin)

Single Page Application (React, Vue) veya mobil app için authorization code yetersiz. Çünkü:
- Backend yok (veya hassas işlem yapamaz)
- Client secret saklayamazsın (kod kullanıcının tarayıcısında, kim isterse görür)

**PKCE (Proof Key for Code Exchange — "piksi" okunur)** bunu çözer. Akış:

1. Client rastgele bir **code verifier** (gizli string) üretir
2. Bunu hash'leyip **code challenge** olarak Google'a gönderir
3. Google'a yetki isteği gönderir (challenge ile birlikte)
4. Authorization code alır
5. Token isterken **orijinal verifier'ı** gönderir
6. Google: "Hash'i hesapladım, challenge ile uyuşuyor — sensin" der → token verir

**Neden bu işe yarar?** Saldırgan authorization code'u çalsa bile verifier'ı bilmiyor → token alamıyor.

**Ne zaman:** SPA, mobil app, masaüstü uygulamalar. Modern öneri: tüm public client'lar PKCE kullanmalı.

### 3. Client Credentials (Sunucudan Sunucuya)

Kullanıcı yok, iki sistem birbiriyle konuşuyor. Mesela senin servisin başka bir API'ye çağrı yapıyor.

**Akış:** Direkt client_id + client_secret ile token isteniyor. Kullanıcı etkileşimi yok.

**Ne zaman:** Backend → backend iletişimi. Cron job'lar, microservice'lerin birbirini çağırması, scheduled task'lar.

### 4. Diğerleri (Eski/Önerilmeyen)

- **Implicit Flow:** Eski SPA çözümü, PKCE varken artık önerilmiyor. Güvenlik açığı vardı.
- **Resource Owner Password Credentials:** Kullanıcı şifresini doğrudan client'a vermek. OAuth'un çözdüğü asıl problem bu — kullanma.
- **Device Code:** TV, IoT cihazlar için. Kullanıcı başka cihazda kod onaylıyor. Niş senaryo.

### Karar Tablosu

| Senaryo | Doğru akış |
|---------|-----------|
| Sunucu tarafı web (ASP.NET Core MVC) | Authorization Code |
| SPA (React, Vue, Angular) | Authorization Code + PKCE |
| Mobil app (iOS, Android, React Native) | Authorization Code + PKCE |
| Backend-to-backend, cron job | Client Credentials |
| Akıllı TV, IoT | Device Code |

---

## JWT — JSON Web Token

OAuth'tan aldığın token genellikle JWT formatında. JWT, kendi içinde verisini taşıyan, imzalı bir token.

### JWT'nin Yapısı — Üç Parça

JWT şu yapıda: `header.payload.signature` — üç base64 string nokta ile ayrılmış.

```
eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiI0MiIsIm5hbWUiOiJCZXJrYW4ifQ.SflKxwR...
└─── header ────┘ └────── payload ──────┘ └─── signature ───┘
```

**Header:** Token'ın metadata'sı. Hangi algoritma ile imzalandı?
```json
{ "alg": "HS256", "typ": "JWT" }
```

**Payload:** Token'ın asıl içeriği. Kullanıcı bilgisi, yetkiler, son kullanma tarihi.
```json
{
  "sub": "42",                       // subject — kullanıcı ID
  "name": "Berkan",
  "role": "admin",
  "iat": 1715000000,                  // issued at — ne zaman üretildi
  "exp": 1715003600                   // expires — ne zaman geçersiz olur
}
```

**Signature:** Header + payload'ın imzası. Token'ın içeriği değişmediğini garantiler.

### JWT Nasıl Doğrulanır?

Server token'ı aldığında:
1. Header ve payload'ı çözer (base64 decode — şifreli değil, sadece encoded)
2. Header + payload'ı kendi gizli anahtarıyla tekrar imzalar
3. Üretilen imza ile token'daki imza eşleşiyor mu? → eşleşiyorsa token geçerli

**Önemli:** JWT şifreli DEĞİL, imzalı. Payload'ı herkes okuyabilir (jwt.io'da decode et). İmza sadece "değiştirilmediğini" kanıtlıyor.

Bu yüzden JWT payload'ına asla hassas veri koyma (şifre, kredi kartı). Sadece public bilgiler (kullanıcı id, rol).

### Stateful vs Stateless Token

Geleneksel session: server her kullanıcı için bellekte oturum tutar. Token sadece bu oturumun anahtarı.
- Dezavantaj: server'da state var → ölçeklendirme zor.

JWT stateless: token'ın kendisi tüm bilgiyi taşıyor. Server bir şey saklamıyor.
- Avantaj: ölçeklendirme kolay, microservice'lerde her servis ayrı doğrulayabilir.
- Dezavantaj: token üretildikten sonra geri çağıramazsın — exp'e kadar geçerli kalır.

**Bu son madde kritik bir problem yaratır → refresh token'a geçelim.**

---

## Refresh Token — JWT'nin Geri Çağrılamaması Problemi

### Problem

Kullanıcıya bir saat geçerli JWT verdin. Kullanıcı 10 dakika sonra "şifremi değiştirdim, çıkış yap" dedi. Token hâlâ 50 dakika geçerli. Şifreyi değiştirdin ama token çalışmaya devam ediyor.

Veya: token çalındı. Saldırgan elinde token var, 50 dakika boyunca işlem yapabilir.

JWT'nin doğası gereği "iptal" yok — sadece süresinin dolmasını bekleyebilirsin.

### Çözüm: Kısa Ömürlü Token + Uzun Ömürlü Refresh Token

İki tür token kullanıyorsun:

**Access Token (JWT, kısa ömürlü):**
- 15 dakika - 1 saat geçerli
- API çağrılarında kullanılır
- Çalınırsa hasar sınırlı (kısa sürede expire)

**Refresh Token (uzun ömürlü):**
- 7-30 gün geçerli
- Yeni access token almak için kullanılır
- Sadece auth server'a gönderilir, normal API'lara değil
- DB'de tutulur — istediğin zaman iptal edebilirsin (stateful)

**Akış:**
1. Kullanıcı giriş yapar → access token (15 dk) + refresh token (7 gün) alır
2. 15 dakika boyunca API'ları access token ile kullanır
3. Access token expire olur → client refresh token ile yeni access token ister
4. Auth server refresh token'ı DB'den kontrol eder, geçerliyse yeni access token verir

**Felsefe:** Access token kullanışlı ama tehlikeli (geri alınamaz). Kısa tut. Refresh token uzun ömürlü ama nadiren kullanılıyor ve geri alınabiliyor — DB'de kontrol altında.

### Refresh Token Rotation — Güvenliği Artır

Refresh token kullanılınca yeni refresh token üret, eskisini iptal et:

```
İlk giriş: Access1 + Refresh1
Refresh1 ile yenileme: Access2 + Refresh2 (Refresh1 artık geçersiz)
Refresh2 ile yenileme: Access3 + Refresh3 (Refresh2 artık geçersiz)
```

**Neden değerli?** Saldırgan refresh token'ı çaldı. Kullandı, yeni access aldı. Sen de aynı refresh token'ı kullanmaya çalışıyorsun ama artık geçersiz. Auth server "bu refresh token zaten kullanılmış, hemen tüm session'ı iptal et" diyebilir.

**Token reuse detection:** Aynı refresh token iki kez kullanılırsa → birinin çaldığı belli. Kullanıcının tüm refresh token'larını iptal et, tekrar giriş yapsın.

---

## API Key Yönetimi

JWT kullanıcılar için. **API Key** genelde 3. parti geliştiriciler için — "developer.app.com" kayıt ol, API key al, API'mı kullan.

### JWT vs API Key — Fark Ne?

| | JWT | API Key |
|---|-----|---------|
| Kullanıcı | Son kullanıcı | Developer / 3. parti uygulama |
| Süresi | Kısa (15 dk - 1 saat) | Uzun (ay/yıl, manuel iptal) |
| İçerik | Kullanıcı bilgisi, yetki | Sadece bir random string |
| Doğrulama | İmza kontrolü (stateless) | DB lookup (stateful) |
| İptal | Refresh token rotation | DB'de "active=false" yap |

API key'ler aslında basit: rastgele, uzun bir string (örn: `sk_live_a3f4b9c2...`). Sunucu DB'de "bu key kime ait, hangi yetkilere sahip" tutar. Her istek geldiğinde DB'ye bakıyor.

### API Key En İyi Uygulamalar

**1. Hash'leyerek sakla:**
DB'de raw API key'i tutma — hash'le. Sızıntı olursa bile düz key görünmesin.
```
DB'de saklanan: sha256(api_key)
Karşılaştırma: sha256(gelen_key) == DB'deki hash mi?
```

**2. Prefix'le kategori ver:**
```
sk_live_xxx → production secret key
sk_test_xxx → test ortamı
pk_xxx → publishable key (frontend'de güvenli)
```
GitHub log'larına yanlışlıkla yapıştırıldığında pattern matching ile yakalanır (GitHub bunu yapıyor).

**3. Scope (yetki sınırı) tanımla:**
Her API key her şeyi yapamasın. "Bu key sadece read-only", "bu key sadece kitap endpoint'leri" gibi.

**4. Rate limit per key:**
Bir developer'ın key'i tüm sistemi yavaşlatmasın. Key başına ayrı limit.

**5. Rotation politikası:**
Key'ler bir yıl sonra otomatik expire olsun. Developer yeni key oluşturmak zorunda → eski key kullanılıyor mu monitoring yap.

**6. Audit log:**
Her API key kullanımı loglansın — kim ne zaman hangi endpoint'i çağırdı. Şüpheli aktivite tespiti.

---

## ASP.NET Core'da JWT Kurulumu — Pratik

Konsepti anlattık. Hızlıca implementation:

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://my-auth-server.com",
            // ne yapar → token'ı kim üretmiş kontrol eder
            // bunu kontrol etmezsen → başka bir auth server'ın token'ları da geçerli sayılır

            ValidateAudience = true,
            ValidAudience = "my-api",
            // ne yapar → token bu API için mi üretilmiş kontrol eder
            // bunu kontrol etmezsen → başka API için üretilen token senin API'na da geçer

            ValidateLifetime = true,
            // ne yapar → exp claim'ini kontrol eder, süresi geçmişse reddeder
            // bunu kontrol etmezsen → 1 yıl önceki token hâlâ geçerli sayılır (felaket)

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
            // ne yapar → imzayı kontrol eder, manipüle edilmediğini garantiler
        };
    });

builder.Services.AddAuthorization();

app.UseAuthentication();   // önce authentication
app.UseAuthorization();    // sonra authorization
```

**Sıra önemli:** Authentication önce çalışmalı (kim olduğunu öğren), sonra authorization (ne yapabileceğine karar ver).

---

## Token'ı Nereye Saklamalı? (Client Tarafı)

Frontend'de access token nereye konacak — bu güvenlik açısından kritik karar.

### Seçenekler

**1. LocalStorage:** Kolay ama tehlikeli. XSS saldırısı varsa JavaScript'le okunabilir, çalınabilir.

**2. SessionStorage:** Sekme kapanınca silinir. LocalStorage'dan biraz daha iyi ama XSS hâlâ riski.

**3. Memory (JS değişkeni):** Sayfa yenilenince kaybolur. XSS'e karşı en güvenli ama UX kötü.

**4. HttpOnly Cookie:** JavaScript erişemez (XSS koruması). Otomatik gönderilir. CSRF riski oluşur (Gün 107'de).

**Modern tavsiye:**
- Access token → memory'de (JS değişkeni)
- Refresh token → HttpOnly + Secure cookie
- Sayfa yenilenince → refresh token ile yeni access token al

Bu yaklaşım hem XSS'e hem CSRF'ye karşı dayanıklı.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de Identity ile cookie-based auth var. 500 kullanıcıda mükemmel çalışır. Ama:
- Mobil app yapmak istersen — cookie auth zor (CSRF, cross-origin sorunları)
- 3. parti developer'a API açmak istersen — cookie değil token gerekli
- Microservice'e geçersen — her servis cookie'ye bağlı kalamaz

50K kullanıcıda: web + mobil + 3. parti entegrasyonlar bir arada. JWT + refresh token + API key sistemi standart yaklaşım.

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| Cookie auth (Identity) | Sade web app için yeterli | Web için OK ama mobil/3. parti'ye yetmez |
| JWT | Mobil app varsa | Standart — stateless, ölçeklenebilir |
| Refresh token rotation | İyi alışkanlık | Zorunlu — security best practice |
| API key sistemi | Gereksiz (3. parti yok) | Developer ekosistemi varsa zorunlu |
| OAuth provider (Auth0, Keycloak) | Kendin yazabilirsin | Hazır çözüm tercih edilir (Gün 111) |

---

## Kontrol Soruları

1. Authentication ile authorization arasındaki fark nedir? Hangi sırada çalışırlar?
2. OAuth Authorization Code akışında neden token doğrudan değil "kod" aracılığı ile gönderilir?
3. PKCE neyi çözüyor? SPA'lar neden Authorization Code yerine Authorization Code + PKCE kullanmalı?
4. JWT'nin payload'ı şifreli midir? Hassas veri konabilir mi?
5. Refresh token rotation nedir? Token reuse detection neyi tespit eder?
6. JWT'nin "geri çağrılamama" problemi nedir? Refresh token bunu nasıl çözer?
7. API key'i DB'de neden hash'leyerek saklarsın? Düz saklamanın riski ne?
8. Access token'ı frontend'de localStorage'da saklamak neden tehlikelidir? Modern tavsiye nedir?
