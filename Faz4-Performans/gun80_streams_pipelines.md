# Gün 80 — System.IO Streams, Pipelines ve Networking

Büyük veriyi belleğe almadan okumak, yazmak ve aktarmak — .NET'in stream altyapısı bu işin temelidir.

---

## Stream Nedir?

**Buffer:** Tüm veriyi önce belleğe al, sonra işle.  
**Stream:** Veri parça parça gelir, her parçayı gelince işle — tüm veri beklenmez.

```
Buffer:  [dosyanın tamamı belleğe] → işle       → 500 MB dosya = 500 MB RAM
Stream:  [4 KB chunk] → işle → [4 KB chunk] → işle → 500 MB dosya = 4 KB RAM
```

---

## Stream Hiyerarşisi

```
Stream (abstract)
├── FileStream       → dosya okuma/yazma
├── MemoryStream     → bellek üzerinde stream (test, geçici buffer)
├── NetworkStream    → TCP socket üzerinden okuma/yazma
├── BufferedStream   → başka stream'i buffer'layarak hızlandırır
├── GZipStream       → sıkıştırma/açma (başka stream'i sarar)
└── CryptoStream     → şifreleme/çözme (başka stream'i sarar)
```

---

## Temel Okuma — Chunk'larla

```csharp
await using var fs = new FileStream("buyuk_dosya.csv", FileMode.Open);
// await using → FileStream IAsyncDisposable → Dispose garantili

var buffer = new byte[4096];                // 4 KB'lık okuma penceresi
int okunan;

while ((okunan = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
{
    // okunan → bu turda kaç byte geldi (son turda buffer.Length'ten az olabilir)
    // bunu fs.ReadToEndAsync() ile yapsaydık → tüm dosya belleğe girdi
    await IsleAsync(buffer, 0, okunan);
}
```

---

## Stream Composition — Katmanlı Sarma

Stream'leri birbirinin içine sarabilirsın. Her katman bir dönüşüm ekler.

```csharp
// Dosyayı okurken aynı anda sıkıştır ve şifrele — belleğe hiç tam hali girmez
await using var dosya   = new FileStream("kitaplar.csv", FileMode.Open);
await using var gzip    = new GZipStream(dosya, CompressionMode.Compress);
// GZipStream, dosya stream'inden okuduğu her chunk'ı sıkıştırır

await using var hedef   = new FileStream("kitaplar.csv.gz", FileMode.Create);
await gzip.CopyToAsync(hedef);
// CopyToAsync → chunk'ları otomatik aktarır, manuel döngü gerekmez
// bunu MemoryStream ile yapsaydık → önce tüm dosyayı belleğe, sonra sıkıştır → iki kat RAM
```

Üçlü zincir — oku, sıkıştır, şifrele:

```csharp
await using var kaynak  = new FileStream("data.csv", FileMode.Open);
await using var gzip    = new GZipStream(kaynak, CompressionMode.Compress);
await using var crypto  = new CryptoStream(gzip, encryptor, CryptoStreamMode.Read);
await using var hedef   = new FileStream("data.csv.gz.enc", FileMode.Create);

await crypto.CopyToAsync(hedef);
// Akış: FileStream → GZipStream → CryptoStream → hedef
// Her chunk sırayla sıkıştırılır ve şifrelenir — RAM'de tek chunk
```

---

## Stream.CopyToAsync — Pipe Karşılığı

```csharp
// İki stream'i birbirine bağla — pipe() karşılığı
await kaynak.CopyToAsync(hedef, cancellationToken);
// chunk'ları otomatik okur ve yazar, döngü yazmana gerek yok
// buffer boyutunu kendin vermek istersen:
await kaynak.CopyToAsync(hedef, bufferSize: 81920, cancellationToken);
```

---

## System.IO.Pipelines — Yüksek Performanslı IO

**Ne işe yarar?** Stream API'sinde buffer yönetimi manuel ve hatalıdır. `System.IO.Pipelines` buffer'ı otomatik yönetir, yeniden kullanır, backpressure uygular.

**Backpressure:** Consumer (işleyen taraf) yazandan yavaşsa pipe otomatik olarak yazanı yavaşlatır — bellek taşmaz.

