// GÜN 116 — SignalR Hub ve Groups
// Hub: client ↔ server gerçek zamanlı iletişim merkezi
// Groups: mantıksal kanallar — sadece ilgili kullanıcılara mesaj gönder

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Ornekler.gun116;

// --- 1. Strongly-typed hub interface ---
// ne yapar: istemciye gönderilecek metodları tip güvenli tanımlar
// bunu yazmasaydık: Clients.All.SendAsync("MetodAdi") string hatalarına açık olurdu
public interface IBorsaClient
{
    Task FiyatGuncellendi(string sembol, decimal fiyat, DateTime zaman);
    Task HisseEklendi(string sembol);
    Task HisseKaldirildi(string sembol);
}

// --- 2. Hub implementasyonu ---
[Authorize]  // ne yapar: hub'a bağlanmak için authentication zorunlu
public class BorsaHub : Hub<IBorsaClient>
{
    private readonly ILogger<BorsaHub> _logger;

    public BorsaHub(ILogger<BorsaHub> logger) => _logger = logger;

    // --- Connection lifecycle ---
    public override async Task OnConnectedAsync()
    {
        // ne yapar: kullanıcı bağlandığında welcome mesajı gönder
        // bunu yazmasaydık: bağlanma olayını yakalayamazdık
        _logger.LogInformation("Bağlantı: {ConnectionId} / {UserId}",
            Context.ConnectionId, Context.UserIdentifier);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // ne yapar: bağlantı kopunca temizlik yap
        // bunu yazmasaydık: bağlantısı kopan kullanıcı gruplarda kalırdı
        _logger.LogInformation("Bağlantı kesildi: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // --- Client → Server metodlar ---
    public async Task HisseTakipEt(string sembol)
    {
        // ne yapar: kullanıcıyı bu hissenin grubuna ekler
        // bunu yazmasaydık: tüm kullanıcılara tüm hisse fiyatları gönderilirdi — verimsiz
        await Groups.AddToGroupAsync(Context.ConnectionId, $"hisse_{sembol}");
        await Clients.Caller.HisseEklendi(sembol);

        _logger.LogInformation("{UserId} → {Sembol} takip başladı",
            Context.UserIdentifier, sembol);
    }

    public async Task HisseTakipBirak(string sembol)
    {
        // ne yapar: kullanıcıyı gruptan çıkarır — artık o hissenin fiyatlarını almaz
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"hisse_{sembol}");
        await Clients.Caller.HisseKaldirildi(sembol);
    }

    // --- Clients targets ---
    // Clients.All          → tüm bağlı istemciler
    // Clients.Caller       → sadece isteği gönderen
    // Clients.Others       → gönderen hariç hepsi
    // Clients.User(userId) → belirli kullanıcının tüm bağlantıları (telefon + tablet)
    // Clients.Group(name)  → gruptaki herkes
    // Clients.GroupExcept  → gruptan belirli bağlantıları hariç tut
}

// --- 3. Background service: fiyat yayını ---
public class FiyatYayinServisi : BackgroundService
{
    // ne yapar: Hub dışından Hub metodlarını çağırmayı sağlar
    // bunu yazmasaydık: sadece Hub sınıfı içinden mesaj gönderebilirdik
    private readonly IHubContext<BorsaHub, IBorsaClient> _hubContext;

    public FiyatYayinServisi(IHubContext<BorsaHub, IBorsaClient> hubContext)
        => _hubContext = hubContext;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Her hisse için fiyat güncelle
            var fiyatlar = new Dictionary<string, decimal>
            {
                ["THYAO"] = 150 + (decimal)(new Random().NextDouble() * 10),
                ["ASELS"] = 45 + (decimal)(new Random().NextDouble() * 3),
                ["SASA"] = 80 + (decimal)(new Random().NextDouble() * 5)
            };

            foreach (var (sembol, fiyat) in fiyatlar)
            {
                // ne yapar: sadece bu hisseyi takip eden kullanıcılara gönderir
                // bunu yazmasaydık: Clients.All → tüm kullanıcılar tüm fiyatları alırdı
                await _hubContext.Clients
                    .Group($"hisse_{sembol}")
                    .FiyatGuncellendi(sembol, fiyat, DateTime.UtcNow);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}

// Program.cs:
// builder.Services.AddSignalR(opt =>
// {
//     opt.EnableDetailedErrors = builder.Environment.IsDevelopment();
//     opt.KeepAliveInterval = TimeSpan.FromSeconds(15);
// });
// builder.Services.AddHostedService<FiyatYayinServisi>();
// app.MapHub<BorsaHub>("/hubs/borsa");

// JavaScript client:
// const connection = new signalR.HubConnectionBuilder()
//     .withUrl("/hubs/borsa", { accessTokenFactory: () => localStorage.getItem("token") })
//     .withAutomaticReconnect()
//     .build();
//
// connection.on("FiyatGuncellendi", (sembol, fiyat, zaman) => { ... });
// await connection.start();
// await connection.invoke("HisseTakipEt", "THYAO");
