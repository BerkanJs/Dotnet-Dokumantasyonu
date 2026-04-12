using KitabeviMVC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Data;

// Gün 29: DbContext — Unit of Work + Repository pattern'in EF Core implementasyonu.
// Gün 33: Composite index (Kategori+Stok), RowVersion (optimistic concurrency) eklendi.
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

            // ─────────────────────────────────────────────────────────────
            // Gün 33: Composite Index — Kategori + StokAdedi birlikte.
            //
            // Sık çalışan sorgu: WHERE Kategori = ? AND StokAdedi > 0
            // Tekli index (sadece Kategori): önce kategori filtrelenir,
            // sonra her satır için StokAdedi okunur → ek I/O.
            // Composite index: tek B-tree'de iki kolon birlikte → tek okuma.
            //
            // Sol-prefix kuralı: Kategori öne geldiği için
            // "WHERE Kategori = ?" tek başına da bu index'i kullanabilir.
            // Tersine yazsaydık (StokAdedi, Kategori): "WHERE Kategori = ?"
            // bu index'i kullanamaz → yanlış sıra.
            // ─────────────────────────────────────────────────────────────
            entity.HasIndex(k => new { k.Kategori, k.StokAdedi })
                .HasDatabaseName("IX_Kitaplar_Kategori_Stok");
            // composite index tek satırla tanımlanır; EF Core iki kolonu sıraya göre B-tree'ye koyar

            // ─────────────────────────────────────────────────────────────
            // Gün 33: Optimistic Concurrency — RowVersion konfigürasyonu.
            //
            // IsRowVersion(): [Timestamp] attribute'unun Fluent API karşılığı.
            // EF Core, UPDATE SQL'ine WHERE RowVersion = @originalValue ekler.
            // DB her UPDATE'te RowVersion'ı otomatik değiştirir.
            // Eşleşmezse: DbUpdateConcurrencyException fırlatır.
            //
            // NOT: InMemory provider IsRowVersion()'ı desteklemez.
            // Sadece SQL Server (veya gerçek RDBMS) ile çalışır.
            // ─────────────────────────────────────────────────────────────
            entity.Property(k => k.RowVersion)
                .IsRowVersion();
            // bunu yazmasaydık [Timestamp] attribute tek başına yeterliydi,
            // ama Fluent API > DataAnnotation kuralına göre burada da belirtiyoruz

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
        // Gün 32: HasData() — Migration tabanlı seed.
        //
        // HasData() ne zaman kullanılır?
        //   → Nadiren değişen referans veriler: ülke kodları, para birimleri,
        //     sabit kategori listesi vb.
        //   → Değiştirilmesi production deployment gerektirse bile sorun olmayan veriler.
        //
        // HasData() KULLANILMIYOR çünkü:
        //   → InMemory provider desteklemez → test ve geliştirme kırılır.
        //   → Veri değişince yeni migration gerekir (fiyat düzeltmesi = deployment).
        //   → FK sırası (Yazarlar → Kitaplar) migration'da yönetmek karmaşıklaşır.
        //   → Bunun yerine DbSeeder kullanılıyor (Services/DbSeeder.cs).
        //
        // Eğer kullanılsaydı — örnek olarak:
        //
        // modelBuilder.Entity<Yazar>().HasData(
        //     new Yazar { Id = 1, Ad = "Robert", Soyad = "Martin" }
        //     // Id zorunlu: HasData migration'a Id bazlı INSERT üretir.
        //     // Id yazmasaydık EF Core exception fırlatırdı.
        //     // DateTime.UtcNow yazılmaz: her migration oluşturmada değer
        //     // değişir, EF "veri değişti" sanır → gereksiz UPDATE migration üretir.
        // );
        // ─────────────────────────────────────────────────────────────────────
    }
}
