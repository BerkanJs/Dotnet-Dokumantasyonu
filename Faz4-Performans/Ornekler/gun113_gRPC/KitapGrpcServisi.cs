// GÜN 113 — gRPC ASP.NET Core implementasyonu

using Grpc.Core;
using Ornekler.gun113;

namespace Ornekler.gun113;

// ne yapar: .proto'dan üretilen base class'ı implement eder
// bunu yazmasaydık: gRPC framework hangi kodu çağıracağını bilemezdi
public class KitapGrpcServisiImpl : KitapService.KitapServiceBase
{
    private readonly IKitapRepository _repo;
    private readonly ILogger<KitapGrpcServisiImpl> _logger;

    public KitapGrpcServisiImpl(IKitapRepository repo, ILogger<KitapGrpcServisiImpl> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // --- 1. Unary ---
    public override async Task<KitapYaniti> KitapGetir(
        KitapGetirIstegi istek,
        ServerCallContext context)
    {
        // ne yapar: context.CancellationToken → client iptal ederse işlem durur
        // bunu yazmasaydık: client bağlantısı koptuktan sonra da DB sorgusu devam ederdi
        var kitap = await _repo.GetirAsync(istek.Id, context.CancellationToken);

        if (kitap is null)
        {
            // ne yapar: gRPC status code ile hata döner — HTTP 404 değil
            // bunu yazmasaydık: null exception fırlatır → Status.Internal (500) olurdu
            throw new RpcException(new Status(StatusCode.NotFound, $"Kitap {istek.Id} bulunamadı"));
        }

        return new KitapYaniti
        {
            Id = kitap.Id,
            Ad = kitap.Ad,
            Yazar = kitap.Yazar,
            Fiyat = (double)kitap.Fiyat,
            Stok = kitap.Stok
        };
    }

    // --- 2. Server Streaming ---
    public override async Task KitaplariListele(
        ListeleIstegi istek,
        IServerStreamWriter<KitapYaniti> responseStream,
        ServerCallContext context)
    {
        // ne yapar: sorgu sonuçlarını parça parça akıtır — tüm listeyi belleğe almaz
        // bunu yazmasaydık: 10.000 kitabı tek yanıtta göndermek zorunda kalırdık
        await foreach (var kitap in _repo.ListeleAsync(istek.Kategori, context.CancellationToken))
        {
            // ne yapar: client bağlantısı koptuysa dur
            if (context.CancellationToken.IsCancellationRequested) break;

            await responseStream.WriteAsync(new KitapYaniti
            {
                Id = kitap.Id,
                Ad = kitap.Ad,
                Yazar = kitap.Yazar,
                Fiyat = (double)kitap.Fiyat
            });
        }
    }

    // --- 3. Client Streaming ---
    public override async Task<EkleSonucu> TopluKitapEkle(
        IAsyncStreamReader<KitapEkleIstegi> requestStream,
        ServerCallContext context)
    {
        var eklenenIdler = new List<int>();

        // ne yapar: client'tan gelen stream'i okur — her item'ı ayrı ekler
        // bunu yazmasaydık: tüm kitapları tek request body'ye sığdırmak zorunda kalırdık
        await foreach (var istek in requestStream.ReadAllAsync(context.CancellationToken))
        {
            var id = await _repo.EkleAsync(istek.Ad, istek.Yazar, (decimal)istek.Fiyat);
            eklenenIdler.Add(id);
        }

        var sonuc = new EkleSonucu { EklenenSayisi = eklenenIdler.Count };
        sonuc.OlusturulanIdler.AddRange(eklenenIdler);
        return sonuc;
    }

    // --- 4. Bidirectional Streaming ---
    public override async Task StokGuncelleAkisi(
        IAsyncStreamReader<StokGuncelleIstegi> requestStream,
        IServerStreamWriter<StokSonucu> responseStream,
        ServerCallContext context)
    {
        // ne yapar: istemcinin gönderdiği her stok güncellemesini anında işler ve sonuç akıtır
        // bunu yazmasaydık: her güncelleme için ayrı unary çağrı gerekirdi — N round-trip
        await foreach (var istek in requestStream.ReadAllAsync(context.CancellationToken))
        {
            try
            {
                await _repo.StokGuncelleAsync(istek.KitapId, istek.YeniStok);

                await responseStream.WriteAsync(new StokSonucu
                {
                    KitapId = istek.KitapId,
                    Basarili = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stok güncelleme hatası: {KitapId}", istek.KitapId);
                await responseStream.WriteAsync(new StokSonucu
                {
                    KitapId = istek.KitapId,
                    Basarili = false,
                    HataMesaji = ex.Message
                });
            }
        }
    }
}

// Program.cs:
// builder.Services.AddGrpc(opt =>
// {
//     opt.EnableDetailedErrors = builder.Environment.IsDevelopment();
//     opt.MaxReceiveMessageSize = 4 * 1024 * 1024; // 4 MB
// });
// app.MapGrpcService<KitapGrpcServisiImpl>();

public record KitapDto(int Id, string Ad, string Yazar, decimal Fiyat, int Stok);
public interface IKitapRepository
{
    Task<KitapDto?> GetirAsync(int id, CancellationToken ct);
    IAsyncEnumerable<KitapDto> ListeleAsync(string kategori, CancellationToken ct);
    Task<int> EkleAsync(string ad, string yazar, decimal fiyat);
    Task StokGuncelleAsync(int kitapId, int yeniStok);
}
