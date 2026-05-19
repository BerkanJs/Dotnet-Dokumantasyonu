# Gün 113 — gRPC Service Types ve ASP.NET Core Implementasyonu

---

## Bu Ders Neden Var?

Gün 112'de gRPC'nin ne olduğunu ve neden var olduğunu gördük. Bugün ise nasıl kullanılacağına geçiyoruz.

gRPC'nin REST'ten en büyük farklarından biri: **dört farklı iletişim modeli** sunması. REST'te her şey "request-response" — bir istek, bir yanıt. gRPC streaming desteğiyle bunun ötesine geçiyor.

Dört modeli inceleyeceğiz, hangi senaryoya hangisinin uygun olduğunu anlayacağız, sonra ASP.NET Core'da implementasyonu göreceğiz.

---

## gRPC'nin 4 İletişim Modeli

Dört modeli şu eksenle düşün: **kim ne zaman mesaj gönderiyor?**

| Model | Client gönderir | Server gönderir |
|-------|----------------|------------------|
| **Unary** | 1 mesaj | 1 mesaj |
| **Server Streaming** | 1 mesaj | N mesaj (stream) |
| **Client Streaming** | N mesaj (stream) | 1 mesaj |
| **Bidirectional Streaming** | N mesaj (stream) | N mesaj (stream) |

### 1. Unary — Klasik Request/Response

En tanıdık model. REST'in karşılığı. Client bir istek gönderir, server bir yanıt döner.

```
Client ──── GetKitap(id=42) ────▶ Server
Client ◀──── Kitap{ad="Clean Code"} ──── Server
```

**.proto tanımı:**
```proto
service CatalogService {
    rpc GetKitap (GetKitapRequest) returns (Kitap);
}
```

**Ne zaman kullan:**
- Basit kayıt çekme, oluşturma, güncelleme
- Tek seferlik işlemler
- Çoğu CRUD operasyonu

Aslında çoğu gRPC çağrısı unary. Streaming özel senaryolar için.

---

### 2. Server Streaming — Server Birden Fazla Mesaj Gönderiyor

Client bir istek gönderir, server **birden fazla yanıt** döner. Bağlantı açık kalır, server mesajları akıttıkça client okur.

```
Client ──── ListKitaplar(kategori="programlama") ──▶ Server
Client ◀──── Kitap{Clean Code} ──── Server
Client ◀──── Kitap{Refactoring} ──── Server
Client ◀──── Kitap{DDD} ──── Server
Client ◀──── [stream end] ──── Server
```

**.proto tanımı:**
```proto
service CatalogService {
    rpc ListKitaplar (ListKitaplarRequest) returns (stream Kitap);
                                                  ^^^^^^
    //                                            stream keyword
}
```

**Ne zaman kullan:**
- Büyük liste, hepsini birden yüklemek istemiyorsun (memory)
- Sonuç akarken client işlemeye başlasın
- Real-time veri akışı: borsa fiyatları, sensör verileri, log akışı
- Server uzun süre işlem yapıyor, ara sonuçları göndermek istiyor

**REST karşılığı:** HTTP/2 chunked transfer veya Server-Sent Events (SSE). Ama gRPC daha temiz.

**Pratik örnek:** "Tüm siparişleri export et" — 100K kayıt var. Unary'de bunlar bellekte toplanır, tek mesajda gönderilir (kötü). Server streaming'de birer birer gönderilir, client her birini işleyip diske yazar.

---

### 3. Client Streaming — Client Birden Fazla Mesaj Gönderiyor

Client **birden fazla mesaj** gönderir, hepsi bitince server **bir yanıt** döner.

```
Client ──── LogEntry{...} ────▶ Server
Client ──── LogEntry{...} ────▶ Server
Client ──── LogEntry{...} ────▶ Server
Client ──── [stream end] ────▶ Server
Client ◀──── ProcessResult{toplam: 3} ──── Server
```

**.proto tanımı:**
```proto
service LogService {
    rpc UploadLogs (stream LogEntry) returns (UploadResult);
}
```

**Ne zaman kullan:**
- Toplu yükleme: client'ın elinde çok sayıda kayıt var, batch'leyerek değil parça parça göndermek
- Dosya yükleme: dosya chunk'larını gönder, server birleştirsin
- Sensör verisi: client sürekli ölçüm yapıyor, periyodik gönderiyor, server agrega ediyor

