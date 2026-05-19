// GÜN 110 — HybridCache (.NET 9)
// L1: in-process IMemoryCache (nanosaniye)
// L2: distributed Redis (milisaniye)
// Cache stampede koruması: aynı key için sadece bir kez DB'ye git

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Ornekler.gun110;

// --- 1. Program.cs kurulum ---
public static class HybridCacheSetup
{
    public static void Kaydet(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder)
    {
        // ne yapar: L1 (memory) + L2 (Redis) otomatik birleşik cache
        // bunu yazmasaydık: IMemoryCache + IDistributedCache'i elle koordine etmek zorunda kalırdık
        builder.Services.AddHybridCache(opt =>
        {
            opt.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                // ne yapar: L1 cache 30 saniye, L2 cache 10 dakika
                // bunu yazmasaydık: her L2 miss anında DB'ye giderdik
                LocalCacheExpiration = TimeSpan.FromSeconds(30),
                Expiration = TimeSpan.FromMinutes(10)
            };

            // ne yapar: maksimum cache item boyutu — büyük nesneler L2'ye sığmaz
            opt.MaximumPayloadBytes = 1024 * 1024; // 1 MB
        });

        // L2 olarak Redis ekle
        builder.Services.AddStackExchangeRedisCache(opt =>
        {
            opt.Configuration = "localhost:6379";
            opt.InstanceName = "Kitabevi:";
        });
    }
}

// --- 2. Kullanım ---
public class KitapServisi
{
    private readonly HybridCache _cache;
    private readonly IKitapRepository _repo;

    public KitapServisi(HybridCache cache, IKitapRepository repo)
    {
        _cache = cache;
        _repo = repo;
    }

    public async Task<Kitap?> GetirAsync(int id, CancellationToken ct = default)
    {
        // ne yapar: L1 → miss → L2 → miss → DB → L1 + L2'ye yaz
        // stampede koruması: 1000 istek aynı anda gelirse sadece 1 tanesi DB'ye gider
        // bunu yazmasaydık: IDistributedCache ile GetOrCreateAsync'ı elle yazmak zorunda kalırdık
        return await _cache.GetOrCreateAsync(
            $"kitap:{id}",
            async (ct) => await _repo.GetirAsync(id, ct),
            new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromSeconds(30),
                Expiration = TimeSpan.FromMinutes(10)
            },
            // ne yapar: bu tag'ler ile cache entry'yi toplu invalidate edebilirsin
            // bunu yazmasaydık: kitap güncellenince hangi cache key'lerini temizleyeceğini bilmezdik
            tags: new[] { "kitap", $"kitap:{id}" },
            cancellationToken: ct);
    }

    public async Task GuncelleAsync(int id, Kitap kitap, CancellationToken ct = default)
    {
        await _repo.GuncelleAsync(kitap, ct);

        // ne yapar: "kitap:42" tag'ine sahip tüm cache entry'leri siler
        // bunu yazmasaydık: eski veri TTL dolana kadar dönmeye devam ederdi
        await _cache.RemoveByTagAsync($"kitap:{id}", ct);
    }

    // IMemoryCache vs IDistributedCache vs HybridCache karşılaştırması:
    // IMemoryCache:     L1 only, pod restart = cache sıfırlanır, çok pod = tutarsız
    // IDistributedCache: L2 only, her erişimde network, serialize/deserialize maliyeti
    // HybridCache:      L1+L2, stampede koruması, tag invalidation — en iyi birleşim
}

public record Kitap(int Id, string Ad, decimal Fiyat);
public interface IKitapRepository
{
    Task<Kitap?> GetirAsync(int id, CancellationToken ct);
    Task GuncelleAsync(Kitap kitap, CancellationToken ct);
}
