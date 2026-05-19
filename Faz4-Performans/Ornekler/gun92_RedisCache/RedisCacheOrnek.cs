// GÜN 92 — Redis Distributed Cache
// IDistributedCache: soyutlama — Redis, SQL Server, NCache arkasında çalışır
// StackExchange.Redis: doğrudan Redis client

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Text.Json;

namespace Ornekler.gun92;

// --- 1. IDistributedCache ile Cache-Aside pattern ---
public class KitapCacheServisi
{
    private readonly IDistributedCache _cache;
    private readonly IKitapRepository _repo;

    public KitapCacheServisi(IDistributedCache cache, IKitapRepository repo)
    {
        _cache = cache;
        _repo = repo;
    }

    public async Task<Kitap?> GetirAsync(int id, CancellationToken ct = default)
    {
        string cacheKey = $"kitap:{id}";

        // ne yapar: önce cache'e bak
        // bunu yazmasaydık: her seferinde veritabanına giderdik
        var cachedBytes = await _cache.GetAsync(cacheKey, ct);

        if (cachedBytes is not null)
            return JsonSerializer.Deserialize<Kitap>(cachedBytes);

        // Cache miss — veritabanından getir
        var kitap = await _repo.GetirAsync(id, ct);
        if (kitap is null) return null;

        var options = new DistributedCacheEntryOptions
        {
            // ne yapar: 10 dakika sonra kesin sil
            // bunu yazmasaydık: eski veri sonsuza kadar dönebilirdi
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),

            // ne yapar: 2 dakika erişilmezse sil — aktif kullanımda tutar
            // bunu yazmasaydık: az kullanılan kitaplar da 10 dakika cache'de kalırdı
            SlidingExpiration = TimeSpan.FromMinutes(2)
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(kitap);

        // ne yapar: veriyi Redis'e kaydeder
        // bunu yazmasaydık: bir sonraki istekte yine DB'ye giderdik
        await _cache.SetAsync(cacheKey, bytes, options, ct);

        return kitap;
    }

    public async Task InvalidateAsync(int id, CancellationToken ct = default)
    {
        // ne yapar: kitap güncellendiğinde cache'i temizle
        // bunu yazmasaydık: eski veri TTL dolana kadar dönmeye devam ederdi
        await _cache.RemoveAsync($"kitap:{id}", ct);
    }
}

// --- 2. StackExchange.Redis — doğrudan Redis komutları ---
public class RedisDirectOrnek
{
    private readonly IDatabase _db;

    public RedisDirectOrnek(IConnectionMultiplexer redis)
    {
        // ne yapar: Redis veritabanı 0'a bağlantı alır
        // bunu yazmasaydık: her işlemde yeni bağlantı açardık — pahalı
        _db = redis.GetDatabase();
    }

    // Pub/Sub
    public async Task MesajYayinla(string kanal, string mesaj)
    {
        var pub = _db.Multiplexer.GetSubscriber();

        // ne yapar: kanala abone olan tüm servisler bu mesajı alır
        // bunu yazmasaydık: servisler arası event yayını için RabbitMQ gerekirdi
        await pub.PublishAsync(RedisChannel.Literal(kanal), mesaj);
    }

    public async Task KanalaAbone(string kanal, Action<string> handler)
    {
        var sub = _db.Multiplexer.GetSubscriber();

        // ne yapar: kanala abone ol — mesaj gelince handler çağır
        // bunu yazmasaydık: cache invalidation event'i diğer pod'lara iletemezdik
        await sub.SubscribeAsync(RedisChannel.Literal(kanal), (_, value) =>
        {
            if (value.HasValue) handler(value!);
        });
    }

    // Atomic increment — sayaç
    public async Task<long> ZiyaretciSayisiArtir(string sayfaId)
    {
        // ne yapar: Redis'te atomik artırma — race condition yok
        // bunu yazmasaydık: iki pod aynı anda artırırsa kayıp olurdu
        return await _db.StringIncrementAsync($"sayac:{sayfaId}");
    }

    // Distributed Lock (RedLock benzeri basit versiyon)
    public async Task<bool> KilitAl(string kaynak, TimeSpan sure)
    {
        // ne yapar: "lock:kaynak" anahtarını NX (yoksa koy) + EX (expire) ile set et
        // bunu yazmasaydık: iki pod aynı kaynağa aynı anda yazabilirdi
        return await _db.StringSetAsync(
            $"lock:{kaynak}",
            Environment.MachineName,
            sure,
            When.NotExists);    // sadece anahtar yoksa set et — atomic
    }

    public async Task KilitBirak(string kaynak)
    {
        await _db.KeyDeleteAsync($"lock:{kaynak}");
    }
}

// Program.cs:
// builder.Services.AddStackExchangeRedisCache(opt =>
// {
//     opt.Configuration = builder.Configuration.GetConnectionString("Redis");
//     opt.InstanceName = "Kitabevi:";
// });
// builder.Services.AddSingleton<IConnectionMultiplexer>(
//     ConnectionMultiplexer.Connect("localhost:6379"));

public record Kitap(int Id, string Ad, decimal Fiyat);
public interface IKitapRepository
{
    Task<Kitap?> GetirAsync(int id, CancellationToken ct);
}
