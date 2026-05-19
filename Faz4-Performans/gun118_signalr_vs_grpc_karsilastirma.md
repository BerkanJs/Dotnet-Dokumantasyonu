# Gün 118 — SignalR vs gRPC Bidirectional Streaming: Ne Zaman Hangisi?

## Bugün Ne Öğreneceğiz?

Bu son derste, aynı hedefe (gerçek zamanlı, çift yönlü iletişim) ulaşan iki farklı teknolojinin karşılaştırmasını yapacağız: **SignalR** ve **gRPC bidirectional streaming**. Her ikisi de WebSocket veya HTTP/2 kullanıyor, her ikisi de sunucu ↔ istemci arasında anlık mesaj gönderebiliyor — ama mimari açıdan köklü farkları var.

Bu ders aynı zamanda gün 115'ten beri işlediğimiz SignalR bloğunun kapanış dersi. Hem teknolojiyi anlamış, hem de hangisini ne zaman kullanacağını bilebiliyor olmalısın.

---

## Sorun: "Gerçek Zamanlı" Demek Ne Demek?

Bir sistemi "gerçek zamanlı" yapmanın iki farklı anlamı olabilir:

**1. Olay odaklı gerçek zamanlı (event-driven realtime)**  
"Kullanıcı A bir mesaj attı, hemen Kullanıcı B'ye iletilsin."  
"Sunucuda bir şey değişti, bağlı tüm tarayıcıları güncelle."  

Burada birden fazla istemci var, her istemcinin kendi bağlantısı var, sunucu doğru istemciye doğru mesajı göndermeli. Bu bir **yayın (broadcast)** problemi.

**2. Akış odaklı gerçek zamanlı (streaming realtime)**  
"Büyük bir dosya analiz ediliyor, sonuçlar parça parça gelsin."  
"Sensör verisi sürekli akıyor, her paketi işle."  
"İstemci komut gönderiyor, sunucu anlık yanıt üretiyor."  

Burada tek bir istemci var ve tek bir stream üzerinden karşılıklı veri akıyor. Bu bir **akış (stream)** problemi.

SignalR birinci problemi çözmek için tasarlandı. gRPC ikinci problemi çözmek için tasarlandı. İkisi de ikinci görevi yapabilir ama optimize oldukları yer farklı.

---

## SignalR'ın Gerçek Zamanlı Modeli

SignalR, **bağlantı yönetimi merkezli** düşünür. Her istemci hub'a bağlanır, bağlantısının bir ID'si olur. Sunucu bu bağlantı ID'leri arasından seçim yaparak mesaj gönderir.

```
Kullanıcı A ──── WebSocket bağlantısı ────► Hub
Kullanıcı B ──── WebSocket bağlantısı ────► Hub
Kullanıcı C ──── WebSocket bağlantısı ────► Hub

Hub ──► Clients.All("mesaj")      → A, B, C hepsine
Hub ──► Clients.Group("oda1")     → sadece oda1'dekiler
Hub ──► Clients.User("kullanici") → sadece o kullanıcı
```

Önemli özellikler:
- **Transport fallback**: WebSocket yoksa SSE, yoksa Long Polling
- **Connection management**: bağlantı kesilirse otomatik yeniden bağlan
- **JavaScript istemci**: tarayıcıdan doğrudan kullanılabilir (`@microsoft/signalr` npm)
- **Strongly-typed hub**: istemci metodlarını tip güvenli çağır
- **Groups**: mantıksal kanallar, dinamik üyelik

---

## gRPC Bidirectional Streaming'in Modeli

gRPC, **request/response sözleşmesi merkezli** düşünür. Her çağrı bir `.proto` sözleşmesiyle tanımlanır, her mesaj tipi önceden bildirilir.

Bidirectional streaming modunda istemci ve sunucu birbirinden bağımsız olarak mesaj gönderebilir, aynı HTTP/2 stream üzerinde:

```
İstemci ──── HTTP/2 stream açar ────► Sunucu
        ──── mesaj1 ────────────────►
             ◄──── yanit1 ───────────
        ──── mesaj2 ────────────────►
        ──── mesaj3 ────────────────►
             ◄──── yanit2 ───────────
             ◄──── yanit3 ───────────
        ──── stream kapat ──────────►
```

