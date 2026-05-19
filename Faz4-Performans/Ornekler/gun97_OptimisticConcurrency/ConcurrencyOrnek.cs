// GÜN 97 — Optimistic Concurrency
// "Çakışma nadirdir" varsayımı — kayıt kilitlenmez, kaydetmeden önce kontrol edilir
// Pessimistic locking yerine kullan: web app'lerde lock tutmak impractical

using Microsoft.EntityFrameworkCore;

namespace Ornekler.gun97;

// --- 1. Entity: RowVersion ile concurrency token ---
public class UrunEntity
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public int StokAdedi { get; set; }
    public decimal Fiyat { get; set; }

    // ne yapar: her UPDATE'de DB otomatik artırır — çakışma tespiti için
    // bunu yazmasaydık: iki kullanıcı aynı satırı aynı anda güncelleyebilirdi, biri kaybolurdu
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}

// --- 2. DbContext ---
public class UrunDbContext : DbContext
{
    public DbSet<UrunEntity> Urunler => Set<UrunEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UrunEntity>()
            // ne yapar: RowVersion'ı concurrency token olarak işaretler
            // bunu yazmasaydık: EF Core WHERE sorgusuna RowVersion eklemezdi — çakışma tespiti olmaz
            .Property(u => u.RowVersion)
            .IsRowVersion();
    }
}

// --- 3. Güncelleme — çakışma yönetimi ---
public class UrunServisi
{
    private readonly UrunDbContext _db;

    public UrunServisi(UrunDbContext db) => _db = db;

    public async Task FiyatGuncelleAsync(int urunId, decimal yeniFiyat, byte[] rowVersion)
    {
        var urun = await _db.Urunler.FindAsync(urunId)
            ?? throw new KeyNotFoundException("Ürün bulunamadı");

        urun.Fiyat = yeniFiyat;

        // ne yapar: RowVersion'ı request'ten gelen değere set et
        // bunu yazmasaydık: EF Core her zaman DB'deki son RowVersion'ı kullanırdı — çakışma tespiti olmaz
        _db.Entry(urun).Property(u => u.RowVersion).OriginalValue = rowVersion;

        try
        {
            await _db.SaveChangesAsync();
            // Üretilen SQL:
            // UPDATE Urunler SET Fiyat = @p0
            // WHERE Id = @p1 AND RowVersion = @p2  ← rowVersion eşleşmezse 0 row affected
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // ne yapar: başka biri aynı satırı bizden önce güncelledi
            // bunu yazmasaydık: sessizce eski veriyle üzerine yazardık (lost update)
            var entry = ex.Entries.Single();
            var dbDegerleri = await entry.GetDatabaseValuesAsync();

            if (dbDegerleri is null)
                throw new InvalidOperationException("Kayıt silinmiş");

            // Strateji 1: son yazan kazanır (client wins)
            // entry.OriginalValues.SetValues(dbDegerleri);
            // await _db.SaveChangesAsync();

            // Strateji 2: kullanıcıya sor
            var dbUrun = dbDegerleri.ToObject() as UrunEntity;
            throw new ConcurrencyException(
                $"Bu ürün başkası tarafından güncellendi. Güncel fiyat: {dbUrun?.Fiyat}");
        }
    }
}

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}
