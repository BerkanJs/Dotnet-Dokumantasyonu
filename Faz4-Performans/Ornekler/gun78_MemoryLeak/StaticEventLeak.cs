// GÜN 78 — Memory Leak: Static Event Sızıntısı
// Static event'e subscribe olan nesne GC tarafından toplanamaz.

namespace Ornekler.gun78;

public static class UygulamaOlaylari
{
    public static event EventHandler<string>? SiparisOlusturuldu;
    public static void Bildir(string id) => SiparisOlusturuldu?.Invoke(null, id);
}

// YANLIŞ: unsubscribe yok
public class YanlisSubscriber
{
    public YanlisSubscriber()
    {
        // ne yapar: static event'e abone olur
        // SORUN: static event bu nesneye strong reference tutar
        // → nesne dispose edilse bile GC toplayamaz → bellek sızıntısı
        UygulamaOlaylari.SiparisOlusturuldu += Geldi;
    }

    private void Geldi(object? sender, string id) => Console.WriteLine(id);
}

// DOĞRU: Dispose'da unsubscribe
public class DogruSubscriber : IDisposable
{
    private bool _disposed;

    public DogruSubscriber()
    {
        UygulamaOlaylari.SiparisOlusturuldu += Geldi;
    }

    private void Geldi(object? sender, string id) => Console.WriteLine(id);

    public void Dispose()
    {
        if (_disposed) return;
        // ne yapar: static event'ten referansı kaldırır
        // bunu yazmasaydık: bu nesne GC'den sonsuza kadar kaçardı
        UygulamaOlaylari.SiparisOlusturuldu -= Geldi;
        _disposed = true;
    }
}
