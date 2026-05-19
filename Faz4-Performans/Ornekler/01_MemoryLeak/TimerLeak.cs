// KONU: Timer'ı dispose etmemek → bellek sızıntısı
// Timer her tick'te bir şey yapıyor ama GC onu serbest bırakamıyor.

using System.Timers;

namespace Ornekler._01_MemoryLeak;

// YANLIŞ: Timer field olarak tutulmuyor, dispose edilmiyor
public class YanlisTimerKullanimi
{
    public void BaslatIzleme()
    {
        // ne yapar: her saniye çalışan bir timer oluşturur
        // bunu yazmasaydık: timer hiç çalışmazdı
        var timer = new System.Timers.Timer(1000);

        // ne yapar: timer tick olduğunda LogYaz metodunu çağırır
        // SORUN: timer local variable — bu metoddan çıkınca referans kaybolur
        // ama GC onu toplamaz çünkü Elapsed event hâlâ subscribe
        timer.Elapsed += LogYaz;
        timer.Start();

        // metod bitti, timer local scope'tan çıktı
        // GC toplamamalı mı? Hayır — Elapsed event internal queue'da referans tutuyor
        // Sonuç: timer sonsuza kadar çalışmaya devam eder, dispose edilmez
    }

    private void LogYaz(object? sender, ElapsedEventArgs e)
    {
        Console.WriteLine("Tick: " + DateTime.Now);
    }
}

// DOĞRU: IDisposable implement et, timer'ı field'da tut
public class DogruTimerKullanimi : IDisposable
{
    // ne yapar: timer referansını class seviyesinde tutar → dispose edilebilir
    // bunu yazmasaydık: Dispose() metodu timer'a erişemezdi
    private readonly System.Timers.Timer _timer;

    private bool _disposed;

    public DogruTimerKullanimi()
    {
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += LogYaz;
        _timer.Start();
    }

    private void LogYaz(object? sender, ElapsedEventArgs e)
    {
        Console.WriteLine("Tick: " + DateTime.Now);
    }

    public void Dispose()
    {
        if (_disposed) return;

        // ne yapar: timer'ı durdurur ve sistem kaynaklarını serbest bırakır
        // bunu yazmasaydık: servis kapansa bile timer arka planda çalışmaya devam ederdi
        _timer.Stop();
        _timer.Elapsed -= LogYaz;  // event subscription'ı temizle
        _timer.Dispose();
        _disposed = true;
    }
}

// Program.cs kayıt örneği:
// builder.Services.AddSingleton<DogruTimerKullanimi>();  ← AddSingleton ile Dispose otomatik çağrılır
// builder.Services.AddHostedService<IzlemeServisi>();    ← IHostedService daha iyi seçim
