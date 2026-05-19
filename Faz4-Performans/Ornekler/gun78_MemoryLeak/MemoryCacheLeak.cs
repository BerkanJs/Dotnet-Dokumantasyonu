// GÜN 78 — Memory Leak: IMemoryCache Sınır Yok
// Cache'e boyut sınırı konmazsa sınırsız büyür → OutOfMemoryException

using Microsoft.Extensions.Caching.Memory;

namespace Ornekler.gun78;

// YANLIŞ: boyut limiti yok
public class YanlisCache
{
    private readonly IMemoryCache _cache;

    public YanlisCache(IMemoryCache cache) => _cache = cache;

    public void Ekle(string key, object deger)
    {
        // ne yapar: cache'e item ekler
        // SORUN: SizeLimit tanımlanmamışsa cache sınırsız büyür
        // bunu yazmasaydık: cache'i hiç kullanamayacaktık
        _cache.Set(key, deger);
    }
}

// DOĞRU: TTL + boyut sınırı
public class DogruCache
{
    private readonly IMemoryCache _cache;

    public DogruCache(IMemoryCache cache) => _cache = cache;

    public void Ekle(string key, object deger)
    {
        var options = new MemoryCacheEntryOptions
        {
            // ne yapar: item 5 dakika sonra otomatik silinir
            // bunu yazmasaydık: eski veriler sonsuza kadar bellekte kalırdı
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),

            // ne yapar: 10 dakika erişilmezse sil — aktif kullanım devam ederse tutulsun
            // bunu yazmasaydık: sık kullanılan item'lar da 5 dakikada silinirdi
            SlidingExpiration = TimeSpan.FromMinutes(10),

            // ne yapar: SizeLimit ile birlikte kullanılır — her item'ın "ağırlığını" belirtir
            // bunu yazmasaydık: SizeLimit'in anlamı olmazdı
            Size = 1
        };

        _cache.Set(key, deger, options);
    }
}

// Program.cs'de SizeLimit tanımı:
// builder.Services.AddMemoryCache(opt => opt.SizeLimit = 1024);
// → toplamda 1024 birimlik item tutulabilir, dolunca LRU çıkarma başlar
