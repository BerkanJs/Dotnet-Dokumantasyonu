// GÜN 85 — ThreadPool ve Task Scheduling
// ThreadPool: .NET'in thread havuzu — Task.Run, async/await hepsi buraya gider
// Starvation: tüm thread'ler meşgul → yeni Task'lar bekler → latency spike

namespace Ornekler.gun85;

public static class ThreadPoolOrnegi
{
    // --- 1. ThreadPool starvation — YANLIŞ kullanım ---
    public static async Task StarvationSenaryosu()
    {
        // SORUN: Task.Run + Task.Wait() birlikte kullanmak
        // Thread A: Task.Run başlatır
        // Thread B: işi çalıştırır, ama Task.Wait() ile A'yı bloklar
        // Tüm threadler böyle bloklanırsa → pool doldu → deadlock

        // YANLIŞ:
        // var sonuc = Task.Run(() => async işim).Result;    ← .Result bloklar
        // var sonuc = Task.Run(() => async işim).Wait();    ← Wait bloklar

        // DOĞRU:
        var sonuc = await Task.Run(() => CpuBoundIs());
        Console.WriteLine(sonuc);
    }

    private static int CpuBoundIs()
    {
        // ne yapar: CPU yoğun hesaplama — ThreadPool thread'inde çalıştır
        // bunu yazmasaydık: UI thread veya ASP.NET request thread bloklanırdı
        return Enumerable.Range(1, 1_000_000).Sum();
    }

    // --- 2. 10.000 URL paralel fetch — doğru yol ---
    public static async Task<string[]> BuyukScaleFetch(List<string> url'ler)
    {
        // YANLIŞ: tüm görevleri aynı anda başlat → 10.000 eşzamanlı HTTP bağlantısı
        // var gorevler = url'ler.Select(url => client.GetStringAsync(url));
        // await Task.WhenAll(gorevler); // → socket exhaustion

        // DOĞRU: SemaphoreSlim ile parallelism'i sınırla
        using var client = new HttpClient();
        // ne yapar: aynı anda en fazla 50 eşzamanlı istek gönderir
        // bunu yazmasaydık: 10.000 eşzamanlı bağlantı → socket exhaustion → tüm istekler hata verir
        using var semaphore = new SemaphoreSlim(50);

        async Task<string> FetchWithLimit(string url)
        {
            await semaphore.WaitAsync();
            try { return await client.GetStringAsync(url); }
            finally { semaphore.Release(); }
        }

        return await Task.WhenAll(url'ler.Select(FetchWithLimit));
    }

    // --- 3. Task.Run ne zaman kullanılır? ---
    public static void TaskRunKurallari()
    {
        // KULLAN: CPU-bound iş — UI thread'ini veya ASP.NET thread'ini serbest bırak
        Task.Run(() => AgirHesaplama());

        // KULLANMA: async I/O — zaten non-blocking, Task.Run thread harcar
        // Task.Run(async () => await File.ReadAllTextAsync("...")); // gereksiz
        // await File.ReadAllTextAsync("...");                       // doğru
    }

    private static void AgirHesaplama()
    {
        // Fibonacci, matrix çarpımı, şifreleme vb. — CPU yoğun
        Thread.Sleep(100); // simülasyon
    }

    // --- 4. Custom TaskScheduler — öncelikli kuyruk ---
    public static async Task OncelikliGorev()
    {
        // ne yapar: görevi LongRunning olarak işaretle → ayrı thread al, havuzu tıkama
        // bunu yazmasaydık: uzun süren görev havuz thread'ini meşgul ederdi → starvation
        await Task.Factory.StartNew(
            () => UzunSurenIs(),
            TaskCreationOptions.LongRunning);
    }

    private static void UzunSurenIs()
    {
        // Sonsuz döngü, uzun polling vb. — bunlar için LongRunning kullan
        Thread.Sleep(5000); // simülasyon
    }
}
