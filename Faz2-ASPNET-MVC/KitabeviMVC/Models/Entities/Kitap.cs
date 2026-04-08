namespace KitabeviMVC.Models.Entities;

public class Kitap
{
    public int Id { get; set; }
    public string Baslik { get; set; } = string.Empty;
    public string Yazar { get; set; } = string.Empty;
    public decimal Fiyat { get; set; }
    public string Kategori { get; set; } = string.Empty;
    public int StokAdedi { get; set; }
    public DateTime EklemeTarihi { get; set; } = DateTime.UtcNow;

    // Gün 29: Foreign Key — YazarId null olabilir (optional relationship).
    // Mevcut seed verisi YazarId içermiyor; nullable yaparak geriye dönük uyumluluk sağlanır.
    public int? YazarId { get; set; }

    // Navigation property — EF Core bunu YazarId üzerinden JOIN ile çözer.
    // "?" → YazarId null olduğunda bu property de null gelir (optional).
    public Yazar? YazarNavigation { get; set; }
}
