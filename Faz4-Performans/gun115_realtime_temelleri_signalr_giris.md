# Gün 115 — Real-Time İletişim Temelleri ve SignalR'a Giriş

---

## Real-Time İletişim Nedir? Neden Var?

Klasik web istek-yanıt modelinde işliyor: Tarayıcı bir istek atar, server cevap verir, bağlantı kapanır. Bir sonraki bilgi için tarayıcı tekrar istek atması gerekir.

Bu model **sunucudan tarayıcıya proaktif bildirim** için yetersiz:
- Yeni mesaj geldi → kullanıcıya anında göster
- Sipariş durumu değişti → "hazırlanıyor" → "yola çıktı" → "teslim edildi" canlı güncelleme
- Borsa fiyatı değişti → grafik anlık değişmeli
- Aynı dokümanda birden fazla kullanıcı → herkesin değişikliği anında diğerlerinde görünmeli

Tarayıcının "yeni mesaj var mı?" diye saniyede 1 kez sorması da çözüm değil — gereksiz trafik, gecikme, sunucu yükü.

**Real-time iletişim**: bağlantının açık kalması, sunucunun istediği anda tarayıcıya mesaj göndermesi.

Bunu sağlayan birkaç teknoloji var. Her birinin avantaj/dezavantajı farklı.

---

## Üç Temel Yaklaşım

### 1. Long Polling — En Eski Hile

Klasik HTTP'nin sınırlarını esnetmek için bulunmuş trick. Tarayıcı istek atıyor, sunucu **hemen cevap vermiyor**. Yeni veri olana kadar bekletiyor. Veri olduğunda cevap dönüyor, tarayıcı yeni isteğe başlıyor.

```
Tarayıcı ──── GET /messages ──────▶ Sunucu (yeni mesaj yok, beklet)
                                       │
                                       │ (30 saniye geçti, hâlâ yok)
                                       ▼
Tarayıcı ◀──── 204 No Content ──── Sunucu
Tarayıcı ──── GET /messages ──────▶ Sunucu (tekrar bekle)
                                       │
                                       │ (10 saniye sonra mesaj geldi)
                                       ▼
Tarayıcı ◀──── 200 OK + {mesaj} ──── Sunucu
Tarayıcı ──── GET /messages ──────▶ Sunucu (yeni mesaj)
```

**Avantaj:** Standart HTTP. Her yerde çalışır. Eski sistemlerde, eski tarayıcılarda problem yok.

