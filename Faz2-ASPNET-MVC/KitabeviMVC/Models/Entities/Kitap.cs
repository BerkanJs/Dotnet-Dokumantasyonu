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
}
