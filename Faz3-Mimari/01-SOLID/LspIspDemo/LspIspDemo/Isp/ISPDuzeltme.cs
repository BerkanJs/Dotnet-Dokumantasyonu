namespace LspIspDemo.Isp;

// ✅ ISP Düzeltme: Faz2'nin yaptığı tam olarak bu
// IKitapServisi + IKitapSorguServisi + IKitapBatchServisi = ayrı ayrı interface

public interface IKitapOkuma
{
    List<string> HepsiniGetir();
    string? BulById(int id);
    // CachedKitapServisi sadece bunu implement eder — gereksiz metod yok
}

public interface IKitapYazma
{
    void Ekle(string baslik);
    void Sil(int id);
    // sadece yazma operasyonlarına ihtiyacı olan class bunu alır
}

public interface IKitapBatch
{
    int TopluSil(string kategori);
    int StokSifirla(string kategori);
    // sadece EfKitapServisi implement eder
    // CachedKitapServisi bu interface'i hiç bilmez
}

// CachedKitapServisi — sadece ihtiyacı olan interface'leri alır
public class CachedKitapServisi : IKitapOkuma
{
    private readonly IKitapOkuma _gercekServis;
    // bunu yazmasaydık → cache servisi toplu operasyonları da implement etmek zorunda kalırdı

    public CachedKitapServisi(IKitapOkuma gercekServis) => _gercekServis = gercekServis;

    public List<string> HepsiniGetir()
    {
        // Gerçek projede cache kontrolü + fallback
        Console.WriteLine("[CACHE] HepsiniGetir");
        return _gercekServis.HepsiniGetir();
    }

    public string? BulById(int id)
    {
        Console.WriteLine($"[CACHE] BulById({id})");
        return _gercekServis.BulById(id);
    }
}

// EfKitapServisi — tüm interface'leri implement eder
public class EfKitapServisi : IKitapOkuma, IKitapYazma, IKitapBatch
{
    private readonly List<string> _db = ["Suç ve Ceza", "Sefiller", "Dune"];
    // demo için in-memory liste

    public List<string> HepsiniGetir() => _db;
    public string? BulById(int id) => id < _db.Count ? _db[id] : null;
    public void Ekle(string baslik) => _db.Add(baslik);
    public void Sil(int id) => _db.RemoveAt(id);
    public int TopluSil(string kategori) { Console.WriteLine($"[DB] TopluSil: {kategori}"); return 0; }
    public int StokSifirla(string kategori) { Console.WriteLine($"[DB] StokSifirla: {kategori}"); return 0; }
}
