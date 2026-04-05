using KitabeviMVC.Models.Entities;
using KitabeviMVC.Models.ViewModels;

namespace KitabeviMVC.Services;

// Gün 18: İş mantığı controller'da değil, servis katmanında.
// Controller sadece koordine eder — servisi çağır, sonucu view'a ilet.
//
// Singleton kaydedildi (Program.cs): uygulama boyunca tek instance.
// Gerçek projede veritabanı (EF Core) kullanılır; şimdilik in-memory liste.
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

    public IReadOnlyList<KitapListeViewModel> HepsiniGetir() =>
        _kitaplar
            .OrderBy(k => k.Baslik)
            .Select(k => new KitapListeViewModel(k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            .ToList();

    public IReadOnlyList<KitapListeViewModel> KategoriyeGoreGetir(string kategori) =>
        _kitaplar
            .Where(k => k.Kategori.Equals(kategori, StringComparison.OrdinalIgnoreCase))
            .Select(k => new KitapListeViewModel(k.Id, k.Baslik, k.Yazar, k.Fiyat, k.Kategori, k.StokAdedi))
            .ToList();

    public KitapFormViewModel? BulById(int id)
    {
        var kitap = _kitaplar.FirstOrDefault(k => k.Id == id);
        if (kitap is null) return null;

        return new KitapFormViewModel
        {
            Id        = kitap.Id,
            Baslik    = kitap.Baslik,
            Yazar     = kitap.Yazar,
            Fiyat     = kitap.Fiyat,
            Kategori  = kitap.Kategori,
            StokAdedi = kitap.StokAdedi
        };
    }

    public int Ekle(KitapFormViewModel model)
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
        return kitap.Id;
    }

    public bool Guncelle(KitapFormViewModel model)
    {
        var kitap = _kitaplar.FirstOrDefault(k => k.Id == model.Id);
        if (kitap is null) return false;

        kitap.Baslik    = model.Baslik;
        kitap.Yazar     = model.Yazar;
        kitap.Fiyat     = model.Fiyat;
        kitap.Kategori  = model.Kategori;
        kitap.StokAdedi = model.StokAdedi;
        return true;
    }

    public bool Sil(int id)
    {
        var kitap = _kitaplar.FirstOrDefault(k => k.Id == id);
        if (kitap is null) return false;

        _kitaplar.Remove(kitap);
        return true;
    }

    public bool BaslikVarMi(string baslik, int haricId = 0) =>
        _kitaplar.Any(k => k.Baslik.Equals(baslik, StringComparison.OrdinalIgnoreCase)
                        && k.Id != haricId);
}
