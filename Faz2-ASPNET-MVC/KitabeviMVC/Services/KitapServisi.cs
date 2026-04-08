using KitabeviMVC.Models.Entities;
using KitabeviMVC.Models.ViewModels;

namespace KitabeviMVC.Services;

// Gün 18: In-memory liste — test ve geliştirme ortamı için.
// Gün 29: IKitapServisi async'e çevrildi → Task.FromResult ile uyum sağlandı.
//
// Task.FromResult(T): zaten hazır değeri Task'a sarar — I/O beklenmez.
// Gerçek async işlem yok; arayüz uyumluluğu için sarmalanıyor.
//
// Bu sınıf artık birim testlerde "gerçek DB olmadan IKitapServisi test et"
// senaryosunda kullanılabilir (test double / fake).
public class KitapServisi : IKitapServisi
{
    private readonly List<Kitap> _kitaplar =
    [
        new() { Id = 1, Baslik = "Suç ve Ceza",          Yazar = "Dostoyevski", Fiyat = 89,  Kategori = "Roman",   StokAdedi = 12 },
        new() { Id = 2, Baslik = "1984",                  Yazar = "Orwell",      Fiyat = 75,  Kategori = "Roman",   StokAdedi = 8  },
        new() { Id = 3, Baslik = "Kısa Türk Tarihi",     Yazar = "Akşin",       Fiyat = 120, Kategori = "Tarih",   StokAdedi = 5  },
        new() { Id = 4, Baslik = "Sapiens",               Yazar = "Harari",      Fiyat = 140, Kategori = "Tarih",   StokAdedi = 20 },
        new() { Id = 5, Baslik = "Atomik Alışkanlıklar", Yazar = "Clear",       Fiyat = 95,  Kategori = "Kişisel", StokAdedi = 30 },
    ];

    private int _sonrakiId => _kitaplar.Max(k => k.Id) + 1;

    public Task<IReadOnlyList<KitapListeViewModel>> HepsiniGetirAsync()
    {
        IReadOnlyList<KitapListeViewModel> liste = _kitaplar
            .OrderBy(k => k.Baslik)
            .Select(k => new KitapListeViewModel(k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            .ToList();
        return Task.FromResult(liste);
    }

    public Task<IReadOnlyList<KitapListeViewModel>> KategoriyeGoreGetirAsync(string kategori)
    {
        IReadOnlyList<KitapListeViewModel> liste = _kitaplar
            .Where(k => k.Kategori.Equals(kategori, StringComparison.OrdinalIgnoreCase))
            .Select(k => new KitapListeViewModel(k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            .ToList();
        return Task.FromResult(liste);
    }

    public Task<KitapFormViewModel?> BulByIdAsync(int id)
    {
        var kitap = _kitaplar.FirstOrDefault(k => k.Id == id);
        if (kitap is null) return Task.FromResult<KitapFormViewModel?>(null);

        var vm = new KitapFormViewModel
        {
            Id        = kitap.Id,
            Baslik    = kitap.Baslik,
            Yazar     = kitap.Yazar,
            Fiyat     = kitap.Fiyat,
            Kategori  = kitap.Kategori,
            StokAdedi = kitap.StokAdedi
        };
        return Task.FromResult<KitapFormViewModel?>(vm);
    }

    public Task<int> EkleAsync(KitapFormViewModel model)
    {
        var kitap = new Kitap
        {
            Id        = _sonrakiId,
            Baslik    = model.Baslik,
            Yazar     = model.Yazar,
            Fiyat     = model.Fiyat,
            Kategori  = model.Kategori,
            StokAdedi = model.StokAdedi
        };
        _kitaplar.Add(kitap);
        return Task.FromResult(kitap.Id);
    }

    public Task<bool> GuncelleAsync(KitapFormViewModel model)
    {
        var kitap = _kitaplar.FirstOrDefault(k => k.Id == model.Id);
        if (kitap is null) return Task.FromResult(false);

        kitap.Baslik    = model.Baslik;
        kitap.Yazar     = model.Yazar;
        kitap.Fiyat     = model.Fiyat;
        kitap.Kategori  = model.Kategori;
        kitap.StokAdedi = model.StokAdedi;
        return Task.FromResult(true);
    }

    public Task<bool> SilAsync(int id)
    {
        var kitap = _kitaplar.FirstOrDefault(k => k.Id == id);
        if (kitap is null) return Task.FromResult(false);

        _kitaplar.Remove(kitap);
        return Task.FromResult(true);
    }

    public Task<bool> BaslikVarMiAsync(string baslik, int haricId = 0)
    {
        var varMi = _kitaplar.Any(k =>
            k.Baslik.Equals(baslik, StringComparison.OrdinalIgnoreCase) && k.Id != haricId);
        return Task.FromResult(varMi);
    }
}
