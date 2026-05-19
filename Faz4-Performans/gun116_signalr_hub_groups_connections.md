# Gün 116 — SignalR: Hub Pattern, Groups ve Connections

---

## Bu Ders Neden Var?

Dün SignalR'ın ne olduğunu, real-time iletişim teknolojilerini gördük. Bugün asıl koda iniyoruz: **Hub** yazma, kullanıcılara mesaj gönderme, grupları yönetme.

Bu derste asıl odaklanacağımız şey: SignalR'ın "kim kime mesaj gönderir?" sorusunun ne kadar farklı cevabı var. Tek bir kullanıcı, bir grup, herkes, belirli bir bağlantı — her biri farklı API çağrısı.

---

## Hub Nedir? — Detaylı Tanım

Hub, SignalR'ın merkezi soyutlaması. Şöyle düşün:
- ASP.NET Core'da REST controller var — istemci REST endpoint'e istek atıyor, controller cevap veriyor
- SignalR'da Hub var — istemci hub'daki bir metodu çağırıyor, hub diğer istemcilere mesaj gönderiyor

REST controller'dan farkı: **hem istemciden çağrı alır hem istemciye çağrı yapar.** Tek yönlü request-response değil, iki yönlü RPC.

```csharp
public class ChatHub : Hub
{
    // Client'tan gelen çağrı:
    public async Task SendMessage(string user, string message)
    {
        // Bu metod istemciden çağrılıyor
        // İçinde de istemcilere mesaj gönderebiliyoruz:
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
```

