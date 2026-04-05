using KitabeviMVC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Data;

public class KitabeviDbContext : DbContext
{
    public KitabeviDbContext(DbContextOptions<KitabeviDbContext> options) : base(options) { }

    public DbSet<Kitap> Kitaplar => Set<Kitap>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Kitap>(entity =>
        {
            entity.HasKey(k => k.Id);
            entity.Property(k => k.Baslik).IsRequired().HasMaxLength(200);
            entity.Property(k => k.Yazar).IsRequired().HasMaxLength(100);
            entity.Property(k => k.Fiyat).HasColumnType("decimal(18,2)");
        });

        // Seed data
        modelBuilder.Entity<Kitap>().HasData(
            new Kitap { Id = 1, Baslik = "Clean Code",          Yazar = "Robert Martin", Fiyat = 45m,  Kategori = "Yazılım",  StokAdedi = 10 },
            new Kitap { Id = 2, Baslik = "DDD",                 Yazar = "Eric Evans",    Fiyat = 85m,  Kategori = "Mimari",   StokAdedi = 5  },
            new Kitap { Id = 3, Baslik = "The Pragmatic Prog",  Yazar = "Hunt & Thomas", Fiyat = 55m,  Kategori = "Yazılım",  StokAdedi = 8  }
        );
    }
}
