using KitabeviMediatr.Domain.Entities;

namespace KitabeviMediatr.Domain.Interfaces;

public interface ISiparisRepository
{
    Task<Siparis?> BulByIdAsync(int id, CancellationToken ct = default);
    Task EkleAsync(Siparis siparis, CancellationToken ct = default);
    Task KaydetAsync(CancellationToken ct = default);
}
