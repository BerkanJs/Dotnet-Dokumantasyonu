namespace DipDemo;

// Infrastructure katmanı — EF Core ile gerçek DB işlemleri
// IKitapRepository'yi implement eder → bağımlılık yönü: buradan domain'e
public class EfKitapRepository : IKitapRepository
{
    // Gerçek projede: private readonly KitabeviDbContext _context;
    // Demo'da DbContext simüle ediyoruz — davranış aynı
    private readonly List<string> _db = ["Suç ve Ceza", "Sefiller", "Dune"];

    public List<string> HepsiniGetir()
    {
        Console.WriteLine("[EF→DB] SELECT * FROM Kitaplar");
        return _db;
        // gerçek projede: return await _context.Kitaplar.ToListAsync()
    }

    public void Ekle(string baslik)
    {
        _db.Add(baslik);
        Console.WriteLine($"[EF→DB] INSERT INTO Kitaplar ('{baslik}')");
        // gerçek projede: _context.Add(...); await _context.SaveChangesAsync()
    }
}
