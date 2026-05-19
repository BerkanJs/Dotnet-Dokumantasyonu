// GÜN 82 — Thread Safety: Lock, Monitor, Interlocked, ReaderWriterLock
// Race condition: iki thread aynı anda aynı veriye yazarsa sonuç belirsiz.

namespace Ornekler.gun82;

// --- 1. Race condition örneği ---
public class YanlisCounter
{
    private int _sayi = 0;

    public void Artir()
    {
        // SORUN: _sayi++ üç işlemdir: oku → artır → yaz
        // İki thread aynı anda "oku" yaparsa aynı değeri görür, ikisi de 1 artırır → kayıp
        _sayi++;
    }

    public int Deger => _sayi;
}

// --- 2. lock ile düzeltme ---
public class LockliCounter
{
    private int _sayi = 0;

    // ne yapar: lock objesi olarak kullanılacak özel nesne — this veya public nesne kullanma
    // bunu yazmasaydık: lock(this) veya lock(typeof(X)) deadlock riski taşırdı
    private readonly object _kilit = new();

    public void Artir()
    {
        // ne yapar: sadece bir thread bu bloğa aynı anda girebilir
        // bunu yazmasaydık: race condition → kayıp sayım
        lock (_kilit)
        {
            _sayi++;
        }
    }

    public int Deger => _sayi;
}

// --- 3. Interlocked — lock'suz atomik işlem ---
public class InterlockedCounter
{
    private int _sayi = 0;

    public void Artir()
    {
        // ne yapar: CPU talimat seviyesinde atomik artırma — lock'tan hızlı
        // bunu yazmasaydık: lock overhead'i gereksiz yere öderdik
        // KULLANIM: sadece basit sayaçlar, flag'ler, referans değişimi için
        Interlocked.Increment(ref _sayi);
    }

    public int Deger => Volatile.Read(ref _sayi); // ne yapar: cache bypass ederek okur
}

// --- 4. ReaderWriterLockSlim — çok okuma, az yazma senaryosu ---
public class ReaderWriterOrnek
{
    private readonly Dictionary<string, string> _veri = new();

    // ne yapar: çok okuma / az yazma senaryosunda lock'tan verimli
    // bunu yazmasaydık: her okumada da exclusive lock almak zorunda kalırdık
    private readonly ReaderWriterLockSlim _rwLock = new();

    public string? Oku(string anahtar)
    {
        // ne yapar: birden fazla thread aynı anda okuyabilir
        // bunu yazmasaydık: exclusive lock ile okuma thread'leri birbirini beklerdi
        _rwLock.EnterReadLock();
        try
        {
            _veri.TryGetValue(anahtar, out var deger);
            return deger;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public void Yaz(string anahtar, string deger)
    {
        // ne yapar: yazma sırasında tüm okumalar bekler — exclusive erişim
        // bunu yazmasaydık: okuma sırasında yazılırsa Dictionary bozulurdu
        _rwLock.EnterWriteLock();
        try
        {
            _veri[anahtar] = deger;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }
}

// --- 5. SemaphoreSlim — eşzamanlı erişim sayısını sınırla ---
public class RateLimitedServis
{
    // ne yapar: aynı anda en fazla 10 thread geçebilir
    // bunu yazmasaydık: 10.000 concurrent request veritabanını çökertirdi
    private readonly SemaphoreSlim _semaphore = new(initialCount: 10, maxCount: 10);

    public async Task<string> IsleAsync(string istek)
    {
        // ne yapar: token yoksa bekle — 10'dan fazla concurrent işlem engellenir
        await _semaphore.WaitAsync();
        try
        {
            return await VeritabanIslemi(istek);
        }
        finally
        {
            // ne yapar: token'ı geri ver — bir sonraki bekleyen devam edebilir
            // bunu yazmasaydık: semaphore dolup kalırdı, hiçbir şey ilerleyemezdi
            _semaphore.Release();
        }
    }

    private Task<string> VeritabanIslemi(string istek) =>
        Task.FromResult($"Sonuc: {istek}");
}
