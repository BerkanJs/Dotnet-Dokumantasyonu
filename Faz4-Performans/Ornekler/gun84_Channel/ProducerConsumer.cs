// GÜN 84 — Channel: Producer-Consumer Pattern
// Channel: async-safe, backpressure destekli, thread-safe kuyruk
// Queue<T> yerine Channel — async producer ve consumer için ideal

using System.Threading.Channels;

namespace Ornekler.gun84;

// --- 1. Temel Channel kullanımı ---
public static class TemelChannel
{
    public static async Task Calistir()
    {
        // ne yapar: en fazla 100 item tutan bounded (sınırlı) channel oluşturur
        // bunu yazmasaydık: unbounded channel — producer consumer'dan hızlı giderse bellek dolar
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
        {
            // ne yapar: channel dolunca producer'ı beklet (backpressure)
            // bunu yazmasaydık: eski item'lar düşerdi (DropOldest) veya exception fırlatırdı
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,   // birden fazla producer olabilir
            SingleReader = true     // tek consumer — reader optimizasyonu
        });

        // Producer ve consumer'ı paralel başlat
        var producerGorevi = Producer(channel.Writer);
        var consumerGorevi = Consumer(channel.Reader);

        await Task.WhenAll(producerGorevi, consumerGorevi);
    }

    private static async Task Producer(ChannelWriter<string> writer)
    {
        for (int i = 0; i < 10; i++)
        {
            // ne yapar: channel doluysa bekler (backpressure), boşalınca yazar
            // bunu yazmasaydık: producer consumer'dan çok hızlı giderse bellek taşardı
            await writer.WriteAsync($"Mesaj_{i}");
            await Task.Delay(100);
        }

        // ne yapar: başka item gelmeyeceğini bildirir — consumer döngüsü sona erer
        // bunu yazmasaydık: consumer sonsuza kadar beklerdi
        writer.Complete();
    }

    private static async Task Consumer(ChannelReader<string> reader)
    {
        // ne yapar: channel'dan item'ları sırayla okur, Complete gelince döngü biter
        // bunu yazmasaydık: WaitToReadAsync + TryRead ile manuel döngü yazardık
        await foreach (var mesaj in reader.ReadAllAsync())
        {
            Console.WriteLine($"İşleniyor: {mesaj}");
            await Task.Delay(150); // consumer daha yavaş — backpressure devreye girer
        }
    }
}

// --- 2. Gerçek senaryo: ASP.NET'te background iş kuyruğu ---
public interface IIsKuyruğu
{
    ValueTask KuyruğaEkle(Func<CancellationToken, ValueTask> is_);
    ValueTask<Func<CancellationToken, ValueTask>> SonrakiAl(CancellationToken ct);
}

public class ChannelIsKuyrugu : IIsKuyruğu
{
    // ne yapar: sınırsız channel — HTTP isteği anında kabul et, arka planda işle
    // bunu yazmasaydık: her iş için yeni Task.Run ve thread yönetimi elle yapardık
    private readonly Channel<Func<CancellationToken, ValueTask>> _queue =
        Channel.CreateUnbounded<Func<CancellationToken, ValueTask>>();

    public async ValueTask KuyruğaEkle(Func<CancellationToken, ValueTask> is_)
    {
        // ne yapar: işi kuyruğa ekler — HTTP isteği hemen döner
        // bunu yazmasaydık: iş tamamlanana kadar HTTP isteği bloklanırdı
        await _queue.Writer.WriteAsync(is_);
    }

    public async ValueTask<Func<CancellationToken, ValueTask>> SonrakiAl(CancellationToken ct)
    {
        // ne yapar: kuyrukta iş yoksa bekler, gelince alır
        // bunu yazmasaydık: polling döngüsüyle CPU boşa harcanırdı
        return await _queue.Reader.ReadAsync(ct);
    }
}

// Background service — Channel'dan okur, işler
public class IsciServisi : BackgroundService
{
    private readonly IIsKuyruğu _kuyruk;

    public IsciServisi(IIsKuyruğu kuyruk) => _kuyruk = kuyruk;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var is_ = await _kuyruk.SonrakiAl(stoppingToken);

            // ne yapar: her işi ayrı Task'ta çalıştır — birbirini bloklamasın
            // bunu yazmasaydık: uzun süren bir iş tüm diğer işleri bekletirdi
            _ = Task.Run(() => is_(stoppingToken), stoppingToken);
        }
    }
}

// Program.cs:
// builder.Services.AddSingleton<IIsKuyruğu, ChannelIsKuyrugu>();
// builder.Services.AddHostedService<IsciServisi>();
