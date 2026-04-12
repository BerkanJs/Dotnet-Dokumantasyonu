namespace BehavioralDemo1.Strategy;

// Strategy runtime'da değiştirilebilir — constructor yerine metod parametresi
// bunu yazmasaydık → sıralama tipi için if/switch zinciri (OCP ihlali)
public class KitapListeServisi
{
    private readonly List<string> _kitaplar =
        ["Sefiller", "Dune", "Suç ve Ceza", "1984", "Cesur Yeni Dünya"];

    public List<string> Listele(ISiralama siralama)
    {
        Console.WriteLine($"[{siralama.Ad}]");
        return siralama.Sirala(_kitaplar);
        // hangi algoritmanın çalışacağını bilmiyoruz — polymorphism hallediyor
    }
}
