using System.ComponentModel.DataAnnotations;

namespace KitabeviMVC.Models.ViewModels;

// Liste görünümü için
public record KitapListeViewModel(int Id, string Baslik, string Yazar, decimal Fiyat, string Kategori, int StokAdedi);

// ─────────────────────────────────────────────────────────────────────
// Gün 30: Detay sayfası için ViewModel — Include + Projection birlikte.
//
// KitapFormViewModel'den farkı: YazarAdi ve KategoriOneriler alanları var.
// Bu alanlar SQL'deki JOIN ve subquery'den projection ile doldurulur.
// EF Core bu ViewModel'i doğrudan SQL sonucuna eşler — ayrı mapping yok.
// ─────────────────────────────────────────────────────────────────────
public class KitapDetayViewModel
{
    public int Id { get; set; }
    public string Baslik { get; set; } = string.Empty;
    public string Yazar { get; set; } = string.Empty;   // entity'deki string alan
    public string? YazarAdi { get; set; }               // navigation'dan gelen (JOIN)
    public decimal Fiyat { get; set; }
    public string Kategori { get; set; } = string.Empty;
    public int StokAdedi { get; set; }
    public DateTime EklemeTarihi { get; set; }

    // Aynı kategorideki öneriler — ayrı sorgudan gelir (N+1 önleme gösterimi)
    public IReadOnlyList<KitapListeViewModel> KategoriOneriler { get; set; } = [];
}

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

    // ─────────────────────────────────────────────────────────────────────
    // Gün 33: Optimistic Concurrency — formdaki RowVersion değeri.
    //
    // Akış:
    //   1. Duzenle GET → BulByIdAsync() → RowVersion DB'den gelir → view'a geçer
    //   2. View'da: <input type="hidden" asp-for="RowVersion" />
    //   3. Duzenle POST → model.RowVersion → GuncelleAsync()'e gider
    //   4. EfKitapServisi: .OriginalValue = model.RowVersion → WHERE RowVersion = ?
    //
    // nullable: Ekle senaryosunda RowVersion henüz yoktur; null = concurrency kontrolü yapma.
    // ─────────────────────────────────────────────────────────────────────
    public byte[]? RowVersion { get; set; }
}
