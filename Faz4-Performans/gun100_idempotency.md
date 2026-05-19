# Gün 100 — Idempotency: API ve Message Consumer

---

## Idempotency Nedir?

Aynı işlemi 1 kez yapmak ile 10 kez yapmak arasında fark olmaması. Sonuç her seferinde aynı.

**Analoji:** Asansörde "3. kat" düğmesine 1 kez bassan da 5 kez bassan da sonuç aynı — 3. kata gidersin. Ama otomatta "çay" düğmesine 5 kez basarsan 5 çay alırsın. Asansör idempotent, otomat değil.

**Neden önemli?**
- Kullanıcı "Sipariş Ver" butonuna çift tıkladı → 2 sipariş mi oluşsun?
- Network timeout oldu, client retry attı → ödeme 2 kez mi çekilsin?
- Message queue mesajı tekrar teslim etti → stok 2 kez mi düşsün?

Idempotency olmadan: tekrarlanan istek = tekrarlanan yan etki = felaket (çift ödeme, çift sipariş, yanlış stok).

---

## HTTP Method Idempotency Tablosu

| Method | Idempotent mi? | Açıklama |
|--------|---------------|----------|
| **GET** | Evet (doğal) | Sadece oku, hiçbir şeyi değiştirme — 100 kez çağır, sonuç aynı |
| **PUT** | Evet (doğal) | "Kaynağı bu hale getir" — 5 kez aynı şeyi koysan da sonuç aynı durum |
| **DELETE** | Evet (doğal) | "Bu kaynağı sil" — zaten silinmişse tekrar silmek bir şey değiştirmez |
| **PATCH** | Duruma göre | "Fiyatı 150 yap" → idempotent. "Fiyatı +10 artır" → DEĞİL |
| **POST** | Hayır | "Yeni kayıt oluştur" — her çağrıda yeni kayıt oluşur |

**Sorun POST'ta:** POST doğası gereği idempotent değil. Ama "sipariş oluştur", "ödeme yap" gibi kritik POST işlemlerini idempotent yapmak **zorunlu.** Bunu uygulama katmanında sağlaman lazım.

---

## Idempotency Key Pattern — Nasıl Çalışır?

Client her kritik istekte benzersiz bir anahtar (UUID) gönderir. Server bu anahtarı kaydeder — aynı anahtar tekrar gelirse işlemi yapmaz, önceki yanıtı döner.

### Akış

```
İlk istek:
  POST /api/siparisler
  Idempotency-Key: "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  Body: { urunId: 42, adet: 1 }

  Server:
    1. Redis'te "idemp:a1b2c3d4..." key'i var mı? → YOK
    2. İşlemi yap (sipariş oluştur)
    3. Yanıtı Redis'e kaydet: key → { status: 201, body: { siparisId: 99 } }
    4. Client'a 201 Created dön

Tekrar eden istek (retry veya çift tıklama):
  POST /api/siparisler
  Idempotency-Key: "a1b2c3d4-e5f6-7890-abcd-ef1234567890"  ← AYNI KEY
  Body: { urunId: 42, adet: 1 }

  Server:
    1. Redis'te "idemp:a1b2c3d4..." key'i var mı? → VAR
    2. İşlemi YAPMA
    3. Kayıtlı yanıtı döndür: 201 Created, { siparisId: 99 }
    → Client fark etmez, aynı yanıtı alır, sipariş çift oluşmaz
```

### Key'i Kim Üretir?

**Client üretir** — her yeni işlem için yeni UUID. Aynı işlemi retry ediyorsa aynı key'i gönderir.

```javascript
// Frontend / Mobile:
const idempotencyKey = crypto.randomUUID(); // "a1b2c3d4-..."
await fetch("/api/siparisler", {
  method: "POST",
  headers: { "Idempotency-Key": idempotencyKey },
  body: JSON.stringify({ urunId: 42, adet: 1 })
});
// Timeout oldu? Aynı key ile retry at → server tekrar oluşturmaz
```

---

## ASP.NET Core'da Implementasyon

### Endpoint Filter ile

