namespace StructuralDemo.Decorator;

public interface IKitapServisi
{
    List<string> HepsiniGetir();
    string? BulById(int id);
}

// Gerçek implementasyon — DB'ye gider
public class EfKitapServisi : IKitapServisi
{
    private readonly List<string> _db = ["Suç ve Ceza", "Sefiller", "Dune"];

    public List<string> HepsiniGetir()
    {
        Console.WriteLine("[EF→DB] SELECT * FROM Kitaplar");
        return _db;
    }

    public string? BulById(int id) => id < _db.Count ? _db[id] : null;
}
