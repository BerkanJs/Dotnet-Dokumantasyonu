using KitabeviOnion.Domain.Entities;
using KitabeviOnion.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KitabeviOnion.Infrastructure.Persistence.Repositories;

public class SiparisRepository : ISiparisRepository
{
    private readonly AppDbContext _context;

    public SiparisRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Siparis?> BulByIdAsync(int id, CancellationToken ct = default)
        => await _context.Siparisler
            .Include("_kalemler")
            //       ↑ private backing field adını string olarak belirt
            //         bunu yazmasaydık → kalemler yüklenmez, ToplamTutar() sıfır dönerdi
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task EkleAsync(Siparis siparis, CancellationToken ct = default)
        => await _context.Siparisler.AddAsync(siparis, ct);

    public async Task KaydetAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
    //   ↑ tek SaveChanges: kitap stok değişikliği + yeni sipariş tek transaction'da
    //     bunu yazmasaydık → her repository ayrı kaydetse, stok düşer ama sipariş kaydedilmeyebilirdi
}
