namespace KitabeviMVC.BackgroundServices;

// Gün 25: BackgroundService — Channel consumer.
//
// SiparisOnayKanali'ndan mesaj okur, onay e-postası gönderir.
// HTTP request döngüsünden tamamen bağımsız çalışır.
public class SiparisOnayServisi : BackgroundService
{
    private readonly SiparisOnayKanali _kanal;
    private readonly ILogger<SiparisOnayServisi> _logger;

    public SiparisOnayServisi(
        SiparisOnayKanali kanal,
        ILogger<SiparisOnayServisi> logger)
    {
        _kanal = kanal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SiparisOnay] E-posta servisi başladı, kanal dinleniyor.");

        // "ReadAllAsync" → kanal kapanana veya token iptal edilene kadar sürekli okur.
        // Her mesaj geldiğinde döngü devam eder — mesaj yoksa otomatik bekler (polling yok).
        await foreach (var mesaj in _kanal.Okuyucu.ReadAllAsync(stoppingToken))
        {
            await OnayEmailiGonder(mesaj, stoppingToken);
        }

        _logger.LogInformation("[SiparisOnay] E-posta servisi durduruldu.");
    }

    private async Task OnayEmailiGonder(SiparisOnayMesaji mesaj, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation(
                "[SiparisOnay] E-posta gönderiliyor: SiparisId={SiparisId} → {Eposta}",
                mesaj.SiparisId, mesaj.MusteriEposta);

            // Gerçek projede: SmtpClient, SendGrid, AWS SES vb.
            // stoppingToken geçilir — kapanma sinyalinde e-posta gönderimi iptal edilebilir.
            await Task.Delay(500, stoppingToken); // e-posta gönderimini simüle eder

            _logger.LogInformation(
                "[SiparisOnay] E-posta gönderildi: SiparisId={SiparisId} | " +
                "Kitap='{Kitap}' | Tutar={Tutar:C2}",
                mesaj.SiparisId, mesaj.KitapBaslik, mesaj.ToplamTutar);
        }
        catch (OperationCanceledException)
        {
            // Host kapanıyor — mesajı işleyemedik, log'a yaz ve çık
            _logger.LogWarning(
                "[SiparisOnay] Kapanma sinyali geldi, SiparisId={SiparisId} e-postası gönderilemedi.",
                mesaj.SiparisId);
        }
        catch (Exception ex)
        {
            // E-posta gönderilemedi ama servisi durdurma — sonraki mesaja geç.
            // Gerçek projede: başarısız mesajı dead-letter kuyruğuna taşı veya Hangfire'a ver.
            _logger.LogError(ex,
                "[SiparisOnay] E-posta gönderilemedi: SiparisId={SiparisId}",
                mesaj.SiparisId);
        }
    }
}
