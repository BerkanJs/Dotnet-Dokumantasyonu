// GÜN 78 — Memory Leak: Timer Sızıntısı
// Timer dispose edilmezse GC onu asla toplamaz — sonsuza kadar çalışır.

using System.Timers;

namespace Ornekler.gun78;

// YANLIŞ
public class YanlisTimerKullanimi
{
    public void BaslatIzleme()
    {
        var timer = new System.Timers.Timer(1000);

        // ne yapar: timer'ı Elapsed event'e bağlar
        // SORUN: timer local scope'dan çıkınca referans kaybolur ama
        // Elapsed event timer'a strong reference tutar → GC toplayamaz
        // Sonuç: timer sonsuza kadar çalışır, bellek serbest kalmaz
        timer.Elapsed += (_, _) => Console.WriteLine("Tick");
        timer.Start();
    }
}

// DOĞRU
public class DogruTimerKullanimi : IDisposable
{
    // ne yapar: timer'ı field'da tutarak Dispose'dan erişilebilir yapar
    // bunu yazmasaydık: Dispose çağrıldığında timer'a ulaşamazdık
    private readonly System.Timers.Timer _timer;
    private bool _disposed;

    public DogruTimerKullanimi()
    {
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += Calistir;
        _timer.Start();
    }

    private void Calistir(object? sender, ElapsedEventArgs e)
        => Console.WriteLine("Tick: " + DateTime.Now);

    public void Dispose()
    {
        if (_disposed) return;
        // ne yapar: timer'ı durdurur, event subscription'ı temizler, kaynakları serbest bırakır
        // bunu yazmasaydık: Dispose çağrılsa bile timer arka planda çalışmaya devam ederdi
        _timer.Stop();
        _timer.Elapsed -= Calistir;
        _timer.Dispose();
        _disposed = true;
    }
}
