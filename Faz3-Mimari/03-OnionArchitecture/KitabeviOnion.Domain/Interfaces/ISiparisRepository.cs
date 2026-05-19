using KitabeviOnion.Domain.Entities;

namespace KitabeviOnion.Domain.Interfaces;

public interface ISiparisRepository
{
    Task<Siparis?> BulByIdAsync(int id, CancellationToken ct = default);
    Task EkleAsync(Siparis siparis, CancellationToken ct = default);
    Task KaydetAsync(CancellationToken ct = default);
    // ↑ Unit of Work — değişiklikleri tek seferde kaydet
    //   bunu yazmasaydık → her repository kendi SaveChanges çağırırdı,
    //   transaction yönetimi dağılırdı
}
