namespace StructuralDemo.Adapter;

// Dış tedarikçinin kendi API'si — bizim interface'imize uymayan isimler
// Değiştiremeyiz: dışarıdan geliyor (NuGet paketi, 3. parti servis vs.)
public class DisTedarikciApi
{
    public decimal GetPrice(string isbn)
    {
        Console.WriteLine($"[DIŞ API] GetPrice({isbn}) çağrıldı");
        return 95.00m;  // simüle edilmiş fiyat
    }

    public bool CheckAvailability(string isbn)
    {
        Console.WriteLine($"[DIŞ API] CheckAvailability({isbn}) çağrıldı");
        return true;
    }
}
