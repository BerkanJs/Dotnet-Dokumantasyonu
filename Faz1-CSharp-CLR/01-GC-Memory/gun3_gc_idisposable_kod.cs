// Gün 3 — Garbage Collector ve IDisposable: Kod Demoları

using System;
using System.Diagnostics;
using System.Runtime;

// ============================================================
// BÖLÜM 1: GC nesil bilgisi — nesne hangi nesilde?
// ============================================================
Console.WriteLine("=== GC Nesil Takibi ===");

// Yeni nesne Gen 0'da başlar
var nesne = new object();
Console.WriteLine($"Yeni oluştu — Gen: {GC.GetGeneration(nesne)}");  // 0

// GC'yi tetikle — nesne hayatta kalırsa nesil ilerler
GC.Collect(0);  // sadece Gen 0
GC.WaitForPendingFinalizers();
Console.WriteLine($"Gen 0 GC sonrası — Gen: {GC.GetGeneration(nesne)}");  // 1

GC.Collect(1);  // Gen 0 + Gen 1
GC.WaitForPendingFinalizers();
Console.WriteLine($"Gen 1 GC sonrası — Gen: {GC.GetGeneration(nesne)}");  // 2

// LOH kontrolü: büyük nesne hangi nesilde?
var buyukDizi = new byte[100_000];  // ~100KB → LOH'a gider
Console.WriteLine($"LOH nesnesi — Gen: {GC.GetGeneration(buyukDizi)}");  // 2 (LOH = Gen 2 gibi davranır)

Console.WriteLine();

// ============================================================
// BÖLÜM 2: Bellek bilgisi — GC ne kadar yer kullanıyor?
// ============================================================
Console.WriteLine("=== GC Bellek Bilgisi ===");

// GC'nin yönettiği toplam bellek
long oncesi = GC.GetTotalMemory(forceFullCollection: false);
Console.WriteLine($"Şu anki heap kullanımı: {oncesi / 1024} KB");

// 10.000 nesne oluştur
var liste = new System.Collections.Generic.List<object>();
for (int i = 0; i < 10_000; i++)
    liste.Add(new byte[100]);

long sonrasi = GC.GetTotalMemory(forceFullCollection: false);
Console.WriteLine($"10.000 nesne sonrası: {sonrasi / 1024} KB");
Console.WriteLine($"Fark: {(sonrasi - oncesi) / 1024} KB");

// Referansı bırak — GC toplayabilir
liste = null;
long temizlendi = GC.GetTotalMemory(forceFullCollection: true);  // force GC
Console.WriteLine($"GC sonrası: {temizlendi / 1024} KB");

Console.WriteLine();

// ============================================================
// BÖLÜM 3: Finalizer — nesne ömrü nasıl uzar
// ============================================================
Console.WriteLine("=== Finalizer Etkisi ===");

// Finalizer'lı nesne oluştur
var finalizerliNesne = new FinalizerOrnegi("nesne-1");
Console.WriteLine("Nesne oluştu, referans bırakılıyor...");
finalizerliNesne = null;

// Gen 0 GC — nesne toplanmaya çalışılır ama finalizer kuyruğuna girer
GC.Collect(0);
Console.WriteLine("Gen 0 GC çalıştı — nesne finalizer kuyruğunda, henüz silinmedi");

// Finalizer thread'inin çalışmasını bekle
GC.WaitForPendingFinalizers();
Console.WriteLine("Finalizer çalıştı");

// Şimdi bir GC daha → nesne artık silinebilir
GC.Collect(0);
Console.WriteLine("İkinci GC — nesne artık gerçekten silindi");

Console.WriteLine();

// ============================================================
// BÖLÜM 4: IDisposable — using ile deterministik temizlik
// ============================================================
Console.WriteLine("=== IDisposable ve using ===");

