using KitabeviMVC.Models.Entities;

namespace KitabeviMVC.Repositories;

// Gün 34: Kitap'a özgü sorgular — generic repository'nin üstüne ek operasyonlar.
// IRepository<Kitap> miras alarak temel CRUD de dahil olur.
public interface IKitapRepository : IRepository<Kitap>
{
    Task<IList<Kitap>> GetStokluKitaplarAsync();
    Task<IList<Kitap>> GetKategoriyleAsync(string kategori);
    Task<Kitap?>        GetYazarlıAsync(int id);   // YazarNavigation Include ile
}