Yukarıdaki kod:
- `SendMessage` → client tarafından invoke edilen metod (HTTP POST'a benzer)
- `Clients.All.SendAsync("ReceiveMessage", ...)` → server'dan client'lara giden çağrı

İki yön de aynı sınıfta. İşte hub bu yüzden "merkez" — iki yönlü trafik buradan geçer.

---

## Hub'ı Kaydetme ve Endpoint

```csharp
// Program.cs
builder.Services.AddSignalR();
// ne yapar → SignalR servislerini DI'ya kaydeder

var app = builder.Build();
app.MapHub<ChatHub>("/chathub");
// ne yapar → ChatHub'ı /chathub URL'inde dinler hale getirir
// istemciler bu URL'e WebSocket bağlantısı kuracak
```

Client tarafında bu URL'e bağlanıyor:
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub")
    .build();
await connection.start();
```

Server'da `Hub` sınıfı, client'ta `HubConnection` — ikisi birlikte çalışıyor.

---

## Server → Client Mesajları — "Clients" Property

Hub'ın içinde `Clients` adında bir property var. Bu property, kime mesaj gönderileceğini belirler. Çok çeşitli seçenekler var:

### Clients.All — Herkese

```csharp
await Clients.All.SendAsync("DuyuruVar", "Sunucu yeniden başlatılacak");
```

Hub'a bağlı **tüm istemcilere** mesaj gönderir. Sistem duyuruları, anonim broadcast için.

### Clients.Caller — Sadece Mesajı Gönderen

```csharp
public async Task BeniBilgilendir()
{
    await Clients.Caller.SendAsync("Bilgi", "Sadece sana özel mesaj");
    // ne yapar → metodu çağıran istemciye geri dönüş
    // diğer bağlı istemciler bu mesajı görmez
}
```

Kullanım: Client işlem yaptı, ona özel cevap göndereceksin. Hatayı bildirmek, işlem sonucunu döndürmek.

### Clients.Others — Mesajı Gönderen Hariç Herkese

```csharp
public async Task SendMessage(string message)
{
    var userName = Context.User?.Identity?.Name;
    await Clients.Others.SendAsync("YeniMesaj", userName, message);
    // ne yapar → mesajı gönderen DIŞINDA herkese gönder
    // gönderici zaten mesajı kendi ekranında gördü, geri yansıma istemiyor
}
```

Chat uygulamalarında klasik pattern: "Berkan mesajı yazdı, ekranda zaten görüyor. Diğer kullanıcılara göstereceğim."

### Clients.Client(connectionId) — Belirli Bir Bağlantıya

```csharp
await Clients.Client("connection-id-123").SendAsync("OzelMesaj", "Sana özel");
```

Belirli bir bağlantı ID'sini biliyorsan o bağlantıya özel mesaj. Genelde kullanıcı eşleştirme için (kullanıcı X'in connection ID'si neydi?) ek bir tablo tutmak gerekiyor.

### Clients.Clients(connectionIds) — Birden Fazla Belirli Bağlantı

```csharp
await Clients.Clients(new[] { "conn-1", "conn-2", "conn-3" })
    .SendAsync("Bildirim", "Sizlere özel");
```

Belirli bir liste — birkaç connection ID'sine birden mesaj.

### Clients.User(userId) — Belirli Kullanıcının Tüm Bağlantıları

```csharp
await Clients.User("berkan").SendAsync("YeniMesaj", message);
// ne yapar → "berkan" adlı kullanıcının TÜM açık bağlantılarına gönder
// kullanıcı 3 sekme açtıysa 3'ünde de mesaj görünür
```

Bir kullanıcının birden fazla bağlantısı olabilir (birden fazla sekme, birden fazla cihaz). `Clients.User` hepsine birden gönderiyor.

Bunu kullanmak için SignalR'ın "user identifier" bilmesi gerekiyor. Authentication varsa otomatik geliyor — `User.Identity.Name` user ID olarak kullanılıyor. Custom user ID istiyorsan `IUserIdProvider` implement edersin (yarın göreceğiz).

### Clients.Group(groupName) — Belirli Bir Gruba

```csharp
await Clients.Group("chat-room-42").SendAsync("YeniMesaj", message);
// ne yapar → "chat-room-42" grubundaki tüm bağlantılara gönder
```

Grupları birazdan göreceğiz. Chat odaları, takım kanalları, vs.

### Karşılaştırma

| Hedef | Kime gider | Ne zaman kullan |
|-------|------------|------------------|
| All | Tüm istemciler | Sistem geneli duyuru |
| Caller | Mesajı gönderen | İşlem sonucu, özel cevap |
| Others | Gönderen hariç | Chat broadcast, kullanıcı eylem bildirimi |
| Client(id) | Tek bağlantı | Spesifik connection |
| User(id) | Bir kullanıcının tüm bağlantıları | Kişisel bildirim |
| Group(name) | Gruptaki herkes | Oda, kanal mesajı |

---

## Client → Server Çağrıları

Hub'daki public metotlar client tarafından çağrılabilir.

```csharp
public class ChatHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        // bu metod client'tan çağrılır
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    public async Task<int> GetActiveUserCount()
    {
        // client'tan çağrılır, değer döner
        return _userTracker.Count;
    }
}
```

Client tarafında:
```javascript
// Parametresiz veya sade çağrı (server'a istek):
await connection.invoke("SendMessage", "Berkan", "Merhaba");

