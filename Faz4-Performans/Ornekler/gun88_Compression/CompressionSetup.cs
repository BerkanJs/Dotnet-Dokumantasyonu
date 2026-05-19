// GÜN 88 — Response Compression, HTTP Caching, Output Cache
// Program.cs kurulumu ve middleware kullanımı

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;

namespace Ornekler.gun88;

public static class CompressionSetup
{
    public static void KaydetServisleri(WebApplicationBuilder builder)
    {
        // --- Response Compression ---
        builder.Services.AddResponseCompression(opt =>
        {
            // ne yapar: HTTPS üzerinde de sıkıştırma açar
            // bunu yazmasaydık: HTTPS'de compression kapalı kalırdı (CRIME attack riski vardı, artık mitigation var)
            opt.EnableForHttps = true;

            // ne yapar: Brotli ve Gzip provider'larını aktif eder
            // bunu yazmasaydık: sıkıştırma için provider tanımlanmazdı
            opt.Providers.Add<BrotliCompressionProvider>();
            opt.Providers.Add<GzipCompressionProvider>();

            // ne yapar: bu MIME type'ları sıkıştırılacak
            // bunu yazmasaydık: sadece text/html ve benzer varsayılanlar sıkıştırılırdı
            opt.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/json",
                "application/javascript",
                "text/css"
            });
        });

        builder.Services.Configure<BrotliCompressionProviderOptions>(opt =>
        {
            // ne yapar: sıkıştırma seviyesi — Optimal: iyi sıkıştırma, SmallestSize: max sıkıştırma ama yavaş
            // bunu yazmasaydık: varsayılan seviye (Fastest) daha az sıkıştırırdı
            opt.Level = System.IO.Compression.CompressionLevel.Optimal;
        });

        // --- Output Cache (ASP.NET Core 7+) ---
        builder.Services.AddOutputCache(opt =>
        {
            // ne yapar: "kitaplar" adlı policy — 60 saniye cache
            // bunu yazmasaydık: her endpoint için ayrı ayrı süre yazardık
            opt.AddPolicy("kitaplar", p => p
                .Expire(TimeSpan.FromSeconds(60))
                .SetVaryByQuery("kategori", "sayfa")    // query string'e göre farklı cache
                .Tag("kitap-listesi"));                  // tag ile toplu invalidate için
        });
    }

    public static void KonfigureMiddleware(WebApplication app)
    {
        // ne yapar: gelen istekleri sıkıştırılmış response ile yanıtlar
        // bunu yazmasaydık: JSON yanıtlar sıkıştırılmaz, band genişliği harcanırdı
        // SIRALAMA ÖNEMLİ: UseResponseCompression → UseRouting → endpoint'ler
        app.UseResponseCompression();

        // ne yapar: OutputCache middleware'ini ekler
        // bunu yazmasaydık: [OutputCache] attribute çalışmazdı
        app.UseOutputCache();
    }
}

// Endpoint kullanımı:
// app.MapGet("/kitaplar", async (KitapService servis) =>
// {
//     return await servis.TumunuGetirAsync();
// })
// .CacheOutput("kitaplar");  // ne yapar: bu endpoint'i "kitaplar" policy ile cache'le

// Cache invalidate:
// app.MapPost("/kitaplar", async (IOutputCacheStore store, KitapService servis, Kitap kitap) =>
// {
//     await servis.EkleAsync(kitap);
//     await store.EvictByTagAsync("kitap-listesi", CancellationToken.None);
//     // ne yapar: tag'li tüm cache entry'lerini siler — liste güncellendi
// });