```csharp
public class IdempotencyFilter : IEndpointFilter
{
    private readonly IConnectionMultiplexer _redis;

    public IdempotencyFilter(IConnectionMultiplexer redis) => _redis = redis;

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // 1. Header'dan key al
        if (!httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var keyHeader))
        {
            return TypedResults.BadRequest("Idempotency-Key header zorunlu");
            // ne yapar → key göndermeyen client'ı reddeder
            // neden zorunlu → kritik endpoint'lerde idempotency garantisi olmalı
        }

        var key = $"idemp:{keyHeader}";
        var db = _redis.GetDatabase();

        // 2. Redis'te bu key var mı?
        var cached = await db.StringGetAsync(key);
        if (cached.HasValue)
        {
            // Daha önce işlenmiş — kayıtlı yanıtı dön
            var cachedResponse = JsonSerializer.Deserialize<CachedResponse>(cached!);
            httpContext.Response.StatusCode = cachedResponse!.StatusCode;
            return TypedResults.Json(cachedResponse.Body, statusCode: cachedResponse.StatusCode);
            // ne yapar → işlemi tekrar YAPMADAN önceki yanıtı döner
            // bunu yazmasaydık → aynı sipariş 2 kez oluşur
        }

        // 3. İlk kez — işlemi yap
        var result = await next(context);

        // 4. Yanıtı cache'le (başarılı ise)
        var statusCode = httpContext.Response.StatusCode;
        if (statusCode >= 200 && statusCode < 300)
        {
            var toCache = new CachedResponse { StatusCode = statusCode, Body = result };
            await db.StringSetAsync(key,
                JsonSerializer.Serialize(toCache),
                TimeSpan.FromHours(24));
            // ne yapar → başarılı yanıtı 24 saat saklayır
            // TTL 24 saat → bu süre içinde aynı key gelirse cache'ten döner
        }
        // 5xx hata olursa CACHE'LEME — retry sonucu değişebilir

        return result;
    }
}

public record CachedResponse
{
    public int StatusCode { get; init; }
    public object? Body { get; init; }
}

// Endpoint'e uygula:
app.MapPost("/api/siparisler", async (SiparisDto dto, ISiparisService svc) =>
{
    var siparis = await svc.OlusturAsync(dto);
    return TypedResults.Created($"/api/siparisler/{siparis.Id}", siparis);
})
.AddEndpointFilter<IdempotencyFilter>();
// ne yapar → bu endpoint artık idempotent, çift tıklama/retry güvenli
```

---

## Başarısız Response Cache'lenir mi? (Kritik Karar)

| Status Code | Cache'le? | Neden |
|-------------|-----------|-------|
| **2xx (başarılı)** | EVET | İşlem yapıldı, aynı sonucu dön |
| **4xx (client hatası)** | EVET | Aynı yanlış istek tekrar gelirse aynı hata dönmeli |
| **5xx (server hatası)** | HAYIR | Geçici hata olabilir — retry sonucu farklı olabilir |

```csharp
// 5xx cache'lenmemeli:
if (statusCode >= 500)
{
    // Cache'leme — bir sonraki retry başarılı olabilir
    // Eğer 500'ü cache'lersen → server düzelmiş olsa bile client hep 500 alır
}
```

**Ama dikkat:** 400 Bad Request cache'lersen — client aynı yanlış veriyi gönderdiğinde hızlıca "bu zaten reddedildi" diyebilirsin. Gereksiz iş yapılmaz.

---

## TTL: Ne Kadar Süre Saklansın?

| Senaryo | Önerilen TTL | Neden |
|---------|-------------|-------|
| Ödeme işlemi | 24-48 saat | Network retry uzun sürebilir, kullanıcı saatler sonra tekrar deneyebilir |
| Sipariş oluşturma | 24 saat | Aynı gün içinde çift tıklama koruması yeterli |
| SMS/e-posta gönderimi | 1 saat | Kısa süreli retry yeterli |
| Genel API | 1-24 saat | Kullanım kalıbına göre |

**TTL çok kısa olursa:** Key expire olur → aynı key ile gelen retry yeni işlem yaratır (idempotency kırılır).
**TTL çok uzun olursa:** Redis bellek şişer, gereksiz key birikir.

---

## IdempotentAPI NuGet Paketi — Hazır Çözüm

Her şeyi sıfırdan yazmak istemiyorsan:

```csharp
// NuGet: IdempotentAPI
// Controller'da attribute ile:
[HttpPost]
[Idempotent(ExpireHours = 24)]
public async Task<IActionResult> SiparisOlustur(SiparisDto dto)
{
    var siparis = await _service.OlusturAsync(dto);
    return CreatedAtAction(nameof(Get), new { id = siparis.Id }, siparis);
}
// ne yapar → Idempotency-Key header'ını otomatik kontrol eder
// cache mekanizması built-in (distributed cache kullanır)
// bunu yazmasaydık → elle filter yazmak gerekir (yukarıdaki gibi)
```

**Ne zaman elle yaz, ne zaman paket kullan?**
- Basit senaryo, az endpoint → paket yeterli
- Özel logic (belirli status code'ları cache'leme, custom key extraction) → elle yaz

---

## Consumer Tarafında Idempotency — Inbox Pattern

API tarafı çözdük. Peki message queue (RabbitMQ, Kafka) tarafı? Queue mesajı tekrar teslim edebilir (at-least-once delivery). Consumer aynı mesajı 2 kez işlememeli.

### Sorun

```
Producer → Queue → Consumer
                    ↓
              Mesajı işle (stok düş)
              ACK gönder ← network koptu!
                    ↓
              Queue: "ACK almadım, tekrar göndereyim"
              Consumer: aynı mesajı tekrar alır → stok tekrar düşer!
```