// Dönüş bekleyen çağrı:
const count = await connection.invoke("GetActiveUserCount");
console.log(`Aktif kullanıcı: ${count}`);
```

Yani server method'unun dönüş tipi `Task` ise client void invoke yapıyor, `Task<T>` ise değer alıyor.

### Method İsimlendirme

Server'da metot adı PascalCase (`SendMessage`), client'ta camelCase olarak çağrılır. SignalR otomatik dönüştürüyor:

```javascript
await connection.invoke("sendMessage", ...);   // çalışır
await connection.invoke("SendMessage", ...);   // bu da çalışır
```

Tutarlılık için tek bir convention seç.

---

## Connection Lifecycle — Bağlantı Yaşam Döngüsü

Her client SignalR'a bağlandığında bir **connection** açıyor. Bu bağlantının yaşam döngüsünü Hub içinde hook'layabilirsin.

```csharp
public class ChatHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Yeni bağlantı geldi
        var userName = Context.User?.Identity?.Name ?? "anonim";
        await Clients.All.SendAsync("UserConnected", userName);

        await base.OnConnectedAsync();
        // ne yapar → bağlantı açıldığında çalışır
        // kim bağlandı, hangi gruba ekle, vs
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userName = Context.User?.Identity?.Name ?? "anonim";
        await Clients.All.SendAsync("UserDisconnected", userName);

        await base.OnDisconnectedAsync(exception);
        // ne yapar → bağlantı koptuğunda çalışır (normal kapanma veya hata)
        // exception null değilse anormal disconnect (network, crash)
    }
}
```

**Önemli kavramlar:**

**`Context.ConnectionId`** — Bu bağlantıya özel benzersiz ID. SignalR otomatik üretiyor.

**`Context.User`** — ASP.NET Core auth ile entegre. Giriş yapmış kullanıcının ClaimsPrincipal'ı.

**`Context.UserIdentifier`** — User'ın "kim olduğunu" temsil eden string. Varsayılan: `User.Identity.Name`. `Clients.User(id)` çağrısı bu identifier'a göre eşleştirme yapıyor.

**`Context.Items`** — Bu bağlantıya özel key-value sözlük. Connection state tutmak için kullanılır (örn: kullanıcının seçtiği oda, dili).

```csharp
public override async Task OnConnectedAsync()
{
    Context.Items["BaglanmaSaati"] = DateTime.UtcNow;
    // bu bağlantı süresince saklanır
    await base.OnConnectedAsync();
}

public async Task GetConnectionDuration()
{
    var start = (DateTime)Context.Items["BaglanmaSaati"]!;
    var duration = DateTime.UtcNow - start;
    await Clients.Caller.SendAsync("Duration", duration.TotalMinutes);
}
```

### Connection != User

Bunu net anlamak önemli. Bir kullanıcı birden fazla connection açabilir:
- Aynı kullanıcı 3 sekme açtı → 3 connection
- Aynı kullanıcı laptop + telefon kullanıyor → 2 connection
- Sayfayı refresh etti → eski connection kapandı, yeni connection açıldı

Yani:
- `ConnectionId` → tek bir cihaz/sekme/oturum
- `UserIdentifier` → kullanıcı (birden fazla connection olabilir)

`Clients.User("berkan")` → Berkan'ın tüm cihazlarındaki tüm sekmelerine gönderir
`Clients.Client(connectionId)` → tek bir cihaza/sekmeye gönderir

---

## Groups — Bağlantı Kümeleme

Connection'ları gruplara koyabilirsin. Bir chat odası gibi düşün.

### Gruba Ekleme/Çıkarma

```csharp
public class ChatHub : Hub
{
    public async Task JoinRoom(string roomName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
        // ne yapar → bu bağlantıyı roomName grubuna ekler
        // aynı bağlantı birden fazla gruba ait olabilir

        await Clients.Group(roomName).SendAsync("UserJoined", Context.User?.Identity?.Name);
        // odaya katılım bildirimi
    }

    public async Task LeaveRoom(string roomName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);