**Dezavantaj:**
- Her mesaj için bir HTTP isteği — overhead büyük
- Server side connection yönetimi (timeout'lar, açık bağlantı sayısı)
- Tek yönlü — sadece server→client, ters yön için ayrı istek

**Ne zaman:** Sadece eski tarayıcı desteği gerekli olduğunda. 2010'lardan bugüne pek tercih edilmiyor.

### 2. Server-Sent Events (SSE) — Tek Yönlü Stream

HTML5 standardı. Tarayıcı `EventSource` API ile bağlantı açar — sunucu bu bağlantıdan **akış halinde mesaj gönderir**.

```
Tarayıcı ──── GET /events ──────▶ Sunucu (bağlantı açık kalır)
Tarayıcı ◀──── data: mesaj1 ──── Sunucu
Tarayıcı ◀──── data: mesaj2 ──── Sunucu
Tarayıcı ◀──── data: mesaj3 ──── Sunucu
... (sürekli akış)
```

Sunucu istediği zaman yeni mesaj akıtıyor. Tarayıcı gelen mesajları event olarak alıyor.

**Avantaj:**
- Standart HTTP (HTTP/1.1 üstünde çalışıyor)
- Otomatik reconnect (bağlantı kopunca tarayıcı tekrar açıyor)
- Basit protokol, debug kolay
- Tarayıcı yerel desteği var

**Dezavantaj:**
- Tek yönlü — sadece server→client. Tarayıcıdan server'a mesaj için ayrı HTTP isteği lazım
- Text-only — binary veri için base64'e dönüştürmek lazım
- Eski Internet Explorer desteklemiyor (önemsiz artık)
- Bir bağlantı = bir HTTP connection (HTTP/2 ile bu sorun azalır)

**Ne zaman:** Tek yönlü bildirim. Live ticker, notification feed, dashboard güncelleme. Twitter feed güncelleme gibi.

### 3. WebSocket — Tam İki Yönlü Bağlantı

Modern real-time'ın standartı. HTTP üzerinden başlatılıyor ama sonra **kalıcı, iki yönlü** bir bağlantı oluyor.

```
Tarayıcı ──── HTTP Upgrade Request ──▶ Sunucu
Tarayıcı ◀──── HTTP 101 Switching Protocols ──── Sunucu
                    │
        Bağlantı WebSocket'e dönüştü
                    │
Tarayıcı ←──── data ────→ Sunucu (iki yönlü, sürekli)
Tarayıcı ←──── data ────→ Sunucu
...
```

İlk istek HTTP — `Upgrade: websocket` header'ı ile. Sunucu kabul ederse protokol değişiyor. Artık ne HTTP istek-yanıt var, ne de HTTP semantiği. Pure iki yönlü mesajlaşma.

**Avantaj:**
- İki yönlü — hem server hem client istediği zaman gönderir
- Düşük overhead — header her mesajda yok, frame'ler küçük
- Binary destek doğal
- Modern tarayıcılarda tam destek

**Dezavantaj:**
- HTTP semantiği yok — REST tooling (Postman, curl) doğrudan kullanamazsın
- Bazı proxy'ler/load balancer'lar WebSocket bilmiyor
- Authentication HTTP'den farklı yönetiliyor (token query string'de veya ilk handshake header'larında)
- Connection limiti — her sunucu binlerce kalıcı bağlantı tutuyor

**Ne zaman:** Çift yönlü iletişim gerekli her senaryoda. Chat, gerçek zamanlı oyun, collaborative editing, canlı dashboard.

### Karşılaştırma Tablosu

| | Long Polling | SSE | WebSocket |
|---|---|---|---|
| Yön | İstek-yanıt | Server → Client | İki yönlü |
| Protokol | HTTP | HTTP | WS (HTTP'den upgrade) |
| Overhead | Yüksek | Orta | Düşük |
| Binary | Zor | Zor (base64) | Doğal |
| Auto reconnect | Manuel | Var (built-in) | Manuel |
| Tarayıcı desteği | Her yer | Modern tarayıcılar | Modern tarayıcılar |
| Proxy/firewall sorunu | Yok | Az | Bazen var |
| Use case | Eski sistem fallback | Bildirim akışı | Tam interaktif |

---

## SignalR Nedir?

Burada bir gerçek var: yukarıdaki teknolojilerin hepsinin kendi API'sı, kendi yapılandırması, kendi yönetimi var. Üstelik tarayıcıdan tarayıcıya destek farklı. WebSocket bazı proxy'lerden geçemiyor, fallback gerekiyor.

**SignalR**, ASP.NET Core'un real-time framework'ü. Bu karmaşıklığı senin yerine yönetiyor.

### SignalR'ın Yaptığı Sihir

Sen kod yazarken sadece "şu mesajı şu kullanıcıya gönder" diyorsun. Arka planda SignalR:

1. **Transport seçiyor:** WebSocket destekleniyorsa onu kullan. Yoksa SSE. O da yoksa long polling. Otomatik fallback.
2. **Bağlantıyı yönetiyor:** Hangi kullanıcı nerede, hangi grup'ta, hangi server instance'ında.
3. **Reconnection:** Bağlantı koparsa otomatik yeniden bağlanır, state'i korur.
4. **Method invocation:** Sen sadece `Clients.User("berkan").SendAsync("YeniMesaj", data)` diyorsun, framework gerisini halleder.

**Analoji:** Telefon görüşmesi yapmak istiyorsun. Sen sadece "Berkan'ı ara" diyorsun. Telefon şirketi GSM mi VoIP mi kullanacak, hangi baz istasyonundan geçecek, switch nasıl olacak — düşünmüyorsun. SignalR de aynı: sen "şuna mesaj gönder" diyorsun, gerisini framework yönetiyor.

### SignalR Olmadan Hayat

WebSocket'i raw kullansaydın:

```csharp
// Pure WebSocket — düşük seviye:
app.UseWebSockets();
app.Map("/ws", async ctx =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = 400;
        return;
    }

    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    var buffer = new byte[1024 * 4];

    while (ws.State == WebSocketState.Open)
    {
        var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close) break;

        var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
        // mesajı parse et, gerekirse cevap gönder...
        await ws.SendAsync(...);
    }
});
```

Şimdi şunları kendin yapmak zorundasın:
- Bağlantıları (kim açık?) bir koleksiyonda tutmak
- Mesaj formatını tanımlamak (JSON? Binary? Method name?)
- Gruplar (kullanıcılar room'lara nasıl ayrılır?)
- Reconnection (kopan bağlantı dönüşünde state nasıl restore edilir?)
- Multi-server senaryosu (3 sunucu varsa user A'nın bağlandığı server B'ye nasıl mesaj gönderir?)
- Authentication (WS handshake'te auth nasıl?)
- Fallback (eski tarayıcılarda)

Bunların hepsini sıfırdan yazmak haftalar alır.

SignalR bunların hepsini hazır veriyor. Sen sadece iş mantığına odaklanıyorsun.

---

## SignalR'ın Çekirdek Kavramları (Kısa Tanıtım)

Detaylarına yarın gireceğiz, bugün tanıtım:

### Hub

SignalR'ın "controller" eşdeğeri. Server'da bir class — clientlardan gelen çağrıları handle eder, clientlara mesaj gönderir.

```csharp
public class ChatHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        // client'tan gelen çağrı
        await Clients.All.SendAsync("ReceiveMessage", user, message);
        // tüm bağlı clientlara "ReceiveMessage" event'i gönder
    }
}
```

### Connection

Her client SignalR'a bağlandığında bir **connection** açıyor. Her connection'ın benzersiz bir ID'si var (`ConnectionId`). Bu ID ile client'a özel mesaj gönderebilirsin.

### Group

Connection'ları gruplara koyabilirsin. "chat-room-42" grubu, "kullanıcı-123-bildirimleri" grubu. Tüm gruba mesaj gönderirsin → SignalR o gruptaki tüm connection'lara iletir.

### Client Method Invocation

Server şunu der: "Bağlı clientlardaki `ReceiveMessage` adlı JS fonksiyonunu çağır, şu parametrelerle." Client'taki JS bu fonksiyonu kaydetmişse, server'dan çağrı gelince çalıştırır.

```javascript
// Client (JavaScript):
connection.on("ReceiveMessage", (user, message) => {
    console.log(`${user}: ${message}`);
});
```

Bu mekanizma SignalR'ı "RPC over WebSocket" yapıyor. gRPC'ye benziyor ama daha yüksek seviye.

---

## SignalR vs Diğer Teknolojiler

### SignalR vs Raw WebSocket

WebSocket düşük seviye. SignalR onun üzerine inşa edilmiş framework. WebSocket'in sağladığı her şey SignalR'da var, üstüne:
- Transport fallback
- Hub abstraction (yüksek seviye RPC)
- Group management
- Reconnection
- Scale-out (Redis backplane)

WebSocket'i tek başına kullanmak özel durumlar (custom protokol, performans-kritik niche senaryolar) için. Çoğu uygulamada SignalR daha verimli.

### SignalR vs SSE

SSE tek yönlü. SignalR iki yönlü. Eğer client'tan server'a hiç mesaj göndermiyorsan (sadece notification feed), SSE daha basit. İki yönlü iletişim varsa SignalR.

Ama SignalR zaten SSE'yi transport olarak kullanabiliyor (fallback). Bu yüzden "ya SSE ya SignalR" değil — SignalR seçince SSE'nin avantajlarını da alıyorsun.

### SignalR vs gRPC Bidirectional Streaming

İkisi de iki yönlü streaming sunuyor. Detaylı karşılaştırmayı 4. günde (gün 118) yapacağız. Kısaca:
- gRPC: server-to-server için ideal, browser desteği yarım
- SignalR: browser-first, web uygulamalarında doğal

---

## SignalR'ın Sınırları

Her teknoloji bir trade-off. SignalR'ın da kısıtları var.

**Connection sayısı sınırlı:** Her sunucu binlerce concurrent WebSocket tutar. 100K+ bağlantı için scale-out gerekiyor (Redis backplane, Azure SignalR Service).

**Sticky sessions gerekli:** Long polling fallback'inde aynı client aynı server'a gelmeli. Load balancer "sticky session" desteklemeli.

**Stateless değil:** Bağlantı state'i server tarafında. Sunucu çökerse bağlantılar düşer. Kullanıcılar reconnect oluyor ama UX etkilenir.

**Mobile gücünde:** Mobil uygulamalarda native WebSocket SDK'ları var ama SignalR client kütüphaneleri ek bir bağımlılık.

**Browser tab limit:** Tarayıcılar aynı origin'e açık WebSocket sayısını kısıtlıyor (genelde 30-50). Çok sayıda farklı SignalR connection açmak sorun yaratabilir.

---

## SignalR Ne Zaman Uygun?

**İdeal senaryolar:**
- Chat / mesajlaşma uygulamaları
- Live notification feed (bildirimler, social feed)
- Real-time dashboard (canlı metrikler, monitoring)
- Collaborative editing (Google Docs benzeri)
- Multiplayer browser game (turn-based)
- Live auction (anlık fiyat güncelleme)
- Stock ticker (borsa, kripto fiyatları)
- IoT dashboard (sensör verilerini canlı gösterme)

**Uygun olmayan senaryolar:**
- Yüksek FPS gerçek zamanlı oyun (UDP, custom protokol gerekir)
- Video/ses streaming (WebRTC daha uygun)
- Aşırı düşük latency (microsaniye seviyesinde — özel protokol gerekir)
- Backend-to-backend (gRPC daha iyi)
- Tek yönlü basit bildirim (SSE daha sade)

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de real-time yok. Sayfa yenilemeden veri güncellenmiyor. 500 kullanıcıda kabul edilebilir UX.

50K kullanıcıda modern beklentiler:
- Sipariş durumu canlı güncellensin (kullanıcı sayfayı yenilemesin)
- Yeni bildirim anında görünsün
- Admin paneli canlı metrik göstersin
- Stok değişimi anında yansısın

Bunların hepsi SignalR ile çözülüyor.

---

## 500 vs 50K Kullanıcı

| Senaryo | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---------|-------------------|-------------------|
| Real-time bildirim ihtiyacı | UX iyileştirmesi, opsiyonel | Standart beklenti |
| Long polling | Yeterli | Yetmez — verimli değil |
| SSE | Tek yönlü ise | Bildirim akışı için OK |
| WebSocket / SignalR | Bazı use case'lerde | Çoğu interaktif senaryoda |
| Scale-out (Redis backplane) | Tek instance, gereksiz | 3+ instance varsa zorunlu |
| Azure SignalR Service | Overengineering | Yüksek ölçekte değerli |

---

## Kontrol Soruları

1. Long polling, SSE ve WebSocket arasındaki temel farklar nedir? Hangisi hangi yön desteği sağlıyor?
2. SSE neden tek yönlü? Server→client gönderebiliyor ama client→server için ne gerekiyor?
3. WebSocket nasıl başlıyor? HTTP'den protokol değişimi nasıl oluyor?
4. SignalR'ın "raw WebSocket"e göre temel avantajları nedir? Hangi karmaşıklıkları yönetiyor?
5. Transport fallback ne işe yarar? SignalR hangi sırayla deniyor?
6. Hub kavramı SignalR'da ne işe yarar? REST controller ile benzerliği ne?
7. Hangi senaryolarda SignalR overkill olur, basit SSE yeterli olur?
8. Sticky session ne demek, load balancer'da neden gerekli olabiliyor?
