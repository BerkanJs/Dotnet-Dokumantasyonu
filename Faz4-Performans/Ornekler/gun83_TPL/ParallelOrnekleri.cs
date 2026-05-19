// GÜN 83 — Task Parallel Library (TPL)
// Parallel.For, Parallel.ForEach, Task.WhenAll, PLINQ

namespace Ornekler.gun83;

public static class ParallelOrnekleri
{
    // --- 1. Parallel.ForEach — CPU-bound iş paralel dağıt ---
    public static void ResimIsle(List<string> dosyaYollari)
    {
        // ne yapar: dosyaları ThreadPool üzerinde paralel işler
        // bunu yazmasaydık: her dosyayı sırayla işlerdik — tek çekirdek kullanımı
        // UYARI: I/O bound iş için KULLANMA → thread'leri bloklar, async/await kullan
        Parallel.ForEach(dosyaYollari, new ParallelOptions
        {
            // ne yapar: aynı anda en fazla 4 thread kullan
            // bunu yazmasaydık: MaxDegreeOfParallelism = -1 → CPU çekirdeği kadar thread
            MaxDegreeOfParallelism = 4
        },
        dosya =>
        {
            // CPU-bound iş — resim boyutlandırma vb.
            Console.WriteLine($"İşleniyor: {dosya}");
        });
    }

    // --- 2. Task.WhenAll — birden fazla async işi paralel başlat ---
    public static async Task<string[]> ParalelApiCagrilari(List<string> url'ler)
    {
        using var client = new HttpClient();

        // YANLIŞ: sıralı — her istek bir öncekini bekler
        // foreach (var url in url'ler) { await client.GetStringAsync(url); }

        // ne yapar: tüm istekleri aynı anda başlatır, hepsinin bitmesini bekler
        // bunu yazmasaydık: 10 istek × 500ms = 5000ms olurdu
        // bununla: max(tüm istekler) ≈ 500ms
        var gorevler = url'ler.Select(url => client.GetStringAsync(url));
        return await Task.WhenAll(gorevler);
    }

    // --- 3. Task.WhenAny — ilk biten kazanır (timeout pattern) ---
    public static async Task<string> TimeoutluIstek(string url, int timeoutMs)
    {
        using var client = new HttpClient();

        var istekGorevi = client.GetStringAsync(url);

        // ne yapar: istek veya timeout hangisi önce biterse onu döner
        // bunu yazmasaydık: yavaş bir API sonsuza kadar bekletirdi
        var ilkBiten = await Task.WhenAny(
            istekGorevi,
            Task.Delay(timeoutMs));

        if (ilkBiten != istekGorevi)
            throw new TimeoutException($"İstek {timeoutMs}ms içinde tamamlanmadı");

        return await istekGorevi;
    }

    // --- 4. PLINQ — paralel LINQ ---
    public static double ParalelOrtalamaHesapla(List<int> buyukListe)
    {
        // ne yapar: hesaplamayı tüm CPU çekirdeklerine dağıtır
        // bunu yazmasaydık: tek thread'de LINQ çalışırdı
        // UYARI: küçük koleksiyonlarda AsParallel overhead'i faydasını sıfırlar
        return buyukListe
            .AsParallel()
            .WithDegreeOfParallelism(Environment.ProcessorCount)
            .Where(x => x % 2 == 0)
            .Select(x => (double)(x * x))
            .Average();
    }

    // --- 5. Hata yönetimi: AggregateException ---
    public static async Task HataYonetimi()
    {
        var gorevler = new[]
        {
            Task.Run(() => { throw new InvalidOperationException("Görev 1 patladı"); }),
            Task.Run(() => { throw new ArgumentException("Görev 2 patladı"); }),
            Task.Run(() => Console.WriteLine("Görev 3 tamam"))
        };

        try
        {
            // ne yapar: tüm görevleri bekler, hata olan varsa fırlatır
            // bunu yazmasaydık: hataları tek tek handle edemezdik
            await Task.WhenAll(gorevler);
        }
        catch (Exception)
        {
            // ne yapar: WhenAll'dan gelen tüm hataları toplar
            // bunu yazmasaydık: sadece ilk hatayı görürdük
            var hatalar = gorevler
                .Where(t => t.IsFaulted)
                .SelectMany(t => t.Exception!.InnerExceptions);

            foreach (var hata in hatalar)
                Console.WriteLine(hata.Message);
        }
    }
}