**Pratik örnek:** Mobil app çevrimdışı çalışırken biriken event'leri sunucuya yüklüyor. 500 event birden var. Unary ile 500 istek atmak yerine, client stream ile tek bağlantıdan akıt.

**Avantaj:** Bağlantı bir kez açılır (TLS handshake bir kez). 500 ayrı istektense çok daha verimli.

---

### 4. Bidirectional Streaming — İki Yönlü Stream

Hem client hem server bağımsız olarak mesaj akıtır. Birbirini beklemeden.

```
Client ──── Mesaj1 ────▶ Server
Client ◀──── YanıtA ──── Server
Client ──── Mesaj2 ────▶ Server
Client ──── Mesaj3 ────▶ Server
Client ◀──── YanıtB ──── Server
Client ◀──── YanıtC ──── Server
...
```

İletişim async — kim ne zaman ne göndereceği önceden belirli değil.

**.proto tanımı:**
```proto
service ChatService {
    rpc Chat (stream ChatMessage) returns (stream ChatMessage);
}
```

**Ne zaman kullan:**
- Chat uygulamaları
- Çok kullanıcılı oyun state senkronizasyonu
- Collaboration tool'lar (Google Docs benzeri canlı edit)
- Long-running diyalog: client istiyor, server hesaplıyor, "tamam mı?" diye soruyor, client "devam" diyor

**Karmaşıklık:** Bidirectional stream güçlü ama yönetimi zor. State management, error handling, bağlantı kopması durumları düşünmek lazım. Genelde SignalR gibi yüksek seviye abstraction'lar tercih ediliyor (Gün 115'te göreceğiz).

---

## ASP.NET Core'da gRPC — Kurulum

### Proje Oluşturma

```bash
dotnet new grpc -n KitapApp.CatalogService
```

Bu template hazır bir gRPC servisi üretir:
- `Protos/greet.proto` — örnek proto dosyası
- `Services/GreeterService.cs` — örnek servis implementasyonu
- `Program.cs` — gRPC middleware yapılandırılmış

### .csproj İçeriği

```xml
<ItemGroup>
    <PackageReference Include="Grpc.AspNetCore" Version="..." />
</ItemGroup>

<ItemGroup>
    <Protobuf Include="Protos\catalog.proto" GrpcServices="Server" />
</ItemGroup>
```

`<Protobuf>` tag'i kritik. `GrpcServices="Server"` derken: "Bu .proto dosyasından server kod üret." Client tarafı için `Client` veya hem hem için `Both`.

Build sırasında protoc çalışıyor, otomatik kod üretiyor.

### Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();
// ne yapar → gRPC servislerini DI'ya kaydeder, middleware'i hazırlar

var app = builder.Build();
app.MapGrpcService<CatalogServiceImpl>();
// ne yapar → CatalogServiceImpl class'ını gRPC endpoint olarak expose eder

app.Run();
```

---

## Servis Implementasyonu — 4 Modelin Hepsi

`.proto` dosyamızı genişletelim, 4 modelin de örneğini görelim:

```proto
syntax = "proto3";

service CatalogService {
    // Unary
    rpc GetKitap (GetKitapRequest) returns (Kitap);

    // Server streaming
    rpc ListKitaplar (ListKitaplarRequest) returns (stream Kitap);

    // Client streaming
    rpc UploadKitaplar (stream Kitap) returns (UploadResult);

    // Bidirectional
    rpc Chat (stream ChatMessage) returns (stream ChatMessage);
}

message GetKitapRequest { int32 id = 1; }
message Kitap { int32 id = 1; string ad = 2; }
message ListKitaplarRequest { int32 sayfa = 1; }
message UploadResult { int32 eklenen_sayisi = 1; }
message ChatMessage { string user = 1; string text = 2; }
```

### Unary Implementation

```csharp
public class CatalogServiceImpl : CatalogService.CatalogServiceBase
{
    private readonly IKitapRepo _repo;

    public CatalogServiceImpl(IKitapRepo repo) => _repo = repo;

