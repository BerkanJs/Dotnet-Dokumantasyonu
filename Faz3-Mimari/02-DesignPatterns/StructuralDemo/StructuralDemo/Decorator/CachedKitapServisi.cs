namespace StructuralDemo.Decorator;

// Decorator Pattern: EfKitapServisi'ni SARMALIYOR
// EfKitapServisi'ne dokunmadan caching davranışı eklendi (OCP)
// Controller CachedKitapServisi mi EfKitapServisi mi geldiğini bilmiyor (LSP)
public class CachedKitapServisi : IKitapServisi
{
    private readonly IKitapServisi _gercekServis;
    // bunu yazmasaydık → EfKitapServisi'ne direkt bağımlı olurduk
    // yarın LoggingKitapServisi eklemek istersen aynı şekilde sar

    private List<string>? _cache;

    public CachedKitapServisi(IKitapServisi gercekServis)
        => _gercekServis = gercekServis;

    public List<string> HepsiniGetir()
    {
        if (_cache is not null)
        {
            Console.WriteLine("[CACHE] HepsiniGetir → cache'den döndü");
            return _cache;
            // bunu yazmasaydık → her çağrıda DB'ye gidilirdi
        }

        _cache = _gercekServis.HepsiniGetir();  // gerçek servise delege et
        return _cache;
    }

    public string? BulById(int id) => _gercekServis.BulById(id);
    // cache'e almaya değmez — tek kayıt, kısa yaşam
}