Önemli özellikler:
- **Protocol Buffers**: binary, tip güvenli, sıkıştırılmış mesajlar
- **HTTP/2**: tek TCP bağlantısı üzerinde multiplexing
- **Sözleşme öncelikli (contract-first)**: `.proto` dosyası gerçek kaynak
- **Deadline/Cancellation**: her çağrının zaman sınırı olabilir
- **Kod üretimi**: hem istemci hem sunucu kodu `.proto`'dan üretilir
- **Browser desteği sınırlı**: tarayıcıda doğrudan HTTP/2 gRPC çalışmaz, gRPC-Web gerekir

---

## Mimari Fark: Yayın vs Akış

Bunu bir radyo ile telefon farkı gibi düşün:

**SignalR = Radyo istasyonu**  
Bir sunucu, N tane dinleyiciye yayın yapar. Her dinleyici kendi kanalını tunar (bağlantı), istasyon dilediği dinleyiciye mesaj gönderir. Dinleyici sayısı değişir, yeni biri gelir, biri gider — istasyon bunları takip eder.

**gRPC Bidirectional Streaming = Telefon görüşmesi**  
İki taraf arasında birebir kanal açılır. Her iki taraf da konuşabilir, ama bu kanal sadece bu ikisi için. Üçüncü kişi bu konuşmaya katılamaz. Konuşma bitince kanal kapanır.

---

## Teknik Karşılaştırma Tablosu

| Kriter | SignalR | gRPC Bidirectional |
|--------|---------|-------------------|
| **Protokol** | WebSocket (fallback: SSE/LP) | HTTP/2 |
| **Mesaj formatı** | JSON (varsayılan) | Protocol Buffers (binary) |
| **Browser desteği** | Tam (WebSocket her tarayıcıda var) | Sınırlı (gRPC-Web gerekir) |
| **Mesaj boyutu verimi** | Orta (JSON = verbose) | Yüksek (binary = küçük) |
| **Tip güvenliği** | Strongly-typed hub (isteğe bağlı) | Proto şema zorunlu, tam tipli |
| **Broadcast / Group** | Birinci sınıf özellik | Yok (elle implemente et) |
| **Reconnect** | Otomatik, state restore | Elle implemente et |
| **Transport fallback** | Var (WebSocket → SSE → LP) | Yok (HTTP/2 gerekli) |
| **Sözleşme** | Kod öncelikli | Şema öncelikli (.proto) |
| **Server-to-server** | Çalışır ama fazla | İdeal kullanım |
| **Fan-out (1→N)** | Kolay, built-in | Manuel |
| **Throughput** | Orta | Yüksek |
| **Latency** | Düşük | Çok düşük |
| **Observability** | SignalR metrics | gRPC status codes, interceptors |
| **Authentication** | Cookie/JWT/custom | JWT/mTLS/interceptor |

---

## Ne Zaman SignalR, Ne Zaman gRPC?

### SignalR seç — eğer:

**1. Browser istemcin varsa**  
Web uygulamasında gerçek zamanlı özellik gerekiyorsa SignalR en kolay yol. Tarayıcıda `@microsoft/signalr` paketi ile kurmak birkaç satır:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .withAutomaticReconnect()
    .build();

connection.on("MesajAlindi", (kullanici, mesaj) => {
    // ne yapar: sunucudan yayınlanan mesajı alır
    // bunu yazmasaydık: sunucu mesaj gönderdiğinde tarayıcı tepki veremezdi
    console.log(`${kullanici}: ${mesaj}`);
});

