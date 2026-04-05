using KitabeviMVC.Services;

namespace KitabeviMVC.BackgroundServices;

// Gün 25: BackgroundService + PeriodicTimer
//
// Senaryo: Her 10 dakikada bir stok adedi 5'in altına düşmüş kitapları kontrol et,
// tespit edilirse log'a yaz (gerçek projede e-posta veya alarm gönderilir).
//
// Neden BackgroundService + PeriodicTimer?
//   → Kullanıcı tetiklemeli değil, sabit aralıklı
//   → Retry veya persistence gerekmiyor
//   → Harici paket gerektirmiyor — hafif, library-free
public class StokKontrolServisi : BackgroundService
{
    // "ILogger<T>" → hangi sınıftan geldiği belli olan log kaydı
    private readonly ILogger<StokKontrolServisi> _logger;

    // "IServiceScopeFactory" → Scoped servislere Singleton'dan erişmek için.
    // BackgroundService Singleton'dır ama IKitapServisi Scoped olabilir.
    // Doğrudan inject edilirse captive dependency hatası — her döngüde scope aç.
    private readonly IServiceScopeFactory _scopeFactory;

    // Eşik değer — kaç adedinin altı "düşük stok" sayılır?
    private const int DusukStokEsigi = 5;

    public StokKontrolServisi(
        ILogger<StokKontrolServisi> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    // "ExecuteAsync" → BackgroundService'in implement etmemiz gereken tek metodu.
    // "stoppingToken" → host kapanmak istediğinde iptal edilir.
    // Bu metod döndüğünde servis durur.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[StokKontrol] Servis başladı. Kontrol aralığı: 10 dakika.");

        // "PeriodicTimer" → her 10 dakikada bir tick üretir.
        // Task.Delay(600_000) farkı: işin süresi ne olursa olsun periyot kayaz (drift yok).
        // "using var" → servis durunca timer dispose edilir.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));

        // "WaitForNextTickAsync" → bir sonraki tick'e kadar bekle.
        // stoppingToken iptal edilirse false döner → while döngüsünden çıkılır → graceful shutdown.
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await StokKontrolEt(stoppingToken);
        }

        _logger.LogInformation("[StokKontrol] Servis durduruldu.");
    }

    private Task StokKontrolEt(CancellationToken stoppingToken)
    {
        // Her döngüde yeni bir Scoped scope aç → Scoped servislere güvenli erişim.
        // "using" → scope d��ngü bitince dispose edilir, bellek sızıntısı olmaz.
        using var scope = _scopeFactory.CreateScope();
        var kitapServisi = scope.ServiceProvider.GetRequiredService<IKitapServisi>();

        try
        {
            var tumKitaplar = kitapServisi.HepsiniGetir();

            // LINQ: stok eşiğin altında olan kitapları bul
            var dusukStokluKitaplar = tumKitaplar
                .Where(k => k.StokAdedi < DusukStokEsigi)
                .ToList();

            if (dusukStokluKitaplar.Count == 0)
            {
                _logger.LogDebug("[StokKontrol] Tüm kitaplar yeterli stokta.");
                return Task.CompletedTask;
            }

            // Her düşük stoklu kitap için uyarı log'u
            foreach (var kitap in dusukStokluKitaplar)
            {
                _logger.LogWarning(
                    "[StokKontrol] DÜŞÜK STOK: '{Baslik}' (ID={Id}) — {StokAdedi} adet kaldı.",
                    kitap.Baslik, kitap.Id, kitap.StokAdedi);
            }

            // Gerçek projede burada: e-posta gönder, Slack bildirimi at, alarm oluştur
            _logger.LogInformation(
                "[StokKontrol] {Adet} kitapta düşük stok tespit edildi.",
                dusukStokluKitaplar.Count);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // "when (!stoppingToken.IsCancellationRequested)" → normal kapanma hatasını yutma,
            // gerçek hataları yakala ve log'a yaz ama servisi durdurma.
            _logger.LogError(ex, "[StokKontrol] Stok kontrolü sırasında hata oluştu.");
        }

        return Task.CompletedTask;
    }
}
