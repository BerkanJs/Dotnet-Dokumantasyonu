// GÜN 95 — Soft Delete ve Global Query Filters
// Soft delete: kayıt fiziksel silinmez, IsDeleted = true işaretlenir
// Global Query Filter: EF Core sorgularına otomatik WHERE ekler

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ornekler.gun95;

// --- 1. Interface ---
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}

// --- 2. Entity ---
public class Kitap : ISoftDeletable
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public decimal Fiyat { get; set; }

    // ne yapar: fiziksel silme yerine bu flag set edilir
    // bunu yazmasaydık: veri gerçekten silinirdi — kurtarma/audit imkansız olurdu
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

// --- 3. SaveChangesInterceptor: Delete → Soft Delete ---
public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public SoftDeleteInterceptor(ICurrentUserService currentUser)
        => _currentUser = currentUser;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        if (eventData.Context is null) return base.SavingChangesAsync(eventData, result, ct);

        // ne yapar: EntityState.Deleted olan ISoftDeletable entity'leri yakalar
        // bunu yazmasaydık: her repository'de manuel soft delete yazmak zorunda kalırdık
        var silinecekler = eventData.Context.ChangeTracker
            .Entries<ISoftDeletable>()
            .Where(e => e.State == EntityState.Deleted);

        foreach (var entry in silinecekler)
        {
            // ne yapar: fiziksel silme yerine flag set et, state'i Modified yap
            // bunu yazmasaydık: DB'den gerçekten silinirdi
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;
            entry.Entity.DeletedBy = _currentUser.KullaniciId;
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}

// --- 4. DbContext: Global Query Filter ---
public class KitapDbContext : DbContext
{
    public DbSet<Kitap> Kitaplar => Set<Kitap>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ne yapar: tüm Kitap sorgularına WHERE IsDeleted = 0 otomatik eklenir
        // bunu yazmasaydık: her sorguya manuel .Where(k => !k.IsDeleted) yazmak zorunda kalırdık
        modelBuilder.Entity<Kitap>()
            .HasQueryFilter(k => !k.IsDeleted);

        // ne yapar: silinmiş kayıtlar için unique index — aktif kayıt unique, silinen tekrar eklenebilir
        // bunu yazmasaydık: silinen kitap aynı adla tekrar eklenemezdi (unique constraint ihlali)
        modelBuilder.Entity<Kitap>()
            .HasIndex(k => k.Ad)
            .IsUnique()
            .HasFilter("IsDeleted = 0");
    }
}

// --- 5. Kullanım ---
public class KitapServisi
{
    private readonly KitapDbContext _db;

    public KitapServisi(KitapDbContext db) => _db = db;

    public async Task<List<Kitap>> TumunuGetirAsync()
    {
        // ne yapar: global filter devrede — silinmişler otomatik hariç tutulur
        // bunu yazmasaydık: .Where(k => !k.IsDeleted) eklemeyi unuttuk mu? Hata!
        return await _db.Kitaplar.ToListAsync();
    }

    public async Task<List<Kitap>> SilinenlerDahilGetirAsync()
    {
        // ne yapar: global filter'ı devre dışı bırakır — admin sayfaları için
        // bunu yazmasaydık: silinmiş kayıtlara hiç ulaşamazdık
        return await _db.Kitaplar.IgnoreQueryFilters().ToListAsync();
    }
}

public interface ICurrentUserService
{
    string KullaniciId { get; }
}