await connection.start();
```

gRPC bidirectional'ı tarayıcıdan kullanmak için gRPC-Web proxy kurman, `.proto`'dan JavaScript kodu üretmen gerekir — önemli ek karmaşıklık.

**2. Broadcast / Group mesajlaşma gerekiyorsa**  
Birden fazla kullanıcıya aynı anda mesaj göndermek SignalR'ın en güçlü olduğu alan:

```csharp
// ne yapar: "kripto" grubuna dahil tüm bağlantılara fiyat güncellemesi gönderir
// bunu yazmasaydık: her kullanıcıyı ayrı ayrı takip etmek zorunda kalırdık
await Clients.Group("kripto").SendAsync("FiyatGuncellendi", btcFiyat);
```

gRPC'de bunu yapmak için kendi fan-out mantığını yazman gerekir: tüm açık stream'leri takip eden bir registry, her birine elle yazma.

**3. Bağlantı yaşam döngüsü yönetimi önemliyse**  
`OnConnectedAsync`, `OnDisconnectedAsync`, otomatik reconnect, stateful reconnect — bunlar SignalR'da built-in:

```csharp
public override async Task OnDisconnectedAsync(Exception? exception)
{
    // ne yapar: kullanıcı bağlantısı koptuğunda grubundan çıkarır
    // bunu yazmasaydık: kullanıcı kopuk olsa bile mesaj almaya devam ederdi
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, "aktif");
    await base.OnDisconnectedAsync(exception);
}
```

---

### gRPC bidirectional seç — eğer:

**1. Server-to-server iletişimse**  
Microservice'ler arası gerçek zamanlı veri akışı için gRPC ideal. Her iki servis de `.proto` sözleşmesini bilir, mesajlar binary gider:

```protobuf
service SensorService {
  rpc OlcumAkisi (stream SensorOkuma) returns (stream Analiz);
  // ne yapar: istemci sensor okumalarını akıtır, sunucu analiz sonuçlarını akıtır
  // bunu yazmasaydık: her okuma için ayrı HTTP isteği yapılırdı — son derece verimsiz
}
```

**2. Yüksek throughput ve düşük latency gerekiyorsa**  
Protocol Buffers, JSON'a göre 3-10x daha küçük mesajlar üretir. Saniyede binlerce mesaj geçen sistemlerde bu fark kritik:

- JSON: `{"sensorId":42,"sicaklik":23.5,"zaman":"2025-01-15T10:30:00Z"}` = ~60 byte
- Protobuf: aynı veri = ~12 byte (5x daha küçük)

**3. Tip güvenliği ve sözleşme zorunluysa**  
`.proto` dosyası hem istemci hem sunucu için kaynak. Sözleşme değiştiğinde her iki taraf yeniden derlenir, eksik alan veya tip uyuşmazlığı derleme hatası verir — runtime'da değil.

**4. Stream'de backpressure kontrolü gerekiyorsa**  
gRPC, HTTP/2'nin flow control mekanizmasını kullanır. İstemci yavaşsa sunucu otomatik yavaşlar:

```csharp
await foreach (var okuma in requestStream.ReadAllAsync(cancellationToken))
{
    // ne yapar: istemcinin gönderdiği her sensor okumasını sırayla işler
    // bunu yazmasaydık: istemci sunucudan hızlı gönderirse buffer taşar
    var analiz = await analizServisi.IsleAsync(okuma);
    await responseStream.WriteAsync(analiz);
}
```

---

## Hibrit Senaryo: İkisini Birlikte Kullanmak

Büyük sistemlerde bu iki teknoloji birbirini tamamlar — rekabet etmez:

```
[Tarayıcı] ──── SignalR WebSocket ────► [API Gateway / BFF]
                                              │
                                              │ gRPC bidirectional
                                              ▼
                                       [Analiz Servisi]
                                              │
                                              │ gRPC bidirectional
                                              ▼
                                       [Veri Toplama Servisi]
```

**Örnek akış:**
1. Tarayıcı, SignalR ile BFF'ye bağlanır (kolay, WebSocket)
2. BFF, gRPC ile arka servislerle konuşur (hızlı, binary, tipli)
3. Arka servis bir olay ürettiğinde BFF üzerinden SignalR ile tarayıcıya iletilir

Bu mimaride her teknoloji kendi güçlü olduğu yerde kullanılıyor.

---

## Kod Karşılaştırması: Aynı Senaryo, İki Teknoloji

**Senaryo:** Borsa fiyatları gerçek zamanlı güncellensin, kullanıcılar istedikleri hisseleri takip etsin.

### SignalR Versiyonu

```csharp
// Hub: bağlantı ve grup yönetimi
public class BorsaHub : Hub
{
    public async Task HisseTakipEt(string sembol)
    {
        // ne yapar: kullanıcıyı o hissenin grubuna ekler
        // bunu yazmasaydık: tüm kullanıcılara tüm fiyatlar gönderilirdi — verimsiz
        await Groups.AddToGroupAsync(Context.ConnectionId, $"hisse_{sembol}");
    }

