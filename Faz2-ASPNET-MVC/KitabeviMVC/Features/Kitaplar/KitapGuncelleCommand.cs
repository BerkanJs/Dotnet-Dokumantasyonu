using MediatR;
using System.ComponentModel.DataAnnotations;

namespace KitabeviMVC.Features.Kitaplar;

// Gün 35: CQRS — güncelleme command.
// Hem Id (hangi kitap?) hem de güncel alanlar taşınır.
// IRequest<bool>: güncelleme başarılı mı değil mi? — true/false döner.
// Kayıt bulunamazsa: false döner → controller 404 verir.
public record KitapGuncelleCommand(
    int                          Id,
    [Required][StringLength(200)] string Baslik,
    [Required][StringLength(100)] string Yazar,
    [Range(0, 10000)]             decimal Fiyat,
    [StringLength(50)]            string Kategori,
    [Range(0, int.MaxValue)]      int StokAdedi
) : IRequest<bool>;
// IRequest<bool>: KitapEkleCommand'da int (yeni Id) döndürüyorduk.
// Güncelleme için: yeni Id yok, başarı/başarısızlık bilgisi yeterli.
// Unit (= MediatR'ın void'i) yerine bool: çağıran taraf "kayıt bulundu mu?" bilebilir.
