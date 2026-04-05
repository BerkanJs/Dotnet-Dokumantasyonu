using System.ComponentModel.DataAnnotations;

namespace KitabeviMVC.Models.ViewModels;

// Giriş formu için ViewModel.
// DataAnnotation'lar hem server-side hem client-side validation sağlar.
public class GirisViewModel
{
    [Required(ErrorMessage = "E-posta zorunludur")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin")]
    public string Eposta { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur")]
    [DataType(DataType.Password)] // View'da <input type="password"> üretir
    public string Sifre { get; set; } = string.Empty;

    // Giriş başarılıysa nereye dön?
    // ReturnUrl → [Authorize] ile engellenen sayfanın adresi.
    // Örn: /kitaplar/ekle'ye gitmeye çalışınca login sayfasına atıldıysan,
    // giriş sonrası tekrar /kitaplar/ekle'ye dönmen için saklanır.
    public string? ReturnUrl { get; set; }
}