    public async Task TakibiGuncel(string sembol)
    {
        // ne yapar: kullanıcıyı gruptan çıkarır
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"hisse_{sembol}");
    }
}

// Fiyat güncelleyici background service
public class FiyatYayinServisi : BackgroundService
{
    private readonly IHubContext<BorsaHub> _hubContext;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var fiyatlar = await borsaApi.SonFiyatlariGetirAsync();
            
            foreach (var (sembol, fiyat) in fiyatlar)
            {
                // ne yapar: sadece o hisseyi takip eden kullanıcılara gönderir
                // bunu yazmasaydık: tüm bağlı kullanıcılara tüm fiyatlar gönderilirdi
                await _hubContext.Clients
                    .Group($"hisse_{sembol}")
                    .SendAsync("FiyatGuncellendi", sembol, fiyat, stoppingToken);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

### gRPC Bidirectional Streaming Versiyonu

```protobuf
// fiyat.proto
service BorsaServisi {
  rpc FiyatAkisi (stream HisseTalebi) returns (stream FiyatGuncellemesi);
}

message HisseTalebi {
  string sembol = 1;
  bool takip_et = 2;  // true: ekle, false: çıkar
}

message FiyatGuncellemesi {
  string sembol = 1;
  double fiyat = 2;
  int64 zaman_ms = 3;
}
```

```csharp
// gRPC servis implementasyonu
public class BorsaGrpcServisi : BorsaServisi.BorsaServisiBase
{
    public override async Task FiyatAkisi(
        IAsyncStreamReader<HisseTalebi> requestStream,
        IServerStreamWriter<FiyatGuncellemesi> responseStream,
        ServerCallContext context)
    {
        var takipEdilenler = new HashSet<string>();
        
        // İstemciden gelen talepleri okuma görevi
        // ne yapar: istemci yeni sembol eklediğinde/çıkardığında günceller
        // bunu yazmasaydık: sembol listesi statik kalırdı, dinamik değiştiremezdik
        var okumaDurumu = Task.Run(async () =>
        {
            await foreach (var talep in requestStream.ReadAllAsync(context.CancellationToken))
            {
                if (talep.TakipEt) takipEdilenler.Add(talep.Sembol);
                else takipEdilenler.Remove(talep.Sembol);
            }
        });

        // Fiyatları akıtma görevi
        while (!context.CancellationToken.IsCancellationRequested)
        {
            foreach (var sembol in takipEdilenler.ToList())
            {
                var fiyat = await borsaApi.FiyatGetirAsync(sembol);
                
                // ne yapar: takip edilen her sembol için fiyat günceller
                // bunu yazmasaydık: istemci subscription kavramı olmadan tüm fiyatları çekemezdi
                await responseStream.WriteAsync(new FiyatGuncellemesi
                {
                    Sembol = sembol,
                    Fiyat = fiyat,
                    ZamanMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }
            
            await Task.Delay(1000, context.CancellationToken);
        }

        await okumaDurumu;
    }
}
```

**Ne fark ettik?**

SignalR versiyonunda: Groups, Hub, IHubContext — broadcast mekanizması zaten var. Birden fazla kullanıcıyı takip etmek için sadece grup adı yeterli.

gRPC versiyonunda: her şeyi elle yazmak gerekti. `HashSet<string>` takip, `Task.Run` ile paralel okuma/yazma, `ToList()` ile concurrent modification önleme. Bu daha fazla kod, daha fazla risk — ama daha fazla kontrol ve daha iyi performans.

---

## Faz2 ile Karşılaştırma

Faz2'de (gün 23) Minimal API ile şunu yaptın:

```csharp
app.MapGet("/fiyat/{sembol}", async (string sembol, BorsaService servis) =>
{
    var fiyat = await servis.FiyatGetirAsync(sembol);
    return Results.Ok(fiyat);
});
```

Bu yaklaşım "polling" gerektirir — tarayıcı her 5 saniyede bir istek atar. 1000 kullanıcı = saniyede 200 istek. Sunucu sürekli meşgul, fiyat değişmediğinde bile.

SignalR/gRPC ile sunucu fiyat değiştiğinde itmesi (push) yapar. Fiyat değişmediğinde hiçbir şey olmaz. 1000 kullanıcı = saniyede 0 istek (değişiklik olmadığında).

---

## 500 vs 50.000 Kullanıcı

| Senaryo | 500 Kullanıcı | 50.000 Kullanıcı |
|---------|---------------|------------------|
| **REST polling (5s)** | 100 req/s — idare eder | 10.000 req/s — sunucu çöker |
| **SignalR WebSocket** | 500 açık bağlantı — hafif | 50.000 bağlantı — Redis backplane şart |
| **gRPC bidirectional** | 500 HTTP/2 stream — hafif | 50.000 stream — multiplexing sayesinde verimli |
| **gRPC + SignalR hibrit** | BFF 500 WebSocket, N gRPC stream | BFF birden fazla pod, Redis backplane |
| **Bellek kullanımı** | SignalR: ~50 MB | SignalR: ~5 GB (backplane olmadan dağıtamaz) |
| **Ölçekleme stratejisi** | Tek pod yeterli | SignalR: sticky session + Redis / gRPC: load balancer |

50.000 kullanıcıda kritik fark: gRPC her bağlantıyı HTTP/2 multiplexing ile yönetir, TCP bağlantısı sayısı daha az. SignalR her kullanıcı için ayrı WebSocket bağlantısı tutar, bellek ve file descriptor tüketimi daha yüksek. Ama SignalR'ın group broadcasting avantajı gRPC'de yoktur.

---

## Karar Akışı

Gerçek zamanlı ihtiyacın var:

```
Tarayıcı istemcin var mı?
├── Evet → SignalR
│          (WebSocket desteği, otomatik fallback, kolay JS entegrasyonu)
└── Hayır (server-to-server) →
           Birden fazla istemciye broadcast gerekiyor mu?
           ├── Evet → SignalR (IHubContext ile background push)
           └── Hayır (1-1 stream) →
                      Yüksek throughput / binary verim gerekiyor mu?
                      ├── Evet → gRPC bidirectional
                      └── Hayır → SignalR veya ikisi de olur
```

---

## SignalR Bloğu Kapanış: Öğrendiklerimiz (Gün 115–118)

**Gün 115** — Gerçek zamanlı iletişim temelleri: Long Polling'in neden verimsiz olduğu, SSE'nin tek yönlülüğü, WebSocket'in neden kazandığı. SignalR'ın bunları nasıl soyutladığı.

**Gün 116** — Hub pattern: `Clients.All`, `Clients.Group`, `Clients.User`. Connection lifecycle. Strongly-typed hub. IHubContext ile background servislerden push.

**Gün 117** — Production: Redis backplane ile yatay ölçekleme. Cookie ve JWT ile kimlik doğrulama. IUserIdProvider ile özelleştirme. Auto reconnect ve stateful reconnect.

**Gün 118** — SignalR vs gRPC: yayın (broadcast) problemi vs akış (stream) problemi. Ne zaman hangisi, hibrit mimariler.

---

## Kontrol Soruları

1. SignalR'da `Clients.Group("oda1").SendAsync(...)` çağrısı ne yapar? gRPC bidirectional streaming'de aynı davranışı nasıl implemente edersin?

2. Bir borsa uygulamasında 50.000 kullanıcı gerçek zamanlı fiyat alıyor. Tek pod ile yönetemezsin, 3 pod açıyorsun. SignalR için ne yapman gerekir? gRPC için ne yapman gerekir? Neden farklı?

3. "Tarayıcıdan gRPC kullanmak istiyorum" dersen ne gerekir? Bu ekstra karmaşıklık ne zaman buna değer?

4. Bir sistem tasarlıyorsun: microservice'ler arası sensör verisi akışı (1→1, binary, yüksek throughput) + tarayıcıya anlık dashboard güncellemesi (1→N, JSON). Bu sistemi SignalR mı, gRPC mı, yoksa her ikisi mi ile kurarsın? Tasarımını çiz.

5. gRPC bidirectional streaming'de istemci çok hızlı veri gönderirse ne olur? SignalR'da aynı durumda ne olur? İkisi nasıl farklı davranır?
