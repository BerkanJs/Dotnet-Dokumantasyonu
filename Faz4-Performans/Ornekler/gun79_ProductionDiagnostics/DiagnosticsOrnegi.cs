// GÜN 79 — Production Diagnostics
// dotnet-counters, dotnet-trace, dotnet-dump — CLI araçları ile canlı analiz
// Bu dosya CLI komutlarını ve C# tarafındaki entegrasyonu gösterir.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Diagnostics.NETCore.Client;

namespace Ornekler.gun79;

// --- 1. Özel Metrics (dotnet-counters ile izlenebilir) ---
public class KitapMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _siparisCounter;
    private readonly Histogram<double> _islemSuresi;
    private readonly ObservableGauge<int> _bekleyenSiparis;

    private int _bekleyenSiparisCount;

    public KitapMetrics()
    {
        // ne yapar: uygulama için bir metric namespace tanımlar
        // bunu yazmasaydık: custom counter'larımızı dotnet-counters ile izleyemezdik
        _meter = new Meter("Kitabevi.Siparis", "1.0.0");

        // ne yapar: sipariş oluşturulduğunda toplam sayıyı artırır
        // bunu yazmasaydık: kaç sipariş işlendiğini production'da bilemezdik
        _siparisCounter = _meter.CreateCounter<long>(
            "siparis.toplam",
            description: "Toplam işlenen sipariş sayısı");

        // ne yapar: her işlemin süresini histogram olarak kaydeder (p50, p95, p99)
        // bunu yazmasaydık: ortalama süreyi bilebilirdik ama outlier'ları kaçırırdık
        _islemSuresi = _meter.CreateHistogram<double>(
            "siparis.islem_suresi_ms",
            unit: "ms",
            description: "Sipariş işlem süresi");

        // ne yapar: anlık bekleyen sipariş sayısını gözlemler
        // bunu yazmasaydık: queue dolup dolmadığını anlık göremezdik
        _bekleyenSiparis = _meter.CreateObservableGauge(
            "siparis.bekleyen",
            () => _bekleyenSiparisCount,
            description: "Şu an bekleyen sipariş sayısı");
    }

    public void SiparisIslendi(double sureMilisaniye)
    {
        _siparisCounter.Add(1);                     // counter artır
        _islemSuresi.Record(sureMilisaniye);         // süreyi kaydet
    }

    public void BekleyenArtir() => Interlocked.Increment(ref _bekleyenSiparisCount);
    public void BekleyenAzalt() => Interlocked.Decrement(ref _bekleyenSiparisCount);

    public void Dispose() => _meter.Dispose();
}

// --- 2. Activity / Distributed Tracing ---
public static class TracingOrnegi
{
    // ne yapar: uygulama için ActivitySource tanımlar — her span buradan açılır
    // bunu yazmasaydık: distributed trace'e katkıda bulunamazdık
    private static readonly ActivitySource _source = new("Kitabevi.Siparis");

    public static async Task SiparisOlustur(string kitapId)
    {
        // ne yapar: bir trace span'i başlatır — başlangıç/bitiş zamanı, tag'ler
        // bunu yazmasaydık: bu işlem trace'de görünmezdi (kör nokta)
        using var activity = _source.StartActivity("SiparisOlustur");
        activity?.SetTag("kitap.id", kitapId);
        activity?.SetTag("kullanici.id", "usr_123");

        await StokKontrolEt(kitapId);

        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    private static async Task StokKontrolEt(string kitapId)
    {
        // ne yapar: iç operasyon için child span açar
        // bunu yazmasaydık: StokKontrolEt kaç ms sürdüğünü trace'de göremezdik
        using var activity = _source.StartActivity("StokKontrol");
        activity?.SetTag("kitap.id", kitapId);

        await Task.Delay(10); // simülasyon
    }
}

// CLI Komutları (terminalde çalıştır):
//
// dotnet-counters monitor --process-id <pid> --counters Kitabevi.Siparis
//   → custom metric'leri canlı izle
//
// dotnet-trace collect --process-id <pid> --providers Microsoft-Diagnostics-DiagnosticSource
//   → trace dosyası topla, Perfview ile aç
//
// dotnet-dump collect --process-id <pid>
//   → bellek dökümü al
//
// dotnet-dump analyze <dump-dosyasi>
//   → dmp dosyasını analiz et: dumpheap -stat ile büyük nesneleri bul
