using KitabeviMediatr.Domain.Entities;
using KitabeviMediatr.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMediatr.Infrastructure.Persistence.Repositories;

public class SiparisRepository : ISiparisRepository
{
    private readonly AppDbContext _context;

    public SiparisRepository(AppDbContext context) => _context = context;

    public async Task<Siparis?> BulByIdAsync(int id, CancellationToken ct = default)
        => await _context.Siparisler
            .Include("_kalemler")
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task EkleAsync(Siparis siparis, CancellationToken ct = default)
        => await _context.Siparisler.AddAsync(siparis, ct);

    public async Task KaydetAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
