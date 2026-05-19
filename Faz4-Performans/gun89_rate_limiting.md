# Gün 89 — Rate Limiting

---

## Rate Limiting Nedir, Neden Gerekli?

API'n internete açık. Biri script yazıp saniyede 10.000 istek atarsa — DB bağlantı havuzu tükenir, diğer kullanıcılar yanıt alamaz, sunucu çöker. Rate limiting bunu önler: belirli zaman diliminde izin verilen istek sayısını sınırlar. Aşıldığında `429 Too Many Requests` döner.

**Gerçek hayat analojisi:** 50 kişilik otobüs. 51. kişi binemez — "sonraki seferi bekleyin." Kapasiteyi aşmak herkesi etkiler.

**Neyi korur?**
- Sunucu kaynakları (CPU, RAM, DB bağlantıları)
- Diğer kullanıcıların deneyimi (bir kişi herkesi yavaşlatmasın)
- Brute-force saldırıları (login endpoint'ine dakikada 1000 deneme)
- Maliyet (cloud'da her istek para — abuser'lar fatura şişirir)

**Ne zaman kullanılır?**
- Public API — her zaman
- Login/register endpoint — kesinlikle (brute-force koruması)
- Ödeme/SMS gönderimi — kesinlikle (maliyet koruması)
- Internal microservice — genelde gereksiz (güvenilen ortam)

---

## ASP.NET Core 7+ Built-in Rate Limiting

.NET 7'den itibaren `Microsoft.AspNetCore.RateLimiting` middleware olarak gelir — üçüncü parti paket gerekmez.

```csharp
// Program.cs — temel kurulum
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // ne yapar → limit aşılınca 429 döner
    // bunu yazmasaydık → varsayılan 503 döner, client "sunucu çöktü" zanneder
    // 429 doğru semantik → "çok fazla istek, biraz bekle"

    opt.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        // ne yapar → client'a "60 saniye sonra tekrar dene" bilgisi verir
        // bunu yazmasaydık → client ne kadar bekleyeceğini bilmez, hemen retry yapar
        await context.HttpContext.Response.WriteAsync(
            "Rate limit aşıldı. 1 dakika sonra tekrar deneyin.", ct);
    };

    opt.AddFixedWindowLimiter("Genel", limiter =>
    {
        limiter.PermitLimit = 100;                // pencere başına max 100 istek
        limiter.Window = TimeSpan.FromMinutes(1); // 1 dakikalık pencere
        limiter.QueueLimit = 0;                   // sınırı aşan bekletilmez, direkt 429
        // QueueLimit = 5 yazsaydık → 5 istek kuyrukta bekler, slot açılınca işlenir
    });
});

app.UseRateLimiter();   // UseRouting'den sonra, endpoint mapping'den önce
// bunu UseRouting'den önce koyarsan → route bilgisi yok, policy eşleşemez
```

**Endpoint'e uygulama:**
```csharp
app.MapGet("/kitaplar", async (IKitapRepo repo) =>
{
    return await repo.ListAsync();
}).RequireRateLimiting("Genel");
// ne yapar → bu endpoint dakikada 100 istek kabul eder, 101. → 429
// bunu yazmasaydık → endpoint sınırsız istek alır

// Controller'da:
[EnableRateLimiting("Genel")]
[HttpGet("kitaplar")]
public async Task<IActionResult> GetKitaplar() { /* ... */ }

// Belirli endpoint'i muaf tutmak:
[DisableRateLimiting]
[HttpGet("health")]
public IActionResult Health() => Ok();
// ne yapar → health check endpoint'i rate limit'ten muaf
// neden → monitoring servisleri dakikada yüzlerce kez çağırır, engellenmemeli
```

---

## Dört Strateji — Hangisi Ne Zaman?

### 1. Fixed Window (Sabit Pencere)

Her dakika başında sayaç sıfırlanır. En basit strateji.

```
00:00 ─── 100 hak ─── 01:00 ─── yeni 100 hak ─── 02:00
```

**Problemi:** Kullanıcı 00:59'da 100, 01:01'de 100 istek atabilir → 2 saniyede 200 istek geçer (pencere sınırında burst).

```csharp
opt.AddFixedWindowLimiter("Sabit", l =>
{
    l.PermitLimit = 100;
    l.Window = TimeSpan.FromMinutes(1);
    // ne yapar → her dakika başında 100 hak verir
    // ne zaman kullan → basit API, burst önemsiz, başlangıç için yeterli
});
```

### 2. Sliding Window (Kayan Pencere)

Fixed window'un burst açığını kapatır. Pencere sabit noktada sıfırlanmaz — sürekli kayar.

```
Her an şu soruyu sorar: "Son 60 saniyede kaç istek geldi?"
→ Pencere sınırı yok, burst açığı kapandı
```

```csharp
opt.AddSlidingWindowLimiter("Kayan", l =>
{
    l.PermitLimit = 100;
    l.Window = TimeSpan.FromMinutes(1);
    l.SegmentsPerWindow = 6;
    // ne yapar → 1 dakikayı 6 parçaya böler (10'ar saniye segment)
    // daha fazla segment → daha hassas kontrol ama biraz daha fazla bellek
    // bunu 1 yazsaydık → fixed window'a dönüşür (segment = pencere)
    // ne zaman kullan → fixed window'un burst açığı kabul edilemezken
});
```

### 3. Token Bucket (Jeton Kovası)

Kısa süreli burst'e izin verir ama uzun vadede sabit hız uygular. Kovada jeton birikir — her istek bir jeton harcar. Kova boşalırsa → 429.

**Analoji:** Oyun salonunda saatte 10 jeton düşer, kova max 20 tutar. 15 jeton biriktirdiysen 15 oyun peş peşe oynayabilirsin. Ama saatte 10'dan fazla oynayamazsın (uzun vadede).

```
Kova: [████████████░░░░░░░░]  12/20 jeton
İstek geldi → jeton harcanır: [███████████░░░░░░░░░]  11/20
Her saniye 5 jeton eklenir (max 20'ye kadar)
```

```csharp
opt.AddTokenBucketLimiter("Jeton", l =>
{
    l.TokenLimit = 20;                                 // kova max kapasitesi
    l.ReplenishmentPeriod = TimeSpan.FromSeconds(1);   // her saniye
    l.TokensPerPeriod = 5;                             // 5 jeton eklenir
    l.QueueLimit = 0;
    // ne zaman kullan → burst toleransı istiyorsun ama uzun vadede sınır lazım
    // örnek: mobil app açılışta 5-6 paralel istek atar (burst), sonra yavaşlar
    // fixed window olsa açılış isteklerini engellerdi
});
```

### 4. Concurrency Limiter (Eşzamanlılık)

Zaman penceresi yok — aynı anda işlenen istek sayısını sınırlar. İstek bitince slot serbest kalır.

**Analoji:** 3 kasiyerli market. 3 kişi işlem görüyor, 4. kişi sırada bekler. Biri bitince sıradaki başlar.

```csharp
opt.AddConcurrencyLimiter("Esanli", l =>
{
    l.PermitLimit = 10;     // aynı anda max 10 istek işlenir
    l.QueueLimit = 5;       // 10 doluysa 5 kişi kuyrukta bekler
    // 16. istek → direkt 429
    // ne zaman kullan → ağır endpoint (rapor üretme, dosya işleme, dış API çağrısı)
    // neden → 100 paralel rapor isteği DB'yi çökertir, 10 ile sınırla
});
```

### Seçim Tablosu

| Strateji | En iyi senaryo | Burst davranışı |
|----------|---------------|-----------------|
| Fixed Window | Basit API, hızlı başlangıç | Pencere sınırında burst geçebilir |
| Sliding Window | Burst toleransı sıfır olmalı | Tamamen engeller |
| Token Bucket | Kısa burst OK, uzun vadede sınır | Biriken jeton kadar burst izni |
| Concurrency | Ağır endpoint, DB koruması | Eşzamanlılık sınırlar, hız değil |

---

## Kullanıcı Bazlı Rate Limiting (Partition)

Global limit koyarsan — 100 istek/dk — tüm kullanıcılar aynı havuzu paylaşır. Bir kişi 100'ünü harcarsa diğerleri 429 alır. Çözüm: her kullanıcıya kendi penceresi.

```csharp
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = 429;

    opt.AddPolicy("KullaniciBazli", httpContext =>
    {
        // Partition key belirleme — her key kendi sayacını tutar
        var userId = httpContext.User.Identity?.Name
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonim";
        // ne yapar → giriş yapmışsa kullanıcı adı, yapmamışsa IP ile gruplar
        // bunu yazmasaydık → herkes aynı partition, tek kişi herkesi engeller
        // "anonim" fallback → ne user ne IP alınamazsa (edge case)

        return RateLimitPartition.GetTokenBucketLimiter(userId, _ =>
            new TokenBucketRateLimiterOptions
            {
                TokenLimit = 30,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                TokensPerPeriod = 10,
                QueueLimit = 0
            });
    });

    // Farklı endpoint'lere farklı limit
    opt.AddPolicy("LoginSiniri", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(ip, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,                     // dakikada max 5 login denemesi
                Window = TimeSpan.FromMinutes(1)
            });
        // ne yapar → brute-force koruması, IP başına 5 deneme/dk
        // neden user bazlı değil → saldırgan henüz giriş yapmamış, IP tek bilgi
    });
});

// Kullanım:
app.MapPost("/login", ...).RequireRateLimiting("LoginSiniri");
app.MapGet("/kitaplar", ...).RequireRateLimiting("KullaniciBazli");
```

---

## Rate Limit Yanıtında Ne Dönmeli?

İyi bir 429 yanıtı client'a yardımcı olur:

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 30
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1690000000

{ "error": "Rate limit exceeded", "retryAfter": 30 }
```

```csharp
opt.OnRejected = async (context, ct) =>
{
    var retryAfter = "30";
    context.HttpContext.Response.Headers.RetryAfter = retryAfter;
    context.HttpContext.Response.Headers["X-RateLimit-Limit"] = "100";
    context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = "0";
    // ne yapar → client SDK'ları bu header'ları okuyup otomatik retry yapar
    // bunu yazmasaydık → client karanlıkta, sürekli retry yapıp ban yiyebilir

    await context.HttpContext.Response.WriteAsJsonAsync(new
    {
        error = "Çok fazla istek gönderdiniz.",
        retryAfterSeconds = int.Parse(retryAfter)
    }, ct);
};
```

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de rate limiting yok — herhangi biri sınırsız istek atabilir. 500 kullanıcıda sorun çıkmaz çünkü hepsi normal kullanıyor. 50K'da bir bot tüm API'yı çökertebilir, bir script-kiddie login endpoint'ini brute-force'layabilir.

```csharp
// Faz2'de — hiçbir koruma yok:
[HttpPost]
public async Task<IActionResult> Login(LoginDto dto)
{
    // dakikada 10.000 deneme yapılabilir — brute-force'a açık
    var user = await _userManager.FindByEmailAsync(dto.Email);
    // ...
}

// Faz4'te — IP bazlı sınır:
[EnableRateLimiting("LoginSiniri")]  // 5 deneme/dk/IP
[HttpPost]
public async Task<IActionResult> Login(LoginDto dto) { /* ... */ }
```

---

## 500 vs 50K Kullanıcı

| | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---|---|---|
| Rate limiting | İyi alışkanlık — basit fixed window yeterli | Zorunlu — sliding window veya token bucket |
| Kullanıcı bazlı partition | Opsiyonel | Şart — bir kullanıcı herkesi etkilemesin |
| Login sınırı | Temel güvenlik (10/dk) | Kesinlikle + IP bazlı + captcha |
| Concurrency limiter | Gereksiz | Ağır endpoint'lerde DB koruması |
| Overengineering sinyali | 500 kullanıcıya token bucket + Redis distributed limiter | — |

---

## Kontrol Soruları

1. Fixed window'un burst açığı nedir, sliding window bunu nasıl çözer?
2. Token bucket neden burst'e izin verir ama fixed window vermez?
3. Rate limit aşıldığında neden 503 değil 429 döndürülmelidir?
4. Kullanıcı bazlı partition olmadan ne sorun çıkar?
5. Login endpoint'ine neden kullanıcı bazlı değil IP bazlı limit koyarsın?
6. `Retry-After` header'ı ne işe yarar?
