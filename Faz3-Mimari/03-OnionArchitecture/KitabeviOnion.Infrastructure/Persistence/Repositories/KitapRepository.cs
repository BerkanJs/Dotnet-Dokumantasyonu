using KitabeviOnion.Domain.Entities;
using KitabeviOnion.Domain.Interfaces;
using KitabeviOnion.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace KitabeviOnion.Infrastructure.Persistence.Repositories;

public class KitapRepository : IKitapRepository
// ↑ Domain'deki interface'i implement ediyor — Application'ın göreceği tek şey interface
{
    private readonly AppDbContext _context;
    //               ↑ EF Core sadece burada — Domain ve Application bilmiyor

    public KitapRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default)
        => await _context.Kitaplar
            .FirstOrDefaultAsync(k => k.Id == id, ct);

    public async Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct = default)
        => await _context.Kitaplar
            .AsNoTracking()
            //  ↑ okuma sorgusu: change tracking kapalı → daha hızlı, daha az bellek
            //    bunu yazmasaydık → EF Core tüm nesneleri izler, gereksiz overhead
            .ToListAsync(ct);

    public async Task EkleAsync(Kitap kitap, CancellationToken ct = default)
        => await _context.Kitaplar.AddAsync(kitap, ct);

    public async Task<bool> IsbnMevcutMu(Isbn isbn, CancellationToken ct = default)
        => await _context.Kitaplar
            .AnyAsync(k => k.Isbn.Deger == isbn.Deger, ct);

    public async Task SilAsync(int id, CancellationToken ct = default)
    {
        var kitap = await _context.Kitaplar.FindAsync([id], ct);
        //                                   ↑ FindAsync: önce change tracker'a bakar, yoksa DB'ye gider
        //                                     bunu yazmasaydık → FirstOrDefaultAsync kullanırdık,
        //                                     her seferinde DB'ye giderdi

        if (kitap is not null)
            _context.Kitaplar.Remove(kitap);
            //                 ↑ Remove: entity'yi Deleted state'e alır
            //                   SaveChanges çağrılınca DB'den silinir
            //                   bunu yazmasaydık → nesne bellekten silinir ama DB'de kalırdı

        await _context.SaveChangesAsync(ct);
        //             ↑ değişikliği DB'ye yaz
        //               bunu yazmasaydık → Remove çağrılmış ama DB'ye hiç gidilmemiş olurdu
    }
}