// using bloğu: blok bitince Dispose otomatik çağrılır
using (var kaynak = new YonetilenKaynak("bağlantı-1"))
{
    Console.WriteLine("Kaynak kullanılıyor...");
    kaynak.IslemYap();
}  // burada Dispose() otomatik çağrıldı
Console.WriteLine("using bloğu bitti — Dispose çağrıldı");

Console.WriteLine();

// C# 8+ kısa using — scope bitince Dispose çağrılır
using var kaynak2 = new YonetilenKaynak("bağlantı-2");
kaynak2.IslemYap();
// metot veya blok bitince Dispose çağrılır

Console.WriteLine();

// ============================================================
// BÖLÜM 5: Dispose edilmemiş kaynak simülasyonu
// ============================================================
Console.WriteLine("=== Dispose Edilmemiş Kaynak ===");

var kaynak3 = new YonetilenKaynak("bağlantı-3");
kaynak3.IslemYap();
// kaynak3.Dispose() çağrılmadı!
// Kaynak "sızıyor" — GC eninde sonunda Finalizer çalıştırır
// ama ne zaman? Belirsiz. Production'da bu bir bug.
Console.WriteLine("Kaynak dispose edilmedi — finalizer eninde sonunda çalışacak (belirsiz)");

Console.WriteLine();

// ============================================================
// BÖLÜM 6: Tam Dispose Pattern
// ============================================================
Console.WriteLine("=== Tam Dispose Pattern ===");

using var tam = new TamDisposePattern();
tam.Calis();
// Dispose çağrıldığında hem managed hem unmanaged kaynaklar temizlenir
// GC.SuppressFinalize sayesinde finalizer çalışmaz — verimli

// ============================================================
// Tip tanımları
// ============================================================

// Finalizer'ı olan nesne — GC sürecini nasıl uzattığını gösterir
class FinalizerOrnegi
{
    private readonly string _ad;

    public FinalizerOrnegi(string ad)
    {
        _ad = ad;
        Console.WriteLine($"  [{_ad}] oluşturuldu");
    }

    // Finalizer — nesne silinmeden önce çağrılır
    ~FinalizerOrnegi()
    {
        Console.WriteLine($"  [{_ad}] finalizer çalıştı — şimdi silinebilir");
    }
}

// Basit IDisposable uygulama
class YonetilenKaynak : IDisposable
{
    private readonly string _ad;
    private bool _disposed = false;

    public YonetilenKaynak(string ad)
    {
        _ad = ad;
        Console.WriteLine($"  [{_ad}] kaynak açıldı");
    }

    public void IslemYap()
    {
        if (_disposed) throw new ObjectDisposedException(_ad);
        Console.WriteLine($"  [{_ad}] işlem yapılıyor");
    }

    public void Dispose()
    {
        if (_disposed) return;
        Console.WriteLine($"  [{_ad}] Dispose() çağrıldı — kaynak kapatıldı");
        _disposed = true;
    }

    // Finalizer: Dispose unutulursa son güvence
    ~YonetilenKaynak()
    {
        if (!_disposed)
            Console.WriteLine($"  [{_ad}] UYARI: Dispose çağrılmadı, finalizer temizliyor!");
        Dispose();
    }
}

// Tam Dispose Pattern — managed + unmanaged kaynak
class TamDisposePattern : IDisposable
{
    private bool _disposed = false;

    // Örnek managed kaynak
    private System.IO.MemoryStream _stream = new System.IO.MemoryStream();

    public void Calis() => Console.WriteLine("  TamDisposePattern çalışıyor");

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);  // finalizer'ı atla — Dispose zaten çalıştı
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Managed kaynakları temizle
            _stream?.Dispose();
            Console.WriteLine("  Managed kaynaklar temizlendi");
        }

        // Unmanaged kaynaklar burada temizlenirdi (IntPtr vb.)
        Console.WriteLine("  Dispose tamamlandı");

        _disposed = true;
    }

    ~TamDisposePattern()
    {
        Dispose(disposing: false);  // sadece unmanaged için
    }
}
