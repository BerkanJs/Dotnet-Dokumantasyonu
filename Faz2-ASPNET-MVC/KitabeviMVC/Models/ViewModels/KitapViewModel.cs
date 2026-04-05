using System.ComponentModel.DataAnnotations;

namespace KitabeviMVC.Models.ViewModels;

// Liste görünümü için
public record KitapListeViewModel(int Id, string Baslik, string Yazar, decimal Fiyat, string Kategori, int StokAdedi);

// Detay ve form için
public class KitapFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Başlık zorunludur")]
    [StringLength(200)]
    public string Baslik { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yazar zorunludur")]
    public string Yazar { get; set; } = string.Empty;

    [Range(0, 10000, ErrorMessage = "Fiyat 0-10000 arasında olmalıdır")]
    public decimal Fiyat { get; set; }

    public string Kategori { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int StokAdedi { get; set; }

    // Gün 20: Resource-based authorization için — kitabı kim ekledi?
    // Gerçek projede bu alan DB'den gelir. Burada KitapServisi dolduruyor.
    public string? EkleyenKullanici { get; set; }
}