        await Clients.Group(roomName).SendAsync("UserLeft", Context.User?.Identity?.Name);
    }

    public async Task SendToRoom(string roomName, string message)
    {
        var userName = Context.User?.Identity?.Name;
        await Clients.Group(roomName).SendAsync("RoomMessage", userName, message);
        // ne yapar → sadece o grubun üyelerine mesaj
        // diğer odadaki kullanıcılar görmez
    }
}
```

### Önemli Detaylar

**1. Groups bağlantı bazlı, kullanıcı bazlı değil.**

Bir kullanıcının 3 connection'ı varsa, hangisini gruba eklersen sadece o gruba ait. Diğer iki connection'ı eklemen lazım — veya `Clients.User(userId)`'i grup içinde özel olarak handle etmen lazım.

**2. Group state bağlantıyla beraber kaybolur.**

Bağlantı koptuğunda SignalR otomatik olarak o connection'ı tüm gruplardan çıkarır. Sen elle temizleme yapmıyorsun.

**3. Grup adları benzersiz olmalı ve dikkatli isimlendirilmeli.**

```csharp
// İyi naming:
await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");
await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}:notifications");
await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:admins");
```

Prefix kullan, çakışma olmasın.

**4. Grup listesini SignalR senin için tutuyor — sen tutmak zorunda değilsin.**

Hangi connection hangi gruptadır → SignalR biliyor. Sen sadece "ekle" / "çıkar" / "mesaj gönder" diyorsun.

### Grup Senaryoları

**Chat room:**
- Her oda bir grup
- Kullanıcı odaya katılınca → AddToGroup
- Mesaj gönderme → Clients.Group(odaAdi)

**Kullanıcı bildirimleri:**
- Kullanıcı ID bazlı grup: `user:42:notifications`
- Her connection bağlanınca bu gruba eklenir
- Bildirim → Clients.Group("user:42:notifications")

(Alternatif: Clients.User(42) ile aynı sonuç — IUserIdProvider düzgün konfigure edilmişse.)

**Tenant izolasyonu (multi-tenant SaaS):**
- Her tenant'ın kendi grubu: `tenant:acme`
- Tenant geneli duyuru: Clients.Group("tenant:acme")

**Admin paneli:**
- "admins" grubu — sadece admin'lerin connection'ları
- Kritik olayları admin'lere bildir: Clients.Group("admins")

---

## Strongly-Typed Hub'lar

Yukarıdaki örneklerde client method'larını **string** ile çağırıyorduk:

```csharp
await Clients.All.SendAsync("ReceiveMessage", user, message);
```

String typo'su = runtime hatası. "ReciveMessage" yazsan client mesajı almaz, hata bile vermez.

SignalR strongly-typed alternatif sunuyor. Önce client interface'i tanımlıyorsun:

```csharp
public interface IChatClient
{
    Task ReceiveMessage(string user, string message);
    Task UserJoined(string user);
    Task UserLeft(string user);
    Task RoomMessage(string user, string message);
}
```

Sonra Hub bu interface'i generic olarak alıyor:

```csharp
public class ChatHub : Hub<IChatClient>
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.ReceiveMessage(user, message);
        // ne yapar → method call gibi, IntelliSense + compile-time check
        // string yok, typo riski yok
    }

    public async Task SendToRoom(string roomName, string message)
    {
        await Clients.Group(roomName).RoomMessage(Context.User?.Identity?.Name ?? "anon", message);
    }
}
```

**Avantajları:**
- Compile-time tip kontrolü
- IntelliSense (Visual Studio/Rider doğru method'ları öneriyor)
- Refactoring güvenli (interface'i değiştirirsen tüm kullanım yerleri patlar)

**Dezavantajı:**
- Interface'i ayrıca tanımlama gereği (küçük overhead)

Modern projelerde strongly-typed yaklaşım standart kabul ediliyor.

---

## Method Calling Patterns

### Server'dan Client'a Çağrı Türleri

```csharp
// 1. Fire-and-forget — sonucu beklemiyorsun:
await Clients.All.SendAsync("Notify", "Bir şey oldu");
// SendAsync mesajı gönderir, client'ın işlemesini beklemez
// client tarafında yanıt verme imkanı yok

