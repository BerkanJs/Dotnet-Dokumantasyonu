using KitabeviMVC.Models.Entities;
using KitabeviMVC.Models.ViewModels;

namespace KitabeviMVC.Services;

public interface IKitapServisi
{
    IReadOnlyList<KitapListeViewModel> HepsiniGetir();
    IReadOnlyList<KitapListeViewModel> KategoriyeGoreGetir(string kategori);
    KitapFormViewModel? BulById(int id);
    int Ekle(KitapFormViewModel model);
    bool Guncelle(KitapFormViewModel model);
    bool Sil(int id);
    bool BaslikVarMi(string baslik, int haricId = 0);
}
