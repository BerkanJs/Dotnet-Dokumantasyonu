using Hangfire;
using KitabeviMVC.Services;

namespace KitabeviMVC.BackgroundServices;

// Gün 25: Hangfire — persistence + retry + dashboard.
//
// Senaryo:
//   - Sipariş sonrası fatura e-postası → fire-and-forget (anında kuyruğa at, arka planda çalış)
//   - Her gece 02:00'de stok raporu → recurring job
//   - Ödeme sonrası kargo oluştur → continuation (A bitti → B başlat)
//
// Neden Hangfire?
//   → Uygulama çökse bile job kaybolmaz (InMemory yerine SQL/Redis ile production'da)
//   → Otomatik retry — geçici hatalardan kurtarır
//   → Ops ekibi /hangfire dashboard'dan job durumunu görür
public class KitabeviJoblar
{
    private readonly IKitapServisi _kitapServisi;
    private readonly ILogger<KitabeviJoblar> _logger;

    // DI container bu sınıfı Hangfire job'ı olarak çalıştırırken inject eder.
    // Hangfire her job çalıştırmasında yeni instance oluşturur.
    public KitabeviJoblar(IKitapServisi kitapServisi, ILogger<KitabeviJoblar> logger)
    {
        _kitapServisi = kitapServisi;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // Fire-and-forget job — fatura e-postası
    //
    // Controller'dan çağrılır:
    //   BackgroundJob.Enqueue<KitabeviJoblar>(j => j.FaturaEmailiGonder(siparisId));
    //
    // [AutomaticRetry] → başarısız olursa 3 kez daha dene.
    // Hangfire hataları yakalar, üstel geri çekilme ile yeniden dener (1dk, 5dk, 10dk).
    // ─────────────────────────────────────────────────────────────────
    [AutomaticRetry(Attempts = 3)]
    public async Task FaturaEmailiGonder(int siparisId)
    {
        _logger.LogInformation("[Hangfire] Fatura e-postası gönderiliyor: SiparisId={Id}", siparisId);

        // Gerçek projede: DB'den sipariş detaylarını çek, PDF oluştur, e-posta gönder
        await Task.Delay(300); // simüle

        _logger.LogInformation("[Hangfire] Fatura e-postası gönderildi: SiparisId={Id}", siparisId);
    }

    // ─────────────────────────────────────────────────────────────────
    // Recurring job — her gece 02:00'de stok raporu
    //
    // Program.cs'de kayıt:
    //   RecurringJob.AddOrUpdate<KitabeviJoblar>("stok-raporu",
    //       j => j.GunlukStokRaporu(), "0 2 * * *");
    // ─────────────────────────────────────────────────────────────────
    public async Task GunlukStokRaporu()
    {
        _logger.LogInformation("[Hangfire] Günlük stok raporu oluşturuluyor...");

        var tumKitaplar = _kitapServisi.HepsiniGetir();

        var kritikStok = tumKitaplar.Where(k => k.StokAdedi == 0).ToList();
        var dusukStok  = tumKitaplar.Where(k => k.StokAdedi is > 0 and < 5).ToList();

        _logger.LogInformation(
            "[Hangfire] Stok Raporu | Stoksuz: {Stoksuz} | Düşük Stok: {Dusuk} | Toplam: {Toplam}",
            kritikStok.Count, dusukStok.Count, tumKitaplar.Count);

        // Gerçek projede: raporu PDF'e dönüştür, yöneticiye e-posta gönder
        await Task.Delay(200);

        _logger.LogInformation("[Hangfire] Günlük stok raporu tamamlandı.");
    }

    // ─────────────────────────────────────────────────────────────────
    // Continuation örneği için ayrı servis: KargoServisi
    //
    // Controller'dan kullanımı:
    //   var odemeJobId = BackgroundJob.Enqueue<OdemeJoblar>(j => j.OdemeAl(siparisId));
    //   BackgroundJob.ContinueJobWith<KitabeviJoblar>(odemeJobId, j => j.KargoOlustur(siparisId));
    // ─────────────────────────────────────────────────────────────────
    [AutomaticRetry(Attempts = 2)]
    public async Task KargoOlustur(int siparisId)
    {
        _logger.LogInformation("[Hangfire] Kargo oluşturuluyor: SiparisId={Id}", siparisId);

        // Gerçek projede: kargo şirketinin API'sine istek at, takip no al
        await Task.Delay(400);

        _logger.LogInformation("[Hangfire] Kargo oluşturuldu: SiparisId={Id}", siparisId);
    }
}