// 2. InvokeAsync — client'tan dönüş bekliyorsun (yeni özellik, .NET 7+):
public async Task<string> AskClient()
{
    var result = await Clients.Caller.InvokeAsync<string>("ClientMethod", "soru", CancellationToken.None);
    // client'a çağrı atıp dönüşünü bekle (15 saniye varsayılan timeout)
    return result;
}
```

`InvokeAsync` özellikle değerli — server'dan client'a "şu bilgiyi getirir misin?" sorusu mümkün. Eskiden client'tan ayrı bir invoke yapması gerekiyordu, artık server da bekleyebiliyor.

### Client'tan Server'a — Streaming

Hub method'u `IAsyncEnumerable` veya `ChannelReader` döndürebilir → stream desteği:

```csharp
public class StockHub : Hub
{
    public async IAsyncEnumerable<int> StreamPrices(
        string symbol,
        [EnumeratorCancellation] CancellationToken ct)
    {
        for (int i = 0; i < 100; i++)
        {
            if (ct.IsCancellationRequested) yield break;

            var price = await GetCurrentPriceAsync(symbol);
            yield return price;
            // ne yapar → her fiyat geldiğinde client'a akıt

            await Task.Delay(1000, ct);
        }
    }
}
```

Client tarafında:
```javascript
const stream = connection.stream("StreamPrices", "AAPL");
stream.subscribe({
    next: (price) => console.log(`Fiyat: ${price}`),
    complete: () => console.log("Stream bitti"),
    error: (err) => console.error(err)
});
```

Bu **server-to-client streaming**. Client-to-server da var (client `IAsyncEnumerable` gönderiyor) ama daha az yaygın.

---

## Bir Tam Örnek — Mini Chat

Hepsini bir araya koyalım. Basit ama production-leyebilir chat:

```csharp
public interface IChatClient
{
    Task ReceiveMessage(string user, string message, DateTime time);
    Task UserJoined(string user, string room);
    Task UserLeft(string user, string room);
    Task RoomList(string[] rooms);
}

[Authorize]
public class ChatHub : Hub<IChatClient>
{
    private static readonly HashSet<string> _activeRooms = new();
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var userName = Context.User?.Identity?.Name ?? "anon";
        _logger.LogInformation("{User} bağlandı: {ConnectionId}", userName, Context.ConnectionId);

        // Aktif oda listesini gönder
        await Clients.Caller.RoomList(_activeRooms.ToArray());

        await base.OnConnectedAsync();
    }

    public async Task JoinRoom(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            throw new HubException("Oda adı boş olamaz");

        _activeRooms.Add(roomName);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomName}");

        var userName = Context.User?.Identity?.Name ?? "anon";
        await Clients.Group($"room:{roomName}").UserJoined(userName, roomName);
    }

    public async Task LeaveRoom(string roomName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room:{roomName}");

        var userName = Context.User?.Identity?.Name ?? "anon";
        await Clients.Group($"room:{roomName}").UserLeft(userName, roomName);
    }

    public async Task SendToRoom(string roomName, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (message.Length > 500)
            throw new HubException("Mesaj 500 karakterden uzun olamaz");

        var userName = Context.User?.Identity?.Name ?? "anon";
        await Clients.Group($"room:{roomName}").ReceiveMessage(userName, message, DateTime.UtcNow);
    }
}
```

**Önemli noktalar:**
- `[Authorize]` — auth zorunlu (yarın detaylanacak)
- `HubException` — client'a hata mesajı gönderir (RpcException benzeri)
- Validation — input zafiyeti olmasın (boş mesaj, çok uzun mesaj)
- `static _activeRooms` — basit örnek. Production'da Redis'te tutulmalı (yarın)

---

## Hata Yönetimi

SignalR'da hata fırlatmak için `HubException`:

```csharp
public async Task SendMessage(string message)
{
    if (string.IsNullOrEmpty(message))
        throw new HubException("Mesaj boş olamaz");
    // ne yapar → client'a hata mesajı gönderir
    // client'taki invoke(...) call'u reject olur, error handler tetiklenir
}
```

Client tarafında:
```javascript
try {
    await connection.invoke("SendMessage", "");
} catch (err) {
    console.error("Hata:", err.message);   // "Mesaj boş olamaz"
}
```

**Normal exception'lar:** Eğer `HubException` değil de başka exception fırlatırsan, client'a sadece generic "An error occurred" mesajı gider — production'da güvenli (internal detay sızmaz). Development'ta detayları görmek için `EnableDetailedErrors`:

```csharp
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
```

---

## Hub'ın Sınırları ve Best Practice'ler

### 1. Hub'ı state'siz tut

Hub örnekleri **her çağrıda yeni oluşturulur**. Yani:

```csharp
public class ChatHub : Hub
{
    private int _counter = 0;   // ❌ bu işe yaramaz

