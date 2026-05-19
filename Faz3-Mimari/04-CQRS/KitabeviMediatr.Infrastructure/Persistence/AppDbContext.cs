using KitabeviMediatr.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMediatr.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Kitap> Kitaplar => Set<Kitap>();
    public DbSet<Siparis> Siparisler => Set<Siparis>();
    public DbSet<SiparisKalemi> SiparisKalemleri => Set<SiparisKalemi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Kitap>(e =>
        {
            e.HasKey(k => k.Id);
            e.OwnsOne(k => k.Fiyat, f =>
            {
                f.Property(f => f.Deger).HasColumnName("Fiyat").HasPrecision(18, 2);
                f.Property(f => f.ParaBirimi).HasColumnName("ParaBirimi").HasMaxLength(3);
            });
            e.OwnsOne(k => k.Isbn, i =>
            {
                i.Property(i => i.Deger).HasColumnName("Isbn").HasMaxLength(13);
            });
            e.Property(k => k.Baslik).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Siparis>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Durum).HasConversion<string>();
            e.HasMany<SiparisKalemi>("_kalemler").WithOne().HasForeignKey("SiparisId");
            e.Ignore(s => s.DomainEvents);
        });

        modelBuilder.Entity<SiparisKalemi>(e =>
        {
            e.HasKey(k => k.Id);
            e.OwnsOne(k => k.BirimFiyat, f =>
            {
                f.Property(f => f.Deger).HasColumnName("BirimFiyat").HasPrecision(18, 2);
                f.Property(f => f.ParaBirimi).HasColumnName("ParaBirimi").HasMaxLength(3);
            });
        });
    }
}
