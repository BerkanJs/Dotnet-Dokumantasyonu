# Gün 117 — SignalR: Scale-Out, Authentication ve Resilience

---

## Bu Ders Neden Var?

Önceki iki günde SignalR'ın temellerini ve hub kullanımını gördük. Ama bunlar **tek bir sunucuda** çalışıyor. Production'da gerçek dünya farklı:

- 3 uygulama sunucusu var, load balancer arkasında
- Kullanıcı Server A'ya bağlı, mesaj Server B'den geliyor — A'daki kullanıcı bunu nasıl görecek?
- Bir server çökerse, üzerindeki bağlantılar ne olur?
- Sunucular yeniden başlarken, mevcut connection'lar düzgün handle edilir mi?

Ayrıca güvenlik tarafı: SignalR connection'larında kimlik doğrulama nasıl çalışıyor? Cookie auth, JWT — hangisi nasıl yapılandırılır?

Son olarak: bağlantı kopması production'da kaçınılmaz. Client otomatik nasıl yeniden bağlanır? Mesaj kaybı olmadan nasıl recover edilir?

Bugün bu üç konuya bakacağız: **scale-out**, **authentication**, **resilience**.

---

## Scale-Out Problemi

### Tek Server — Sorunsuz

Tek bir SignalR sunucun varsa her şey basit:
- Tüm bağlantılar bu sunucuda
- Hub'da `Clients.All` denirsen, server kendi listesindeki herkese gönderir
- `Clients.Group("oda")` — yine yerel grup üyeleri

In-memory state — hızlı, basit, sorunsuz.

### Çok Server — İletişim Bozulması

3 sunucu varsa:
```
Server A: Berkan, Ayşe bağlı
Server B: Mehmet, Fatma bağlı
Server C: Emre, Ali bağlı
```

Berkan mesaj gönderdi. Bu mesaj Server A'ya geldi. Server A `Clients.All.SendAsync(...)` çağırdı.

**Server A sadece kendi bağlı kullanıcılarını biliyor.** Mesaj sadece Ayşe'ye gidiyor. Mehmet, Fatma, Emre, Ali → görmüyor.

`Clients.Group("oda")` da aynı şekilde — her server kendi grup üyelerini biliyor. Server B'deki Mehmet aynı odadaysa bilgisi yok.

Bu temel sorun: **server'lar birbirinden habersiz**.

### Çözüm: Backplane

Server'lar arası iletişim için merkezi bir mesaj kanalı gerekiyor. Buna **backplane** deniyor. SignalR mesajları backplane üzerinden tüm server'lara dağıtılıyor.

```
Berkan → Server A → backplane → Server A, B, C → tüm kullanıcılar
```

Server A "tüm clientlara gönder" dediğinde:
1. Kendi clientlarına direkt gönderir
2. Aynı mesajı backplane'e yayınlar
3. Server B ve C backplane'i dinliyor, mesajı alır
4. Kendi clientlarına iletir

Sonuç: tüm clientlar mesajı alır, hangi server'a bağlı olduğu fark etmez.

---

## Redis Backplane

En yaygın backplane çözümü Redis. Pub/Sub mekanizmasını kullanıyor.

### Kurulum

```csharp
// NuGet: Microsoft.AspNetCore.SignalR.StackExchangeRedis
builder.Services.AddSignalR()
    .AddStackExchangeRedis("localhost:6379", options =>
    {
        options.Configuration.ChannelPrefix = "ChatApp:";
        // ne yapar → tüm SignalR mesajları "ChatApp:" prefix'i ile Redis'e gider
        // bunu yazmasaydık → aynı Redis'i kullanan başka uygulama varsa mesaj karışıklığı
    });
```

Bu kadar. Kod hiçbir yerde değişmedi. Hub method'ları aynı şekilde yazılıyor. Arkada SignalR otomatik olarak:
- Her mesajı Redis pub/sub'a yayınlıyor
- Diğer server'lar bu kanalı dinliyor
- Mesajlar tüm server'lara dağılıyor

### Pub/Sub Mantığı

Redis'in pub/sub'ı şöyle çalışıyor:
- Subscriber'lar bir kanala abone oluyor
- Publisher kanala mesaj yayınlıyor
- Tüm subscriber'lar mesajı anlık olarak alıyor