```csharp
// Büyük CSV dosyasını satır satır oku — sıfır gereksiz allocation
var pipe = new Pipe();

// Writer — dosyadan pipe'a veri doldur
async Task Doldur(PipeWriter writer)
{
    await using var fs = File.OpenRead("buyuk.csv");
    await fs.CopyToAsync(writer);       // FileStream → PipeWriter
    await writer.CompleteAsync();       // bitti işareti
}

// Reader — pipe'tan satır satır oku
async Task Oku(PipeReader reader)
{
    while (true)
    {
        ReadResult result = await reader.ReadAsync();
        ReadOnlySequence<byte> buffer = result.Buffer;

        while (TrySatirBul(ref buffer, out ReadOnlySequence<byte> satir))
        {
            SatiriIsle(satir);          // allocation yok — doğrudan buffer'a bakıyor
        }

        reader.AdvanceTo(buffer.Start, buffer.End);
        // bunu yazmasaydık → reader buffer'ı serbest bırakmaz, bellek dolar

        if (result.IsCompleted) break;
    }
    await reader.CompleteAsync();
}

await Task.WhenAll(Doldur(pipe.Writer), Oku(pipe.Reader));
```

ASP.NET Core'un Kestrel'i zaten içten `System.IO.Pipelines` kullanır — yüksek throughput'un sırrı bu.

---

## TCP Socket — TcpListener / TcpClient

```csharp
// Sunucu — bağlantı kabul et
var listener = new TcpListener(IPAddress.Any, 5000);
listener.Start();

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync(ct);
    // her bağlantı için ayrı task başlat — bloklamadan devam et
    _ = Task.Run(() => BaglantiIsle(client), ct);
}

async Task BaglantiIsle(TcpClient client)
{
    await using var _ = client;
    NetworkStream stream = client.GetStream();  // hem okunabilir hem yazılabilir
    var buffer = new byte[1024];
    int n = await stream.ReadAsync(buffer, ct);
    await stream.WriteAsync(buffer[..n], ct);   // echo — gelen veriyi geri gönder
}
```

---

## HTTP Range Requests — Büyük Dosya Streaming

**Ne işe yarar?** Kullanıcı 2 GB'lık kitap PDF'ini yarıda kesti, internet kesildi. Range header ile kaldığı yerden devam edebilir.

```csharp
// ASP.NET Core — otomatik range desteği
app.MapGet("/dosya/{ad}", (string ad) =>
{
    var yol = Path.Combine("Dosyalar", ad);
    return Results.File(yol, "application/pdf", enableRangeProcessing: true);
    // enableRangeProcessing: true → ASP.NET, Range header'ı okur
    // Range: bytes=1048576-2097151 → sadece o kısmı gönderir
    // 206 Partial Content döner — tam dosya değil
    // bunu false yazsaydık → her seferinde baştan gönderir, resume olmaz
});
```

---

## SSE — Server-Sent Events ile Push Streaming

**Ne işe yarar?** Client'a sürekli veri göndermek istiyorsun ama WebSocket kurmak istemiyorsun. SSE tek yönlü (sunucu → client) ve HTTP üzerinden çalışır.

```csharp
app.MapGet("/canli-fiyatlar", async (HttpResponse response, CancellationToken ct) =>
{
    response.Headers.ContentType = "text/event-stream";
    // bu header olmadan tarayıcı SSE olarak işlemez

    await foreach (var fiyat in FiyatStreamiAl(ct))
    {
        await response.WriteAsync($"data: {fiyat}\n\n", ct);
        // SSE formatı: "data: <veri>\n\n" — çift newline zorunlu
        await response.Body.FlushAsync(ct);
        // Flush olmazsa → buffer dolana kadar client'a hiçbir şey gitmez
    }
});
```

---

## Özet — Ne Zaman Ne?

| Durum | Araç |
|---|---|
| Dosya okuma/yazma | `FileStream` |
| Bellek üzerinde test | `MemoryStream` |
| Sıkıştırma | `GZipStream(stream)` sarma |
| İki stream bağlama | `CopyToAsync()` |
| Yüksek throughput IO | `System.IO.Pipelines` |
| TCP bağlantı | `TcpListener` / `TcpClient` |
| Büyük dosya indirme | `enableRangeProcessing: true` |
| Sunucudan push | SSE (`text/event-stream`) |

---

## Kontrol Soruları

1. `GZipStream` ve `CryptoStream`'i neden `FileStream`'e ayrı ayrı değil, iç içe sarıyoruz?
2. `System.IO.Pipelines`'da `reader.AdvanceTo()` çağrılmazsa ne olur?
3. Backpressure olmadan producer/consumer farkı ne soruna yol açar?
4. `enableRangeProcessing: true` olmadan büyük dosya indirirken kullanıcıya ne sorun çıkar?
