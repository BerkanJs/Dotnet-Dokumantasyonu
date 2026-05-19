using KitabeviOnion.Domain.Entities;
using KitabeviOnion.Domain.ValueObjects;

namespace KitabeviOnion.Domain.Interfaces;

public interface IKitapRepository
// ↑ interface Domain katmanında tanımlanıyor — kim implement ettiğini bilmiyor
//   bunu yazmasaydık → Application, EF Core'u doğrudan çağırmak zorunda kalırdı
//   SQL Server'dan PostgreSQL'e geçince Application da değişirdi
{
    Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Kitap>> TumunuGetirAsync(CancellationToken ct = default);
    Task EkleAsync(Kitap kitap, CancellationToken ct = default);
    Task<bool> IsbnMevcutMu(Isbn isbn, CancellationToken ct = default);
    Task SilAsync(int id, CancellationToken ct = default);
    // ↑ sözleşme Domain'de — kim implement edeceğini bilmiyor
    //   bunu yazmasaydık → Handler doğrudan Infrastructure'a bağlanmak zorunda kalırdı
}