    public override async Task<Kitap> GetKitap(
        GetKitapRequest request,
        ServerCallContext context)
    {
        var kitap = await _repo.GetAsync(request.Id);
        if (kitap is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Kitap {request.Id} bulunamadı"));

        return new Kitap
        {
            Id = kitap.Id,
            Ad = kitap.Ad
        };
        // ne yapar → DB'den çek, proto Kitap'a map et, dön
        // basit request-response
    }
}
```

`ServerCallContext` parametre — request metadata, cancellation token, deadline burada.

### Server Streaming Implementation

```csharp
public override async Task ListKitaplar(
    ListKitaplarRequest request,
    IServerStreamWriter<Kitap> responseStream,
    ServerCallContext context)
{
    var kitaplar = _repo.StreamAllAsync(context.CancellationToken);

    await foreach (var kitap in kitaplar)
    {
        if (context.CancellationToken.IsCancellationRequested) break;

        await responseStream.WriteAsync(new Kitap
        {
            Id = kitap.Id,
            Ad = kitap.Ad
        });
        // ne yapar → her DB satırı gelince anında client'a gönder
        // bellekte toplanmıyor — streaming
    }
}
```

`IServerStreamWriter<Kitap>` ile mesajları teker teker gönderiyorsun. `WriteAsync` her çağrıda bir mesaj akıtıyor.

Metot bittiğinde stream otomatik kapanıyor.

### Client Streaming Implementation

```csharp
public override async Task<UploadResult> UploadKitaplar(
    IAsyncStreamReader<Kitap> requestStream,
    ServerCallContext context)
{
    int eklenen = 0;
    await foreach (var kitap in requestStream.ReadAllAsync(context.CancellationToken))
    {
        await _repo.AddAsync(new KitapEntity { Id = kitap.Id, Ad = kitap.Ad });
        eklenen++;
        // ne yapar → client'ın gönderdiği her mesajı işle
    }

    return new UploadResult { EklenenSayisi = eklenen };
    // ne yapar → stream bittikten sonra tek bir özet yanıt dön
}
```

Tüm mesajlar bittikten sonra metottan dönen değer client'a iletilir.

### Bidirectional Implementation

```csharp
public override async Task Chat(
    IAsyncStreamReader<ChatMessage> requestStream,
    IServerStreamWriter<ChatMessage> responseStream,
    ServerCallContext context)
{
    await foreach (var message in requestStream.ReadAllAsync())
    {
        // Echo bot örneği:
        await responseStream.WriteAsync(new ChatMessage
        {
            User = "Bot",
            Text = $"Mesajınız alındı: {message.Text}"
        });
        // ne yapar → her gelen mesaja anında yanıt
        // ama yanıt vermek zorunda değil — istediğin gibi async olabilir
    }
}
```

Hem read hem write paralel yapılabilir — `Task.WhenAll` ile iki iş birden yönetilebilir.

---

## Client Tarafı

### Client Proje Yapılandırması

```xml
<ItemGroup>
    <PackageReference Include="Grpc.Net.Client" Version="..." />
    <PackageReference Include="Google.Protobuf" Version="..." />
    <PackageReference Include="Grpc.Tools" Version="...">
        <PrivateAssets>all</PrivateAssets>
    </PackageReference>
</ItemGroup>

<ItemGroup>
    <Protobuf Include="Protos\catalog.proto" GrpcServices="Client" />
    <!-- GrpcServices="Client" — client kodu üretilsin -->
</ItemGroup>
```

### Unary Client

```csharp
using var channel = GrpcChannel.ForAddress("https://localhost:5001");
var client = new CatalogService.CatalogServiceClient(channel);

var kitap = await client.GetKitapAsync(new GetKitapRequest { Id = 42 });
Console.WriteLine($"Kitap: {kitap.Ad}");
// ne yapar → gRPC çağrısı, yerel metot çağırıyormuş gibi
// arka planda HTTP/2 üzerinden binary mesaj gitti
```

### Server Streaming Client

```csharp
using var call = client.ListKitaplar(new ListKitaplarRequest { Sayfa = 1 });

await foreach (var kitap in call.ResponseStream.ReadAllAsync())
{
    Console.WriteLine($"Geldi: {kitap.Ad}");
    // ne yapar → her server mesajı geldiğinde anında işle
    // bellekte hepsini biriktirmeden
}
```

### Client Streaming Client

```csharp
using var call = client.UploadKitaplar();

foreach (var kitap in kitaplarToUpload)
{
    await call.RequestStream.WriteAsync(new Kitap { Id = kitap.Id, Ad = kitap.Ad });
    // ne yapar → her kitabı tek tek server'a akıt
}
await call.RequestStream.CompleteAsync();
// ne yapar → "bitti" sinyali gönder

var result = await call.ResponseAsync;
Console.WriteLine($"Eklenen: {result.EklenenSayisi}");
```

### Bidirectional Client

```csharp
using var call = client.Chat();

// Okuma task'ı:
var readTask = Task.Run(async () =>
{
    await foreach (var msg in call.ResponseStream.ReadAllAsync())
    {
        Console.WriteLine($"{msg.User}: {msg.Text}");
    }
});

// Yazma:
await call.RequestStream.WriteAsync(new ChatMessage { User = "Berkan", Text = "Merhaba" });
await call.RequestStream.WriteAsync(new ChatMessage { User = "Berkan", Text = "Nasılsın?" });
await call.RequestStream.CompleteAsync();

await readTask;
// ne yapar → paralel read/write, klasik chat akışı
```

---

## Dependency Injection ile Client

Console örneği basit. Gerçek uygulamada DI kullanırsın:

```csharp
// Client projede Program.cs:
builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(opt =>
{
    opt.Address = new Uri("https://catalog-service:5001");
});
// ne yapar → CatalogServiceClient'ı DI'ya kaydeder
// bağlantı (channel) tekrar kullanılır — her seferinde yeni bağlantı kurmaz

// Kullanımda inject et:
public class OrderService
{
    private readonly CatalogService.CatalogServiceClient _catalogClient;