```
Server A: SUBSCRIBE ChatApp:messages
Server B: SUBSCRIBE ChatApp:messages
Server C: SUBSCRIBE ChatApp:messages

Server A: PUBLISH ChatApp:messages {mesaj}
              ↓
Server A, B, C → mesajı aldı
```

### Pub/Sub'ın Sınırı: Persistence Yok

Redis pub/sub mesajları saklamaz. Yayınlanan an mevcut subscriber'lar alır, sonra biter. Eğer Server B o anda bağlı değilse mesajı kaybeder.

Bu SignalR backplane için kabul edilebilir mi? Genelde evet:
- Server'lar uzun süreli bağlantı tutuyor
- Bir mesajı kaybeden server, kendi clientlarına o mesajı iletemez — sadece o clientlar etkilenir
- Mesaj kaybı UX bozulur ama sistem çökmez

Eğer mesaj kaybı kabul edilemiyorsa SignalR backplane uygun değil — persistent mesajlaşma (RabbitMQ, Kafka) gerekiyor.

### Redis Backplane Sınırları

**1. Throughput tavanı.**

Redis pub/sub tek sunucudan birden fazla server'a fanout yapıyor. 10-20 server için sorun yok. 100+ server için Redis CPU bottleneck olmaya başlıyor.

Yüksek ölçekte: Azure SignalR Service (managed) veya çoklu Redis cluster gerekebilir.

**2. Latency.**

Her mesaj backplane'den geçiyor — ~1-2 ms ek latency. Tek server'lı senaryoda mesaj direkt giderken, backplane'le birlikte ekstra round-trip.

Bu çoğu uygulama için sorun değil ama anlık reaksiyon (gerçek zamanlı oyun, finans) için önemli.

**3. Tüm mesajlar tüm server'lara gidiyor.**

`Clients.User("berkan")` çağırıyorsun. Sadece Berkan'ın bağlı olduğu Server B'ye gitmesi yeterli. Ama backplane mesajı A, B, C — hepsine yayınlıyor. A ve C "Berkan'ı tanıyor muyum?" diye bakıyor, hayır, drop ediyor.

Bu network ve CPU israfı. Düşük ölçekte fark etmez, yüksek ölçekte hissedilir.

---

## Azure SignalR Service — Managed Çözüm

SignalR backplane'i kendin yönetmek istemiyorsan Azure'un servisi var. **Azure SignalR Service** — SignalR'ı bulutta managed olarak sağlıyor.

### Mimari Farkı

Normal SignalR'da clientlar **uygulamanın sunucularına** bağlanıyor:
```
Client ← WebSocket → ASP.NET Core Sunucusu (Hub)
```

Azure SignalR Service'te clientlar **Azure'a** bağlanıyor:
```
Client ← WebSocket → Azure SignalR Service ← REST → ASP.NET Core Sunucusu
```

Uygulama sunucun WebSocket bağlantıları tutmuyor. Sadece REST API ile Azure'a "şu kullanıcıya şu mesajı gönder" diyor. Azure asıl bağlantıları yönetiyor.

### Avantajları

**Connection limiti yok.** Tek sunucu 5K-10K bağlantı tutabilir. Azure SignalR 100K+ bağlantıyı kolayca yönetir.

**Sunucu yükü düşer.** WebSocket'lerin overhead'i Azure tarafında. Uygulama sunucun rahat.

**Yüksek availability.** Azure servisi SLA garantisi veriyor.

**Auto-scaling.** Bağlantı sayısına göre otomatik ölçekleniyor.

### Dezavantajları

**Maliyet.** Aylık ücret var, connection sayısı arttıkça artıyor.

**Cloud bağımlılığı.** Azure'a bağlanıyorsun. On-premise veya başka cloud için kullanılmaz.

**Yapılandırma:** Bazı SignalR özellikleri (örn: `IUserIdProvider`) Azure ile farklı çalışıyor.

### Kurulum

```csharp
// NuGet: Microsoft.Azure.SignalR
builder.Services.AddSignalR()
    .AddAzureSignalR("Endpoint=https://...;AccessKey=...");
// ne yapar → Azure SignalR Service'i backplane olarak kullanır
// Kendi sunucun bağlantıları yönetmiyor, Azure yönetiyor
```

Tek satır değişiklik. Geri kalan SignalR kodu aynı.

---

## Authentication — Kim Bağlanıyor?

SignalR HTTP üstünde başlıyor — ASP.NET Core auth ile entegre. Üç yaygın senaryo:

### 1. Cookie Authentication

Sayfa zaten cookie ile giriş yaptı, SignalR aynı cookie'yi otomatik kullanır:

```csharp
// Hub'da auth zorunlu:
[Authorize]
public class ChatHub : Hub
{
    public override Task OnConnectedAsync()
    {
        var userName = Context.User?.Identity?.Name;
        // ne yapar → cookie'den çözülmüş kullanıcı bilgisi
        return base.OnConnectedAsync();
    }
}
```

Client tarafında:
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub")   // browser cookie'leri otomatik gönderir
    .build();
```

**Sınır:** SPA'lar veya mobil app cookie kullanmıyorsa bu çalışmaz.

### 2. JWT Bearer Authentication

Modern SPA/mobile için. Client JWT token'ı header olarak gönderir.

**Sorun:** WebSocket handshake'inde **HTTP header'lar nasıl gönderilir?** Tarayıcı WebSocket API'sı custom header desteklemiyor (tarayıcı sınırı).

**Çözüm:** SignalR JWT'yi query string'den okuyabiliyor:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = ...;

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
                {
                    context.Token = accessToken;
                    // ne yapar → /chathub endpoint'inde query string'den token al
                    // bunu yazmasaydık → SignalR WebSocket bağlantısında auth çalışmazdı
                }
                return Task.CompletedTask;
            }
        };
    });
```

Client tarafında token query string olarak ekleniyor:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub", {
        accessTokenFactory: () => getAccessToken()
        // SignalR otomatik olarak ?access_token=... ekler
    })
    .build();
```

**Güvenlik dikkati:** Query string log'larda görünebilir. Token kısa ömürlü olmalı (refresh ile yenileyerek), HTTPS zorunlu.

### 3. IUserIdProvider — Custom User ID

`Clients.User("kullaniciId")` çağırdığında SignalR hangi connection'ın hangi user'a ait olduğunu biliyor. Bu eşleşme nasıl?

Varsayılan: `User.Identity.Name`. Ama bazen başka claim'i user ID olarak kullanmak istersin.

```csharp
public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst("sub")?.Value;
        // ne yapar → JWT'deki "sub" claim'ini user ID olarak kullan
        // bunu yazmasaydık → User.Identity.Name kullanılırdı
        // ne zaman lazım → "name" boş ama "sub" varsa, veya custom ID gerekliyse
    }
}

// Kayıt:
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
```

Artık `Clients.User("sub-value")` çağrısı doğru connection'lara gidiyor.

### 4. Authorization Policy

Sadece auth değil, role/policy bazlı yetki:

```csharp
[Authorize(Policy = "RequireAdmin")]
public class AdminHub : Hub
{
    // sadece admin role'üne sahip kullanıcılar bağlanabilir
}
```

Method bazlı yetki:
```csharp
[Authorize(Roles = "Admin")]
public async Task DeleteRoom(string roomName) { ... }
// ne yapar → sadece admin role'üne sahip kullanıcılar bu metodu çağırabilir
```

---

## Resilience — Bağlantı Kopmaları

Internet stabil değil. Mobil network değişiyor. WiFi'den 4G'ye geçiş oluyor. Sunucu deploy oluyor. Bağlantı kopması üretimde sürekli yaşanıyor.

### Otomatik Yeniden Bağlanma

SignalR client'ı otomatik reconnect destekliyor:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub")
    .withAutomaticReconnect()
    // ne yapar → bağlantı kopunca 0, 2, 10, 30 saniye gecikmelerle dener
    // sonra durur (varsayılan davranış)
    .build();
```

Custom retry delays:
```javascript
.withAutomaticReconnect([0, 2000, 5000, 10000, 30000, 60000])
// 0 sn (anında), 2 sn, 5 sn, 10 sn, 30 sn, 60 sn — sonra dur
```

Sonsuz retry:
```javascript
.withAutomaticReconnect({
    nextRetryDelayInMilliseconds: retryContext => {
        // her zaman 5 saniye sonra dene
        return 5000;
    }
})
```

### Reconnect Event'leri

Bağlantı durumlarını dinleyebilirsin:

```javascript
connection.onreconnecting(error => {
    console.log("Bağlantı koptu, yeniden deneniyor...");
    showWarning("Bağlantı koptu");
});

connection.onreconnected(connectionId => {
    console.log("Yeniden bağlandı:", connectionId);
    hideWarning();
    // ÖNEMLİ: yeni connection ID, eski groupları kaybettin
    // bağlandığın oda/grup state'ini yeniden kurmak gerekebilir
});

connection.onclose(error => {
    console.log("Bağlantı kapandı:", error);
    showError("Bağlantı kapatıldı, yenile sayfayı");
});
```

### Reconnect Sonrası State Restoration

Önemli detay: **reconnect olduğunda yeni connection ID alıyorsun.** Eski ConnectionId artık geçerli değil.

Bu şu demek:
- Eski connection'ın grup üyelikleri yok (Groups.AddToGroupAsync ile eklediklerin)
- Server tarafındaki `Context.Items` state'i kayboldu
- Eski connection ID ile gönderilen son mesajlar muhtemelen alınamadı

**Çözüm pattern'i:** Server `OnConnectedAsync`'te state'i restore eder.

```csharp
public override async Task OnConnectedAsync()
{
    var userId = Context.UserIdentifier;
    if (userId is null) return;

    // Kullanıcının ait olduğu odaları DB'den çek:
    var rooms = await _roomService.GetUserRoomsAsync(userId);
    foreach (var room in rooms)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{room.Id}");
        // ne yapar → reconnect sonrası eski oda üyeliklerini tekrar kur
        // bunu yazmasaydık → kullanıcı reconnect sonrası mesaj alamaz
    }

    await base.OnConnectedAsync();
}
```

**Alternatif:** Stateful Reconnect (ASP.NET Core 8+). Bağlantı kopmasında server kısa süre (60-300 sn) state'i koruyor. Reconnect aynı connection ID ile başarılı olursa, kaybedilen mesajlar replay ediliyor. (Hâlâ relatively yeni özellik.)

```csharp
builder.Services.AddSignalR(options =>
{
    options.StatefulReconnectBufferSize = 1000;
});

// Client'ta da aktif etmen lazım:
// connection.useStatefulReconnect()
```

### Keep-Alive ve Timeout Ayarları

SignalR varsayılan olarak keep-alive ping gönderiyor. Bağlantının yaşadığını kontrol etmek için.

```csharp
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    // ne yapar → server her 15 saniyede ping gönderir
    // varsayılan 15 sn — çoğu senaryo için iyi

    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    // ne yapar → 30 sn boyunca client'tan mesaj gelmezse bağlantı düşer
    // varsayılan 30 sn — keep-alive'ın 2 katı olmalı

    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    // ne yapar → ilk bağlantıda handshake için maks bekleme süresi
});
```

**Trade-off:**
- Kısa timeout → kopuk bağlantılar hızlı tespit edilir, ama mobil network'te yanlış pozitif olabilir
- Uzun timeout → kopukluğu geç fark edersin, sunucuda gereksiz state birikir

Mobile ağırlıklı uygulamada timeout'u biraz uzun tut (60-90 sn). Stabil internet'te 30 sn fine.

### Server-Side Reconnect Strategy

Client otomatik reconnect ediyor — peki server tarafında dikkat etmen gereken şeyler?

**1. Idempotency.** Mesaj kaybı veya çoğaltma olabilir. Önemli işlemler idempotent olmalı:
```csharp
public async Task SendMessage(string messageId, string content)
{
    // messageId benzersiz — aynı mesaj iki kez gelirse ignore
    if (await _msgRepo.ExistsAsync(messageId)) return;

    await _msgRepo.SaveAsync(new Message { Id = messageId, Content = content });
    await Clients.Others.SendAsync("NewMessage", messageId, content);
}
```

**2. Graceful disconnect.** Sunucu deploy olurken bağlantıları "gracefully" kapatma:
```csharp
// Application shutdown'da:
public class GracefulShutdown : IHostedService
{
    private readonly IHubContext<ChatHub> _hub;

    public async Task StopAsync(CancellationToken ct)
    {
        await _hub.Clients.All.SendAsync("ServerShutdown",
            "Sunucu yeniden başlatılıyor, lütfen bekleyin",
            cancellationToken: ct);
        // Client'lara önceden haber ver, otomatik reconnect bekleyebilir
    }
}
```

**3. Health check.** Hub'ın çalışıp çalışmadığını monitör et (Gün 93):
```csharp
builder.Services.AddHealthChecks()
    .AddSignalRHub("https://localhost/chathub", "chathub");
// ne yapar → SignalR hub'a bağlanabilme kontrolü
```

