# Gün 84 — Channel\<T\> ve Producer-Consumer

---

## Producer-Consumer Nedir?

Bazı sistemlerde işin üretilmesi ile işlenmesi farklı hızlarda gerçekleşir. Biri diğerini beklemek yerine aralarına bir kuyruk koyarsın — producer kuyruğa yazar, consumer kuyruktan okur, ikisi birbirinden bağımsız çalışır.

Gerçek dünya benzetmesi: restoran mutfağı. Garsonlar (producer) siparişleri bilet asma çubuğuna asar. Aşçılar (consumer) oradan alıp pişirir. Garson aşçının bitmesini beklemez, aşçı da garsonun gelmesini beklemez. Çubuk dolarsa garson bekler — bu backpressure.

.NET'te bu yapıyı kurmanın modern yolu `Channel<T>`'dir.

---

## Channel\<T\> Nedir?

`Channel<T>`, async-friendly, thread-safe bir kuyruktur. `ConcurrentQueue<T>`'dan farkı şudur: kuyruk boşsa consumer async olarak bekler — thread bloklanmaz. Kuyruk doluysa producer async olarak bekler — backpressure çalışır.

---

## Bounded vs Unbounded

```csharp
// Unbounded — sınırsız kapasite
var channel = Channel.CreateUnbounded<Siparis>();
// Producer hiç beklemez, istediği kadar yazar
// Risk: consumer yavaşlarsa bellek sonsuza büyür

// Bounded — sabit kapasite
var channel = Channel.CreateBounded<Siparis>(capacity: 100);
// Kapasite dolunca producer bekler (backpressure)
// Bellek kontrolü sağlanır — maksimum 100 sipariş aynı anda kuyruğta
```

**Hangisini seçmeli?**  
Gerçek uygulamada neredeyse her zaman `Bounded` — sistemi aşırı yükten korur.  
`Unbounded` yalnızca producer'ın kesinlikle consumer'dan daha hızlı olmayacağı garantisi varsa.

---

## Temel Kullanım

```csharp
var channel = Channel.CreateBounded<Siparis>(100);

// Producer — siparişleri kuyruğa ekle
async Task ProducerAsync(CancellationToken ct)
{
    await foreach (var siparis in SiparisAkisiAl(ct))
    {
        await channel.Writer.WriteAsync(siparis, ct);
        // kapasite doluysa burada bekler — backpressure devrede
        // bunu TryWrite ile yazsaydık → kapasite dolunca siparişi düşürürdük
    }

    channel.Writer.Complete();
    // bunu yazmasaydık → consumer ReadAllAsync'ten hiç çıkamaz, sonsuz bekler
}

// Consumer — kuyruktaki siparişleri işle
async Task ConsumerAsync(CancellationToken ct)
{
    await foreach (var siparis in channel.Reader.ReadAllAsync(ct))
    {
        // kuyruk boşsa burada async bekler — thread bloklanmaz
        await IsleAsync(siparis);
    }
    // Writer.Complete() çağrılınca ve kuyruk boşalınca döngü biter
}

// İkisini aynı anda başlat
await Task.WhenAll(ProducerAsync(ct), ConsumerAsync(ct));
```

---

## Birden Fazla Consumer — Yükü Dağıt

Tek consumer yetişemiyorsa birden fazla consumer aynı channel'dan okuyabilir.

```csharp
var channel = Channel.CreateBounded<Siparis>(500);

// 1 producer
var producer = ProducerAsync(channel.Writer, ct);

// 5 paralel consumer — yükü paylaşır
var consumers = Enumerable.Range(0, 5)
    .Select(_ => ConsumerAsync(channel.Reader, ct));

await Task.WhenAll(new[] { producer }.Concat(consumers));
```

Her sipariş yalnızca bir consumer tarafından alınır — channel bunu garanti eder.

---

## IAsyncEnumerable ile Channel Tüketimi

`ReadAllAsync()` zaten `IAsyncEnumerable<T>` döner — doğal uyum:

```csharp
async Task ConsumerAsync(ChannelReader<Siparis> reader, CancellationToken ct)
{
    await foreach (var siparis in reader.ReadAllAsync(ct))
    {
        await IsleAsync(siparis);
    }
}
```

---

## Dataflow — TransformBlock ve ActionBlock

`System.Threading.Tasks.Dataflow` paketi, channel'ın üzerine pipeline oluşturmayı sağlar. Veri bir bloktan diğerine akar — her blok dönüşüm veya işlem yapar.

```csharp
// Paket: System.Threading.Tasks.Dataflow (NuGet)

// 1. Blok: Ham veriyi parse et
var parseBlok = new TransformBlock<string, Siparis>(satir =>
{
    return Siparis.Parse(satir);    // string → Siparis
}, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 4 });
// MaxDegreeOfParallelism → aynı anda 4 thread parse yapar

// 2. Blok: Parse edilmiş siparişi kaydet
var kaydetBlok = new ActionBlock<Siparis>(async siparis =>
{
    await _repo.EkleAsync(siparis);
}, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 2 });

// Blokları birbirine bağla — pipe
parseBlok.LinkTo(kaydetBlok, new DataflowLinkOptions { PropagateCompletion = true });
// PropagateCompletion → parseBlok bitince kaydetBlok'a otomatik "bitti" sinyali gider

// Veri gönder
foreach (var satir in dosyaSatirlari)
    await parseBlok.SendAsync(satir);

parseBlok.Complete();               // producer bitti
await kaydetBlok.Completion;        // tüm pipeline tamamlanana kadar bekle
```

**Ne zaman Dataflow, ne zaman Channel?**  
Birden fazla dönüşüm adımı olan pipeline varsa → Dataflow  
Basit producer/consumer → Channel yeterli

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de her HTTP isteği senkron işleniyordu — sipariş gelir, DB'ye yazılır, kullanıcı bekler. Yüksek trafikte bu model tıkanır.

Faz4'te: sipariş HTTP katmanında alınır → channel'a yazılır → kullanıcıya "alındı" döner → arka planda consumer işler. Kullanıcı DB yazımını beklemez, sistem daha fazla isteği karşılayabilir.

---

## 500 vs 50K Kullanıcı

| | 500 | 50K |
|---|---|---|
| Channel kurma | Overengineering olabilir | Yük dengeleme için kritik |
| Bounded kapasite | İyi alışkanlık | Zorunlu — bellek kontrolü |
| Birden fazla consumer | Gerekmez | Darboğaz çözümü |
| Dataflow | Nadiren gerekir | Karmaşık pipeline'larda değerli |

---

## Kontrol Soruları

1. `Unbounded` channel ne zaman tehlikeli hale gelir?
2. `Writer.Complete()` çağrılmazsa consumer ne yapar?
3. Aynı channel'dan birden fazla consumer okursa her mesaj kaç kez işlenir?
4. `TransformBlock` ile `ActionBlock` arasındaki fark nedir?