    public OrderService(CatalogService.CatalogServiceClient client)
    {
        _catalogClient = client;
    }

    public async Task ProcessOrderAsync(int kitapId)
    {
        var kitap = await _catalogClient.GetKitapAsync(new GetKitapRequest { Id = kitapId });
        // ...
    }
}
```

DI yaklaşımı: connection pooling, HttpClientFactory ile lifecycle yönetimi, test edilebilirlik.

---

## Interceptor — gRPC'nin Middleware'i

ASP.NET Core middleware'in gRPC karşılığı: **interceptor**. Her gelen/giden çağrıyı yakalayıp logla, auth kontrol et, retry yap.

```csharp
public class LoggingInterceptor : Interceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger) => _logger = logger;

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        _logger.LogInformation("gRPC çağrısı geldi: {Method}", context.Method);
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await continuation(request, context);
            _logger.LogInformation("Tamamlandı: {Method} - {Duration}ms",
                context.Method, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC hatası: {Method}", context.Method);
            throw;
        }
    }
}

// Program.cs:
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<LoggingInterceptor>();
    // ne yapar → tüm gRPC çağrılarına interceptor uygulanır
    // ASP.NET Core middleware mantığıyla aynı
});
```

İnterceptor kullanım alanları:
- Loglama (yukarıdaki)
- Authentication/Authorization
- Retry policy
- Metrics toplama
- Caching
- Validation

---

## Deadline ve Cancellation

REST'te timeout yönetimi her zaman manuel. gRPC bunu protokolün parçası yapıyor.

### Deadline

Client diyor ki: "Bu çağrı 5 saniye içinde tamamlanmazsa iptal et."

```csharp
var deadline = DateTime.UtcNow.AddSeconds(5);
var kitap = await client.GetKitapAsync(
    new GetKitapRequest { Id = 42 },
    deadline: deadline);
```

Server tarafında bu deadline `ServerCallContext.Deadline`'da görünür. Server da deadline'a saygı gösterip uzun süren işi iptal edebilir.

```csharp
public override async Task<Kitap> GetKitap(GetKitapRequest request, ServerCallContext context)
{
    // Deadline'a göre cancel:
    var kitap = await _repo.GetAsync(request.Id, context.CancellationToken);
    // ne yapar → deadline geçerse repo'daki sorgu da iptal olur
    return MapToProto(kitap);
}
```

Deadline'ın güzelliği: **client→server arasında otomatik propage olur.** Microservice A → B → C zincirinde, A 5 saniye dedi → B ve C de aynı deadline'ı biliyor → biri uzarsa hepsi iptal.

---

## Error Handling

gRPC'de hata `RpcException` ile fırlatılır:

```csharp
// Server:
throw new RpcException(new Status(StatusCode.NotFound, "Kitap bulunamadı"));