### Çözüm: Inbox Pattern (Processed Messages Tablosu)

```csharp
// Inbox tablosu:
public class ProcessedMessage
{
    public Guid MessageId { get; set; }     // mesajın benzersiz ID'si
    public DateTime ProcessedAt { get; set; }
    public string MessageType { get; set; } = null!;
}

// DbContext'te unique constraint:
modelBuilder.Entity<ProcessedMessage>()
    .HasIndex(m => m.MessageId)
    .IsUnique();
// ne yapar → aynı MessageId iki kez INSERT edilemez (DB garantisi)
```

```csharp
// Consumer'da:
public class SiparisOlusturulduConsumer : IConsumer<SiparisOlusturulduEvent>
{
    private readonly AppDbContext _context;

    public async Task Consume(ConsumeContext<SiparisOlusturulduEvent> ctx)
    {
        var messageId = ctx.MessageId ?? Guid.NewGuid();

        // 1. Bu mesaj daha önce işlendi mi?
        var alreadyProcessed = await _context.ProcessedMessages
            .AnyAsync(m => m.MessageId == messageId);

        if (alreadyProcessed)
        {
            // Zaten işlenmiş — tekrar yapma, sadece ACK gönder
            return;
            // ne yapar → aynı mesaj tekrar gelirse sessizce geçer
            // bunu yazmasaydık → stok 2 kez düşer, e-posta 2 kez gider
        }

        // 2. İşlemi yap
        await StokDusAsync(ctx.Message.UrunId, ctx.Message.Adet);

        // 3. Inbox'a kaydet (aynı transaction'da!)
        _context.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId,
            ProcessedAt = DateTime.UtcNow,
            MessageType = nameof(SiparisOlusturulduEvent)
        });

        await _context.SaveChangesAsync();
        // ne yapar → iş + inbox kaydı aynı transaction'da
        // neden aynı transaction → biri başarılı diğeri başarısız olursa tutarsızlık
    }
}
```

### Neden DB Unique Constraint?

```csharp
// Race condition senaryosu:
// İki consumer instance aynı anda aynı mesajı aldı
// İkisi de "AnyAsync → false" gördü (henüz kayıt yok)
// İkisi de işlemi yaptı → çift işlem!

// Unique constraint ile:
// İlk INSERT başarılı
// İkinci INSERT → DbUpdateException (unique violation) → catch et, ignore et
```

```csharp
try
{
    await _context.SaveChangesAsync();
}
catch (DbUpdateException ex) when (IsUniqueViolation(ex))
{
    // Başka bir instance zaten işlemiş — sorun yok, geç
    _context.ChangeTracker.Clear();
}
```

---

## İki Pattern Birlikte — Tam Resim

```
Client                          Server                        Queue / Consumer
  │                               │                               │
  │ POST + Idempotency-Key ──────▶│                               │
  │                               │ Redis'te key var mı?          │
  │                               │ YOK → İşlemi yap             │
  │                               │      → Event yayınla ────────▶│
  │                               │      → Response cache'le      │ MessageId ile inbox kontrol
  │◀────── 201 Created ──────────│                               │ İlk kez → işle + inbox yaz
  │                               │                               │
  │ (Retry — aynı key) ─────────▶│                               │
  │                               │ Redis'te key VAR              │
  │◀────── 201 (cache'ten) ──────│                               │
  │                               │                               │ (Mesaj tekrar teslim)
  │                               │                               │ Inbox'ta VAR → atla
```

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de idempotency yok. Kullanıcı formu çift tıklarsa 2 kitap eklenir. 500 kullanıcıda fark etmezsin (nadir olay). 50K'da:
- Ödeme sayfasında çift tıklama → çift ödeme → müşteri şikayeti → para iadesi
- Queue mesajı tekrar teslim → stok yanlış → satılamayan ürün satılmış görünür

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| Idempotency key (ödeme/sipariş) | İyi alışkanlık | Zorunlu — çift ödeme riski |
| Tüm POST'larda idempotency | Gereksiz | Kritik endpoint'lerde |
| Inbox pattern (consumer) | Gereksiz (queue yok) | At-least-once delivery varsa zorunlu |
| IdempotentAPI paketi | Yeterli | Özel ihtiyaç varsa elle yaz |
| TTL stratejisi | 24 saat standart | Senaryoya göre ayarla |

---

## Kontrol Soruları

1. Idempotency nedir? Neden POST doğası gereği idempotent değildir?
2. Idempotency key'i kim üretir (client mı server mı)? Neden?
3. 5xx yanıt neden cache'lenmemeli? 4xx neden cache'lenebilir?
4. TTL çok kısa olursa ne olur? Çok uzun olursa?
5. Inbox pattern nedir? Neden DB unique constraint gerekli?
6. İş mantığı + inbox kaydı neden aynı transaction'da olmalı?
7. İki consumer aynı anda aynı mesajı aldığında ne olur?
