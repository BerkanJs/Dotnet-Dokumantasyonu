namespace BehavioralDemo1.Strategy;

public interface ISiralama
{
    List<string> Sirala(List<string> kitaplar);
    string Ad { get; }
}

public class AdaSirala : ISiralama
{
    public string Ad => "Ada Göre (A-Z)";
    public List<string> Sirala(List<string> kitaplar)
        => kitaplar.OrderBy(k => k).ToList();
}

public class TersAdaSirala : ISiralama
{
    public string Ad => "Ada Göre (Z-A)";
    public List<string> Sirala(List<string> kitaplar)
        => kitaplar.OrderByDescending(k => k).ToList();
}

// Yeni sıralama = yeni class — KitapListeServisi'ne dokunulmaz (OCP)
public class RastgeleSirala : ISiralama
{
    public string Ad => "Rastgele";
    public List<string> Sirala(List<string> kitaplar)
        => kitaplar.OrderBy(_ => Random.Shared.Next()).ToList();
}
