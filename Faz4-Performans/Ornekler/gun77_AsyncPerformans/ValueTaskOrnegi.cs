// GÜN 77 — Async Performans: ValueTask ve ConfigureAwait
// Task her zaman heap'te allocation yapar.
// ValueTask: sonuç zaten hazırsa allocation yok — cache, senkron path optimizasyonu için idealdir.

namespace Ornekler.gun77;

public class ValueTaskOrnegi
{
    private static readonly Dictionary<int, string> _cache = new();

    // --- SENARYO: Cache varsa senkron dön, yoksa async fetch ---

    // YANLIŞ: Task döner — sonuç cache'deyse bile heap allocation
    public async Task<string> TaskIleGetir(int id)
    {
        if (_cache.TryGetValue(id, out var cached))
            // ne yapar: Task.FromResult yeni bir Task nesnesi oluşturur
            // bunu yazmasaydık: await etmeden return edemezdik
            // SORUN: her cache hit'te heap allocation → GC baskısı
            return cached;

        var veri = await VeritabanindanGetirAsync(id);
        _cache[id] = veri;
        return veri;
    }

    // DOĞRU: ValueTask — cache hit'te sıfır allocation
    public ValueTask<string> ValueTaskIleGetir(int id)
    {
        if (_cache.TryGetValue(id, out var cached))
            // ne yapar: struct olarak sonucu sarar — heap allocation yok
            // bunu yazmasaydık: Task.FromResult ile her cache hit'te allocation olurdu
            return ValueTask.FromResult(cached);

        // async path — gerçekten bekleme gerekiyorsa Task kullanır
        return new ValueTask<string>(FetchAndCacheAsync(id));
    }

    private async Task<string> FetchAndCacheAsync(int id)
    {
        var veri = await VeritabanindanGetirAsync(id);
        _cache[id] = veri;
        return veri;
    }

    private Task<string> VeritabanindanGetirAsync(int id) =>
        Task.FromResult($"Veri_{id}");
}

// --- ConfigureAwait ---
public class ConfigureAwaitOrnegi
{
    // ne yapar: await sonrası orijinal SynchronizationContext'e dönmez
    // bunu yazmasaydık: UI/ASP.NET context'te deadlock riski, kütüphane kodunda gereksiz context switch
    public async Task<string> KutuphaneMetodu(int id)
    {
        // Library kodunda her await'e ConfigureAwait(false) ekle
        var sonuc = await VeritabanindanGetirAsync(id).ConfigureAwait(false);
        // bu satır threadpool thread'inde çalışır — UI thread'ine dönmez
        return sonuc.ToUpperInvariant();
    }

    // ne yapar: ConfigureAwait(false) — context yakalamayı devre dışı bırakır
    // bunu yazmasaydık: ASP.NET Core'da deadlock olmazdı (ASP.NET Core'un context'i yok)
    // ama kütüphane WinForms/WPF içinde kullanılırsa deadlock olabilirdi
    private Task<string> VeritabanindanGetirAsync(int id) =>
        Task.FromResult($"Veri_{id}");

    // KURAL:
    // Application kodu (Controller, Handler): ConfigureAwait(false) gerekmez
    // Library/Infrastructure kodu: her await'e ConfigureAwait(false) ekle
}

// --- Senkron tamamlanan async metodlar için IValueTaskSource ---
public class ManuelValueTask : IValueTaskSource<int>
{
    // Çok ileri seviye — genellikle framework/library geliştiriciler kullanır
    // Kendi custom state machine'ini yazmanı sağlar

    int IValueTaskSource<int>.GetResult(short token) => 42;

    ValueTaskSourceStatus IValueTaskSource<int>.GetStatus(short token)
        => ValueTaskSourceStatus.Succeeded;

    void IValueTaskSource<int>.OnCompleted(
        Action<object?> continuation, object? state,
        short token, ValueTaskSourceOnCompletedFlags flags)
    { }
}
