using MediatR;
using System.ComponentModel.DataAnnotations;

namespace KitabeviMVC.Features.Kitaplar;

// Gün 35: CQRS — yazma tarafı.
// IRequest<int>: işlem tamamlandığında yeni kitabın Id'si döner.
// record: primary constructor → ASP.NET Core model binding .NET 8+ ile çalışır.
public record KitapEkleCommand(
    [Required][StringLength(200)] string Baslik,
    [Required][StringLength(100)] string Yazar,
    [Range(0, 10000)]             decimal Fiyat,
    [StringLength(50)]            string Kategori,
    [Range(0, int.MaxValue)]      int StokAdedi
) : IRequest<int>;
// IRequest<Unit> yazmak da mümkün — Unit = MediatR'ın void'i (sonuç dönmeyecekse)
// int seçtik: controller redirect için yeni Id'yi kullanacak
