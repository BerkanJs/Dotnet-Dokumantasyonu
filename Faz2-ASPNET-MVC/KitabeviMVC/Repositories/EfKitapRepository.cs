using KitabeviMVC.Data;
using KitabeviMVC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Repositories;

// Gün 34: Kitap'a özgü EF Core repository.
// EfRepository<Kitap>'ı miras alır → CRUD metodları hazır gelir, sadece özel sorgular yazılır.
public class EfKitapRepository : EfRepository<Kitap>, IKitapRepository
{
    public EfKitapRepository(KitabeviDbContext context) : base(context)
    {
        // base(context): _context ve _dbSet üst sınıfta atanır
        // bunu yazmadan _dbSet null kalır → NullReferenceException
    }

    public async Task<IList<Kitap>> GetStokluKitaplarAsync()
        => await _dbSet
            .AsNoTracking()                        // sadece okuma — tracked etme
            .Where(k => k.StokAdedi > 0)           // filtre SQL'e gider, bellekte değil
            .OrderBy(k => k.Baslik)
            .ToListAsync();

    public async Task<IList<Kitap>> GetKategoriyleAsync(string kategori)
        => await _dbSet
            .AsNoTracking()
            .Where(k => k.Kategori == kategori)
            .OrderBy(k => k.Baslik)
            .ToListAsync();

    public async Task<Kitap?> GetYazarlıAsync(int id)
        => await _dbSet
            .Include(k => k.YazarNavigation)       // Yazarlar tablosuyla JOIN
                                                   // bunu yazmasaydık YazarNavigation null gelirdi
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id);
}
