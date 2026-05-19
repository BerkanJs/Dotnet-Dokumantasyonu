using KitabeviMediatr.Domain.Entities;
using KitabeviMediatr.Domain.Interfaces;
using KitabeviMediatr.Domain.Specifications;
using KitabeviMediatr.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMediatr.Infrastructure.Persistence.Repositories;

public class KitapRepository : IKitapRepository
{
    private readonly AppDbContext _context;

    public KitapRepository(AppDbContext context) => _context = context;

    public async Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default)
        => await _context.Kitaplar.FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct = default)
        => await _context.Kitaplar.AsNoTracking().ToListAsync(ct);

    public async Task EkleAsync(Kitap kitap, CancellationToken ct = default)
        => await _context.Kitaplar.AddAsync(kitap, ct);

    public async Task<bool> IsbnMevcutMu(Isbn isbn, CancellationToken ct = default)
        => await _context.Kitaplar
            .AnyAsync(k => k.Isbn.Deger == isbn.Deger, ct);
    //      ↑ EF Core owned entity query — Value Object'in Deger alanına bakıyor
    //        bunu yazmasaydık → string karşılaştırma yapardık, tire/boşluk sorunu çıkardı

    public async Task KaydetAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
    //   ↑ tek SaveChanges — EkleAsync sonrası çağrılır, ID burada atanır

    public async Task<IReadOnlyList<Kitap>> ListAsync(ISpecification<Kitap> spec, CancellationToken ct = default)
        => await _context.Kitaplar
            .Where(spec.Criteria)
            //      ↑ Criteria bir Expression<Func<Kitap,bool>> — EF Core bunu SQL WHERE'e çevirir
            //        spec ne getirirse getirsin, repository bu satırı değiştirmiyor
            //        bunu yazmasaydık → her filtre için yeni if bloğu veya yeni metot yazardık
            .AsNoTracking()
            //   ↑ okuma sorgusu: change tracking kapalı — daha hızlı, daha az bellek
            //     bunu yazmasaydık → EF Core tüm nesneleri izler, gereksiz overhead
            .ToListAsync(ct);
}
