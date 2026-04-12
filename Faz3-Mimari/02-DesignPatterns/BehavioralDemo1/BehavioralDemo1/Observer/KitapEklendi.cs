namespace BehavioralDemo1.Observer;

// Observer Pattern: Yayıncı (Publisher) → Abone (Subscriber)
// Yayıncı aboneleri tanımıyor — sadece event fırlatıyor
// Yeni abone = yeni class, KitapDepo'ya dokunulmaz (OCP)

public class KitapEklendiEventArgs : EventArgs
{
    public string Baslik { get; init; } = string.Empty;
    public decimal Fiyat { get; init; }
}

// Publisher: kitap eklenince event fırlatıyor
public class KitapDepo
{
    // C# event keyword — Observer pattern'in dil seviyesinde uygulaması
    public event EventHandler<KitapEklendiEventArgs>? KitapEklendi;
    // bunu yazmasaydık → KitapDepo içinde bildirim kodu olurdu (SRP ihlali)
    // kim abone olduğunu bilmek zorunda kalırdı (tight coupling)

    public void Ekle(string baslik, decimal fiyat)
    {
        Console.WriteLine($"[DEPO] '{baslik}' eklendi");

        KitapEklendi?.Invoke(this, new KitapEklendiEventArgs
        {
            Baslik = baslik,
            Fiyat  = fiyat
        });
        // bunu yazmasaydık → aboneler değişiklikten haberdar olamazdı
    }
}

// Subscriber 1: e-posta bildirimi
public class EmailBildirimServisi
{
    public void Abone(KitapDepo depo)
    {
        depo.KitapEklendi += (_, e) =>
            Console.WriteLine($"[EMAIL] Yeni kitap: '{e.Baslik}' ({e.Fiyat:C0})");
        // lambda ile abone olundu — depo EmailBildirimServisi'ni bilmiyor
    }
}

// Subscriber 2: stok uyarısı
public class StokUyariServisi
{
    public void Abone(KitapDepo depo)
    {
        depo.KitapEklendi += (_, e) =>
        {
            if (e.Fiyat > 200)
                Console.WriteLine($"[STOK] Yüksek fiyatlı kitap: '{e.Baslik}'");
        };
        // yeni subscriber = bu class — KitapDepo'ya dokunulmadı
    }
}