---

## Sticky Sessions ve Long Polling

WebSocket destekli ortamda bu sorun olmaz. Ama long polling fallback'inde önemli bir konu var.

**Long polling**: Client ardışık HTTP istekleri yapıyor. Her istek farklı bir sunucuya düşebilir.

```
İstek 1 (Server A) → state oluştu
İstek 2 (Server B) → A'daki state'i bilmiyor → hata
```

**Çözüm: Sticky sessions.** Load balancer aynı client'ı hep aynı server'a yönlendirir. Cookie veya IP bazlı.

ASP.NET Core SignalR sticky session'a ihtiyaç duyuyor (long polling kullanıyorsa). Çoğu cloud load balancer (Azure Application Gateway, AWS ALB) bunu destekliyor — config'te aktif edilmesi yeterli.

WebSocket bağlantıları zaten sticky (bir kez kuruldu, aynı sunucuya devam ediyor). Sorun sadece initial handshake ve fallback senaryosunda.

---

## Production Checklist

SignalR'ı production'a çıkarırken:

- [ ] **Backplane:** 1'den fazla sunucu varsa Redis veya Azure SignalR Service
- [ ] **Authentication:** Hub'lara `[Authorize]` koy, JWT için query string handling
- [ ] **Automatic reconnect:** Client'ta `withAutomaticReconnect()` aktif
- [ ] **State restoration:** Reconnect sonrası group/state yeniden kuruluyor
- [ ] **Keep-alive ve timeout:** Network koşullarına göre ayarlandı
- [ ] **Sticky sessions:** Load balancer ayarı doğru
- [ ] **HTTPS:** WebSocket'lerde WSS zorunlu
- [ ] **Rate limiting:** Hub method'larında abuse koruması (Gün 89)
- [ ] **Connection limits:** Sunucu başına maksimum bağlantı sayısı set edilmiş
- [ ] **Monitoring:** Bağlantı sayısı, mesaj throughput, hata oranı log/metric'leniyor
- [ ] **Graceful shutdown:** Deploy sırasında client'lara bildirim gidiyor
- [ ] **Health check:** SignalR endpoint için health check var

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC tek instance — scale-out konsepti alakasız. Auth Identity cookie ile basit.

50K kullanıcıda 3 instance ortamında:
- Backplane (Redis) zorunlu — yoksa cross-server mesaj iletilmez
- JWT auth WebSocket için query string ile yönetiliyor
- Reconnect handling kritik — mobil kullanıcılar sürekli kopuyor
- Sticky session load balancer'da config'lenmiş
- Connection monitoring ile "kaç kullanıcı aktif" anlık görülüyor

---

## 500 vs 50K Kullanıcı

| Konu | 500 kullanıcı/ay | 50K kullanıcı/ay |
|------|-------------------|-------------------|
| Tek instance | Yeterli | Yetmez, scale-out şart |
| Redis backplane | Gereksiz | Zorunlu |
| Azure SignalR | Maliyet anlamsız | 50K+ bağlantıda değerli |
| JWT auth | Cookie yeterli | API-first ise JWT zorunlu |
| Auto reconnect | İyi alışkanlık | Şart, mobil ağırlıklı kullanıcılarda |
| Sticky sessions | İhtimal yok (tek server) | LB'de aktif olmalı |
| Stateful reconnect | Yeni özellik, deneme | Yaygın production'a girdiğinde değerli |

---

## Kontrol Soruları

1. Çoklu server senaryosunda backplane neden gerekli? Backplane olmadan ne sorun çıkar?
2. Redis pub/sub'ın persistence olmaması SignalR backplane için neden kabul edilebilir?
3. Azure SignalR Service ile self-hosted Redis backplane arasındaki temel farklar nelerdir?
4. WebSocket handshake'inde JWT token nasıl gönderilir? Neden header değil query string?
5. `IUserIdProvider` ne işe yarar? Ne zaman custom implementasyon gerekir?
6. Reconnect sonrası neden state restoration gerekiyor? Stateful Reconnect bunu nasıl iyileştiriyor?
7. Keep-alive interval ile client timeout interval arasındaki ilişki nedir?
8. Sticky sessions WebSocket'te neden gerekli olmuyor ama long polling'de gerekli?
9. Production'da SignalR çalıştırırken monitoring için hangi metrikler kritik?