// Client:
try
{
    var kitap = await client.GetKitapAsync(...);
}
catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
{
    // 404 ile karşılık geldi
}
catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
{
    // Timeout
}
```

Gün 112'deki status code listesini hatırla — `OK`, `NOT_FOUND`, `INVALID_ARGUMENT`, vb.

Extra metadata göndermek istersen:
```csharp
var metadata = new Metadata { { "error-code", "KITAP_NOT_FOUND" } };
throw new RpcException(new Status(StatusCode.NotFound, "Kitap yok"), metadata);
```

Client `ex.Trailers` ile metadata'yı okuyabilir.

---

## Performance İpuçları

### Channel Reuse

`GrpcChannel` HTTP/2 bağlantısını yönetir. Her çağrıda yeni channel açmak felaket — TLS handshake her seferinde tekrar.

```csharp
// ❌ Yanlış — her çağrıda yeni channel:
using var channel = GrpcChannel.ForAddress("...");
var client = new CatalogServiceClient(channel);
var result = await client.GetAsync(...);
// channel kapatıldı, sonraki çağrıda tekrar açılır

// ✓ Doğru — channel singleton/DI ile yönetilir:
// Program.cs: services.AddGrpcClient<...>(...);
// Channel HttpClientFactory ile reuse edilir
```

### Streaming için Buffer

Server streaming'de mesajlar tek tek gönderiliyor — ama her `WriteAsync` ayrı network packet değil. .NET buffer'lıyor, optimal packet size'da gönderiyor. Sen düşünme.

### Compression

gRPC payload'ları varsayılan olarak compression'sız. Büyük mesajlar için gzip aktif edilebilir:

```csharp
// Server:
builder.Services.AddGrpc(opt =>
{
    opt.ResponseCompressionLevel = CompressionLevel.Fastest;
    opt.ResponseCompressionAlgorithm = "gzip";
});

// Client istek atarken:
var headers = new Metadata { { "grpc-accept-encoding", "gzip" } };
var call = client.GetKitapAsync(request, new CallOptions(headers: headers));
```

---

## ASP.NET Core gRPC Sınırları

Şunlara dikkat:

**1. Kestrel zorunlu.** IIS gRPC desteklemiyor (limited). Kestrel veya başka HTTP/2 destekli server lazım.

**2. HTTPS gerekli.** HTTP/2 modern tarayıcılarda HTTPS zorunlu. gRPC'de de development haricinde HTTPS kullanmalısın.

**3. Same-port HTTP/1.1 + HTTP/2:** Aynı port'tan hem REST hem gRPC servisi açabilirsin. Kestrel ALPN ile protokol seçer.

```csharp
builder.WebHost.ConfigureKestrel(opt =>
{
    opt.Limits.Http2.InitialConnectionWindowSize = 1024 * 1024;
    // tunable parameters
});
```

---

## 500 vs 50K Kullanıcı

| Model | 500 kullanıcı/ay | 50K kullanıcı/ay |
|-------|-------------------|-------------------|
| Unary RPC | REST yeterli | Yüksek throughput'ta gRPC daha iyi |
| Server streaming | Nadir ihtiyaç | Export, real-time data feed için değerli |
| Client streaming | Nadir | Batch upload, IoT sensör verisi için |
| Bidirectional | Çok nadir | Chat, gerçek zamanlı sync — ama SignalR de aday |
| gRPC interceptor | Overengineering | Standart pattern |

---

## Kontrol Soruları

1. gRPC'nin 4 iletişim modeli arasındaki fark nedir? Hangi senaryoda hangisi?
2. Server streaming ile REST'in chunked transfer'i arasındaki fark nedir?
3. Client streaming hangi senaryolarda 500 ayrı unary çağrıdan daha verimli?
4. Bidirectional streaming'in karmaşıklığı nedir? Ne zaman SignalR yerine bunu tercih ederiz?
5. `IAsyncStreamReader` ve `IServerStreamWriter` arasındaki rol farkı nedir?
6. gRPC interceptor ne işe yarar? ASP.NET Core middleware ile benzerlikleri ne?
7. Deadline propagation microservice zincirinde neden faydalı?
8. Channel reuse neden kritik? Her çağrıda yeni channel açmanın sorunu ne?
9. gRPC neden IIS yerine Kestrel istiyor?
