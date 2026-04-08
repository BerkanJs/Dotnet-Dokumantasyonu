using KitabeviMVC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Data;

// Gün 29: DbContext — Unit of Work + Repository pattern'in EF Core implementasyonu.
//
// Unit of Work: birden fazla değişikliği toplar, tek SaveChanges() ile atomik yazar.
// Repository:   her DbSet<T>, o entity'ye erişim noktasıdır.
//
// DI kaydı Program.cs'te → Scoped (her HTTP request için taze instance).
public class KitabeviDbContext : DbContext
{
    public KitabeviDbContext(DbContextOptions<KitabeviDbContext> options) : base(options) { }

    // Her DbSet bir veritabanı tablosunu temsil eder.
    public DbSet<Kitap> Kitaplar => Set<Kitap>();
    public DbSet<Yazar> Yazarlar => Set<Yazar>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ─────────────────────────────────────────────────────────────────────
        // Kitap entity konfigürasyonu — Fluent API.
        //
        // Fluent API > DataAnnotations: daha güçlü (filtered index, composite key vb.)
        // ve entity sınıfını framework'ten bağımsız tutar.
        // ─────────────────────────────────────────────────────────────────────
        modelBuilder.Entity<Kitap>(entity =>
        {
            entity.ToTable("Kitaplar"); // tablo adı açık belirtildi

            entity.HasKey(k => k.Id);

            entity.Property(k => k.Baslik)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(k => k.Yazar)
                .IsRequired()
                .HasMaxLength(100);

            // Para birimi → decimal(18,2): 18 basamak, 2 ondalık
            entity.Property(k => k.Fiyat)
                .HasColumnType("decimal(18,2)");

            entity.Property(k => k.Kategori)
                .HasMaxLength(50);

            // Varsayılan değer: EklemeTarihi insert sırasında DB tarafından atanır.
            // NOT: InMemory provider bu özelliği desteklemez — uygulama katmanında set edilir.
            entity.Property(k => k.EklemeTarihi)
                .HasDefaultValueSql("GETUTCDATE()");

            // Index — Baslik aramaları hızlanır.
            // IsUnique: aynı başlıkta iki kitap olamaz.
            entity.HasIndex(k => k.Baslik)
                .IsUnique()
                .HasDatabaseName("IX_Kitaplar_Baslik");

            // Index — Kategori filtrelemesi sık kullanılıyor → index faydalı.
            entity.HasIndex(k => k.Kategori)
                .HasDatabaseName("IX_Kitaplar_Kategori");

            // ─────────────────────────────────────────────────────────
            // Gün 29: İlişki tanımı — Kitap (many) ↔ Yazar (one).
            //
            // HasOne(k => k.YazarNavigation)   → Kitap'ın bir Yazar'ı var
            // WithMany(y => y.Kitaplar)         → Yazar'ın çok Kitap'ı var
            // HasForeignKey(k => k.YazarId)     → FK kolonu
            // IsRequired(false)                 → nullable (opsiyonel ilişki)
            // OnDelete(DeleteBehavior.SetNull)   → Yazar silinirse YazarId null'a çekilir
            // ─────────────────────────────────────────────────────────
            entity.HasOne(k => k.YazarNavigation)
                .WithMany(y => y.Kitaplar)
                .HasForeignKey(k => k.YazarId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ─────────────────────────────────────────────────────────────────────
        // Yazar entity konfigürasyonu
        // ─────────────────────────────────────────────────────────────────────
        modelBuilder.Entity<Yazar>(entity =>
        {
            entity.ToTable("Yazarlar");

            entity.HasKey(y => y.Id);

            entity.Property(y => y.Ad)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(y => y.Soyad)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(y => y.Biyografi)
                .HasMaxLength(2000);

            // Ad + Soyad birlikte unique — aynı isimde iki yazar girilemesin.
            entity.HasIndex(y => new { y.Ad, y.Soyad })
                .IsUnique()
                .HasDatabaseName("IX_Yazarlar_AdSoyad");
        });

        // ─────────────────────────────────────────────────────────────────────
        // Seed data — uygulama başladığında DB'ye ilk veriler eklenir.
        //
        // NOT: InMemory provider'da HasData() çalışmaz (migration kavramı yok).
        // Seed, Program.cs'te manuel olarak yapılır (bkz. SeedDataAsync).
        // ─────────────────────────────────────────────────────────────────────
    }
}
