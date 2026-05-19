// GÜN 96 — Audit Trail
// Her değişiklik (kim, ne zaman, ne → ne) loglanır — KVKK, HIPAA gibi uyumluluk için

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace Ornekler.gun96;

// --- 1. Interface: hangi entity'ler audit edilecek ---
public interface IAuditableEntity
{
    DateTime OlusturulmaTarihi { get; set; }
    string OlusturanKullanici { get; set; }
    DateTime? GuncellemeTarihi { get; set; }
    string? GuncelleyenKullanici { get; set; }
}

// --- 2. Audit Log entity ---
public class AuditLog
{
    public int Id { get; set; }
    public string TabloAdi { get; set; } = null!;       // hangi tablo
    public string EntityId { get; set; } = null!;       // hangi kayıt
    public string Aksiyon { get; set; } = null!;        // Created, Updated, Deleted
    public string? EskiDeger { get; set; }              // JSON: önceki değerler
    public string? YeniDeger { get; set; }              // JSON: yeni değerler
    public string KullaniciId { get; set; } = null!;
    public DateTime Tarih { get; set; }
    public string? IpAdresi { get; set; }
}

// --- 3. SaveChangesInterceptor ile otomatik audit ---
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public AuditInterceptor(ICurrentUserService currentUser)
        => _currentUser = currentUser;

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken ct = default)
    {
        // ne yapar: kayıt BAŞARILI olduktan sonra çalışır — başarısız işlemler loglanmaz
        // bunu yazmasaydık: rollback olan değişiklikler de loglanırdı → yanlış audit
        if (eventData.Context is AuditDbContext db)
            await AuditKaydetAsync(db, ct);

        return result;
    }

    private async Task AuditKaydetAsync(AuditDbContext db, CancellationToken ct)
    {
        // ne yapar: ChangeTracker'daki değişen entry'leri tarar
        // bunu yazmasaydık: hangi entity'nin değiştiğini bilmezdik
        var degisiklikler = db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added
                           or EntityState.Modified
                           or EntityState.Deleted)
            .Select(entry => new AuditLog
            {
                TabloAdi = entry.Metadata.GetTableName() ?? entry.Metadata.Name,
                EntityId = string.Join(",", entry.Properties
                    .Where(p => p.Metadata.IsPrimaryKey())
                    .Select(p => p.CurrentValue)),
                Aksiyon = entry.State.ToString(),
                // ne yapar: önceki değerleri JSON'a serialize et
                // bunu yazmasaydık: "ne değişti" bilgisi kaybolurdu
                EskiDeger = entry.State == EntityState.Added ? null
                    : JsonSerializer.Serialize(entry.Properties
                        .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue)),
                YeniDeger = entry.State == EntityState.Deleted ? null
                    : JsonSerializer.Serialize(entry.Properties
                        .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)),
                KullaniciId = _currentUser.KullaniciId,
                Tarih = DateTime.UtcNow
            })
            .ToList();

        if (degisiklikler.Any())
        {
            db.AuditLoglar.AddRange(degisiklikler);
            await db.SaveChangesAsync(ct);
        }
    }
}

// --- 4. DbContext ---
public class AuditDbContext : DbContext
{
    public DbSet<AuditLog> AuditLoglar => Set<AuditLog>();
    public DbSet<Kitap> Kitaplar => Set<Kitap>();
}

public class Kitap : IAuditableEntity
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public decimal Fiyat { get; set; }

    // ne yapar: kim oluşturdu, kim güncelledi — her entity'de otomatik dolar
    // bunu yazmasaydık: her controller'da bu alanları elle set etmek zorunda kalırdık
    public DateTime OlusturulmaTarihi { get; set; }
    public string OlusturanKullanici { get; set; } = null!;
    public DateTime? GuncellemeTarihi { get; set; }
    public string? GuncelleyenKullanici { get; set; }
}

public interface ICurrentUserService
{
    string KullaniciId { get; }
}
