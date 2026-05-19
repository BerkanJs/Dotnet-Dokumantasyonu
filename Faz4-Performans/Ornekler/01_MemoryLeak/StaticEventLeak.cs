// KONU: Static event'e subscribe olmak → nesne GC tarafından toplanamaz
// Static event, subscribe olan nesneye güçlü referans (strong reference) tutar.
// Nesneyi dispose etsen bile GC onu serbest bırakmaz.

namespace Ornekler._01_MemoryLeak;

// Uygulama genelinde bir event yayıncısı — static
public static class UygulamaOlaylari
{
    // ne yapar: uygulama genelinde sipariş oluşturulduğunda tetiklenir
    // bunu yazmasaydık: event-driven mimari kuramazdık
    public static event EventHandler<string>? SiparisOlusturuldu;

    public static void SiparisBildir(string siparisId)
    {
        SiparisOlusturuldu?.Invoke(null, siparisId);
    }
}

// YANLIŞ: Subscribe olup unsubscribe etmiyoruz
public class YanlisEventKullanici
{
    public YanlisEventKullanici()
    {
        // ne yapar: static event'e abone olur
        // SORUN: static event bu nesneye strong reference tutar
        // Bu nesneyi dispose etsen bile GC toplamaz — static event referansı devam eder
        UygulamaOlaylari.SiparisOlusturuldu += SiparisGeldi;
    }

    private void SiparisGeldi(object? sender, string siparisId)
    {
        Console.WriteLine($"Sipariş alındı: {siparisId}");
    }

    // Dispose yok → static event referansı sonsuza dek tutuluyor
}

// DOĞRU: IDisposable ile unsubscribe
public class DogruEventKullanici : IDisposable
{
    private bool _disposed;

    public DogruEventKullanici()
    {
        UygulamaOlaylari.SiparisOlusturuldu += SiparisGeldi;
    }

    private void SiparisGeldi(object? sender, string siparisId)
    {
        Console.WriteLine($"Sipariş alındı: {siparisId}");
    }

    public void Dispose()
    {
        if (_disposed) return;

        // ne yapar: static event'ten aboneliği kaldırır
        // bunu yazmasaydık: bu nesne Dispose edilse bile GC onu asla toplayamazdı
        UygulamaOlaylari.SiparisOlusturuldu -= SiparisGeldi;
        _disposed = true;
    }
}

// WeakReference alternatifi — strong reference istemiyorsan:
public class WeakEventKullanici
{
    // ne yapar: nesneye weak reference tutar — GC serbestçe toplayabilir
    // bunu yazmasaydık: subscribe olan nesne GC'den kaçardı
    private readonly WeakReference<Action<string>> _handler;

    public WeakEventKullanici()
    {
        _handler = new WeakReference<Action<string>>(SiparisGeldi);

        UygulamaOlaylari.SiparisOlusturuldu += (_, id) =>
        {
            if (_handler.TryGetTarget(out var handler))
                handler(id);
            // target GC tarafından toplandıysa hiçbir şey yapmaz
        };
    }

    private void SiparisGeldi(string siparisId)
    {
        Console.WriteLine($"Sipariş (weak): {siparisId}");
    }
}
