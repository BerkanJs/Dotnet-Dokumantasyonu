using KitabeviMVC.Models.ViewModels;

namespace KitabeviMVC.Services;

// Gün 29: Arayüz async'e çevrildi.
//
// Önceki hali (Gün 18): senkron — in-memory liste için yeterliydi.
// Yeni hali (Gün 29):   async   — EF Core, I/O bound işlemler için async zorunlu.
//
// Task<T>: I/O tamamlanana kadar thread serbest kalır.
// "Async" suffix: .NET naming convention — async metod olduğunu bildirir.
public interface IKitapServisi
{
    Task<IReadOnlyList<KitapListeViewModel>> HepsiniGetirAsync();
    Task<IReadOnlyList<KitapListeViewModel>> KategoriyeGoreGetirAsync(string kategori);
    Task<KitapFormViewModel?> BulByIdAsync(int id);
    Task<int> EkleAsync(KitapFormViewModel model);
    Task<bool> GuncelleAsync(KitapFormViewModel model);
    Task<bool> SilAsync(int id);
    Task<bool> BaslikVarMiAsync(string baslik, int haricId = 0);
}