    public async Task Increment()
    {
        _counter++;   // her çağrıda yeni hub instance, _counter hep 0
    }
}
```

Persistent state için DI servisi kullan, hub instance'ına güvenme.

### 2. Hub'da uzun süren işlem yapma

Hub method'u bir mesaj çağrısı işliyor. Uzun sürerse bağlantıyı bloklar.

```csharp
// ❌ Yanlış — hub'da 30 saniye işlem:
public async Task ProcessReport()
{
    await GenerateBigReportAsync();   // 30 sn sürüyor
    await Clients.Caller.SendAsync("Done");
}

// ✓ Doğru — background'da işle, bittiğinde mesaj gönder:
public async Task StartReport()
{
    var connectionId = Context.ConnectionId;
    _ = Task.Run(async () =>
    {
        await GenerateBigReportAsync();
        await _hubContext.Clients.Client(connectionId).SendAsync("Done");
    });
    await Clients.Caller.SendAsync("Started");
}
```

### 3. IHubContext — Hub Dışından Mesaj Gönderme

Bir controller'dan veya background service'ten SignalR mesajı göndermek için `IHubContext`:

```csharp
public class OrderController : ControllerBase
{
    private readonly IHubContext<NotificationHub> _hub;

    public OrderController(IHubContext<NotificationHub> hub) => _hub = hub;

    [HttpPost]
    public async Task<IActionResult> Create(OrderDto dto)
    {
        var order = await _service.CreateAsync(dto);

        // Hub dışından mesaj gönder:
        await _hub.Clients.User(dto.UserId).SendAsync("OrderCreated", order.Id);
        // ne yapar → controller'dan SignalR clientlarına push
        // background job, event handler, controller — hepsi bu pattern'i kullanır

        return Ok(order);
    }
}
```

**Önemli:** Strongly-typed kullanıyorsan `IHubContext<NotificationHub, INotificationClient>`.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de hub yok, real-time yok. 500 kullanıcıda sipariş durumu için sayfayı manuel yenilemek kabul edilebilir.

50K kullanıcıda SignalR ile:
- Sipariş statüsü canlı güncelleniyor
- Admin paneli canlı metrik gösteriyor
- Yorumlar anında görünüyor
- Tüm bunlar tek bir Hub mantığıyla

---

## 500 vs 50K Kullanıcı

| Konu | 500 kullanıcı/ay | 50K kullanıcı/ay |
|------|-------------------|-------------------|
| Hub temel kullanım | İhtimal düşük | Standart pattern |
| Groups | Chat varsa | Tenant izolasyonu, odalar için zorunlu |
| Strongly-typed Hub | Opsiyonel | Standart — typo'yu önle |
| IHubContext (dış) | Nadir | Sık (event-driven mimari) |
| Static state (yanlış) | Risk olabilir | Kesinlikle yapma — DI'ya geç |

---

## Kontrol Soruları

1. Hub ile REST controller arasındaki temel fark nedir? İki yönlü trafiği nasıl yönetiyor?
2. `Clients.All`, `Clients.Caller`, `Clients.Others`, `Clients.User`, `Clients.Group` arasındaki farkları açıkla.
3. Bir kullanıcının birden fazla connection'ı olabilir mi? `Clients.User` bunu nasıl handle eder?
4. Group ile User arasındaki temel fark nedir? Hangisi ne zaman kullanılır?
5. Strongly-typed Hub'ın avantajı nedir? String-based ile karşılaştır.
6. `Context.Items` ne işe yarar? Hub instance'ında state tutmak neden tehlikeli?
7. `HubException` ile normal exception arasındaki fark nedir? Production'da neden önemli?
8. `IHubContext` ne zaman kullanılır? Hub içinden gönderme ile farkı nedir?
9. Hub method'unda uzun süren işlem neden problemli? Çözüm yolu nedir?
