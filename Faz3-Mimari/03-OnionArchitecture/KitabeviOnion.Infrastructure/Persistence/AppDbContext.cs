using KitabeviOnion.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KitabeviOnion.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Kitap> Kitaplar => Set<Kitap>();
    public DbSet<Siparis> Siparisler => Set<Siparis>();
    public DbSet<SiparisKalemi> SiparisKalemleri => Set<SiparisKalemi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Kitap konfigürasyonu
        modelBuilder.Entity<Kitap>(e =>
        {
            e.HasKey(k => k.Id);

            e.OwnsOne(k => k.Fiyat, f =>
            //  ↑ Value Object → owned entity: ayrı tablo değil, aynı satırda kolon
            //    bunu yazmasaydık → EF Core Fiyat'ı nasıl map edeceğini bilemezdi
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

        // Siparis konfigürasyonu
        modelBuilder.Entity<Siparis>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Durum).HasConversion<string>();
            //                        ↑ enum → string olarak sakla: "Onaylandi" okunabilir
            //                          bunu yazmasaydık → 0, 1, 2 sayıları saklanırdı, DB'de anlamı yoktu

            e.HasMany<SiparisKalemi>("_kalemler")   // private field'ı backing field olarak tanımla
             .WithOne()
             .HasForeignKey("SiparisId");
            // ↑ EF Core private _kalemler listesini yönetecek
            //   bunu yazmasaydık → EF Core private listeyi göremez, kalemler yüklenmezdi

            e.Ignore(s => s.DomainEvents);
            // ↑ DomainEvents DB'ye kaydedilmez — sadece uçuş sırasında kullanılır
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
