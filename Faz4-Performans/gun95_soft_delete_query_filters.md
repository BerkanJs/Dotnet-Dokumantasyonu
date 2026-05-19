# Gün 95 — Soft Delete ve Global Query Filters

---

## Soft Delete Nedir?

Kullanıcı "sil" dediğinde kaydı veritabanından gerçekten silmek yerine `IsDeleted = true` işaretliyorsun. Kayıt tabloda duruyor ama uygulama onu görmezden geliyor.

**Neden fiziksel silme yerine soft delete?**
- **Geri alma:** Kullanıcı yanlışlıkla sildi → admin "geri getir" yapabilir
- **Audit/hukuk:** "Bu kitap ne zaman silindi, kim sildi?" sorusuna cevap verebilirsin
- **İlişki bütünlüğü:** Kitap silinse bile eski siparişlerde referans kalır (foreign key kırılmaz)
- **Veri analizi:** Silinen ürünlerin istatistikleri hâlâ erişilebilir

**Ne zaman fiziksel sil (hard delete)?**
- KVKK/GDPR — kullanıcı "verimi sil" dedi → gerçekten silmek zorundasın
- Geçici/teknik veri — log, cache, temp kayıtlar
- Tablo çok büyüdü ve silinen kayıtlar performansı düşürüyor

---

## Sorun: Her Sorguda Elle Filtreleme

```csharp
// ✗ Her sorguda WHERE IsDeleted = false yazman lazım:
var kitaplar = await _context.Kitaplar
    .Where(k => !k.IsDeleted)    // bunu unutursan → silinen kitaplar da gelir!
    .Where(k => k.KategoriId == id)
    .ToListAsync();

// 50 farklı sorguda bunu yazmak zorundasın
// BİR TANESINDE unutursan → bug: kullanıcı silinmiş kitabı görür
```

Bu yaklaşım tehlikeli çünkü:
- Yeni geliştirici projede bu kuralı bilmeyebilir
- 50 sorgudan birinde unutmak çok kolay
- Test'te yakalanması zor — çoğu zaman silinen kayıt olmadığı için test geçer

---

## Çözüm: Global Query Filter

EF Core'a "bu entity'yi her sorguladığında otomatik olarak `IsDeleted = false` filtresi ekle" diyorsun. Bir kez yaz, her sorguda otomatik uygulanır.

```csharp
// Entity:
public class Kitap
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public int YazarId { get; set; }
    public bool IsDeleted { get; set; }          // soft delete flag
    public DateTime? SilinmeTarihi { get; set; } // ne zaman silindi
}

// DbContext — OnModelCreating:
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Kitap>()
        .HasQueryFilter(k => !k.IsDeleted);
    // ne yapar → Kitap sorgulandığında EF Core otomatik WHERE IsDeleted = 0 ekler
    // bunu yazmasaydık → her sorguda elle filtrelemen lazım (ve unutma riski)
    // her Include'da da geçerli → kitap.Yorumlar çekilirken silinmiş yorumlar gelmez

    modelBuilder.Entity<Yorum>()
        .HasQueryFilter(y => !y.IsDeleted);
    // aynı pattern → her soft-delete entity'ye uygulanır
}
```

Artık:
```csharp
// Bu sorgu otomatik olarak silinmişleri dışlar:
var kitaplar = await _context.Kitaplar.ToListAsync();
// Üretilen SQL: SELECT ... FROM Kitaplar WHERE IsDeleted = 0
// Elle filtre yazmana gerek yok — framework garantiliyor
```

---

## IgnoreQueryFilters — Filtreyi Geçici Kaldır

Admin panelinde silinmiş kayıtları da görmek istiyorsun:

```csharp
// Admin: tüm kitaplar (silinmişler dahil)
var hepsi = await _context.Kitaplar
    .IgnoreQueryFilters()    // global filter devre dışı
    .ToListAsync();
// Üretilen SQL: SELECT ... FROM Kitaplar (WHERE yok)
// ne zaman kullan → admin paneli, geri yükleme ekranı, raporlama
// bunu yazmasaydık → silinmiş kayıtlara hiç erişemezdin

// Sadece silinmişleri göster:
var silinenler = await _context.Kitaplar
    .IgnoreQueryFilters()
    .Where(k => k.IsDeleted)
    .OrderByDescending(k => k.SilinmeTarihi)
    .ToListAsync();
```

---

## SaveChangesInterceptor ile Otomatik Soft Delete

`Remove()` çağrıldığında EF Core fiziksel DELETE yapar. Bunu intercept edip soft delete'e çevirebilirsin — geliştirici `Remove()` yazsa bile arka planda `IsDeleted = true` yapılır.

```csharp
public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var context = eventData.Context!;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Sadece "Deleted" state'indeki ve ISoftDeletable olan entity'ler
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable entity)
            {
                entry.State = EntityState.Modified;     // DELETE → UPDATE'e çevir
                entity.IsDeleted = true;
                entity.SilinmeTarihi = DateTime.UtcNow;
                // ne yapar → DELETE SQL yerine UPDATE SET IsDeleted=1 üretilir
                // bunu yazmasaydık → Remove() çağrıldığında kayıt fiziksel silinir
            }
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}

// Interface:
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? SilinmeTarihi { get; set; }
}

// Entity bu interface'i implemente eder:
public class Kitap : ISoftDeletable
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime? SilinmeTarihi { get; set; }
}

// Kayıt — Program.cs:
builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
{
    opt.UseSqlServer(connectionString)
       .AddInterceptors(new SoftDeleteInterceptor());
    // ne yapar → SaveChanges çağrıldığında interceptor araya girer
});
```

Artık geliştirici normal `Remove()` yazdığında:
```csharp
_context.Kitaplar.Remove(kitap);
await _context.SaveChangesAsync();
// Beklenen: DELETE FROM Kitaplar WHERE Id = 42
// Gerçek: UPDATE Kitaplar SET IsDeleted = 1, SilinmeTarihi = '...' WHERE Id = 42
// Geliştirici fark etmez — interceptor arka planda çevirir
```

---

## Cascade Soft Delete — İlişkili Kayıtlar

Bir yazar silindiğinde kitapları da soft-delete olmalı mı?

```csharp
// Interceptor'da cascade:
foreach (var entry in context.ChangeTracker.Entries())
{
    if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable entity)
    {
        entry.State = EntityState.Modified;
        entity.IsDeleted = true;
        entity.SilinmeTarihi = DateTime.UtcNow;

        // İlişkili kayıtları da soft-delete et:
        if (entry.Entity is Yazar yazar)
        {
            var kitaplar = context.Set<Kitap>().Where(k => k.YazarId == yazar.Id);
            foreach (var kitap in kitaplar)
            {
                kitap.IsDeleted = true;
                kitap.SilinmeTarihi = DateTime.UtcNow;
            }
        }
    }
}
```

**Dikkat:** Cascade soft delete karmaşıklaşabilir (kitap → yorumlar → beğeniler...). Basit senaryolarda yapılabilir, derin ilişkilerde domain event ile yönetmek daha temiz.

---

## Unique Index Çakışması

Sorun: `Email` unique index var. Kullanıcı silinmiş (IsDeleted=true) ama email hâlâ tabloda → aynı email ile yeni kayıt oluşturulamaz.

```csharp
// ✗ Normal unique index — silinmişlerle çakışır:
modelBuilder.Entity<Kullanici>()
    .HasIndex(k => k.Email)
    .IsUnique();
// "berkan@mail.com" silinmiş olsa bile yeni kayıtta aynı email kullanılamaz

// ✓ Filtered unique index — sadece aktifler arasında unique:
modelBuilder.Entity<Kullanici>()
    .HasIndex(k => k.Email)
    .IsUnique()
    .HasFilter("[IsDeleted] = 0");
// ne yapar → unique kısıtı sadece IsDeleted=0 olan kayıtlara uygulanır
// silinmiş kayıt aynı email'e sahip olabilir — yeni kayıt engellenmez
// bunu yazmasaydık → "bu email zaten var" hatası (ama kayıt silinmiş!)
```

---

## Fiziksel Temizlik (Purge) Stratejisi

Soft-delete kayıtlar sonsuza kadar tabloda kalırsa → tablo şişer, performans düşer. Periyodik olarak eski silinmiş kayıtları fiziksel sil.

```csharp
// Background service ile periyodik temizlik:
public class PurgeDeletedRecordsJob : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 1 yıldan eski silinmiş kayıtları fiziksel sil:
            var threshold = DateTime.UtcNow.AddYears(-1);

            await context.Kitaplar
                .IgnoreQueryFilters()
                .Where(k => k.IsDeleted && k.SilinmeTarihi < threshold)
                .ExecuteDeleteAsync(ct);
            // ne yapar → 1 yıldan eski soft-deleted kayıtları gerçekten siler
            // neden 1 yıl → hukuki saklama süresi geçtikten sonra (projeye göre değişir)
            // ExecuteDeleteAsync → tek SQL, bellekte entity yüklenmez

            await Task.Delay(TimeSpan.FromHours(24), ct);  // günde 1 kez
        }
    }
}
```

---

## EF Core 10 — Named Query Filters (Yenilik)

EF Core 10'da birden fazla filter tanımlayıp isimle açıp kapatabilirsin:

```csharp
// Tanımlama:
modelBuilder.Entity<Kitap>()
    .HasQueryFilter("softDelete", k => !k.IsDeleted)
    .HasQueryFilter("tenant", k => k.TenantId == currentTenantId);

// Kullanım — sadece birini kaldır:
var silinenler = await _context.Kitaplar
    .IgnoreQueryFilter("softDelete")   // soft delete filtresi kalktı
    .ToListAsync();                     // ama tenant filtresi hâlâ aktif
// ne yapar → EF Core 9'da IgnoreQueryFilters() TÜM filtreleri kaldırıyordu
// EF Core 10'da isimle seçerek kaldırabilirsin — daha hassas kontrol
```

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de silme = fiziksel `DELETE FROM`. Sorunlar:
- Yanlışlıkla silinen kayıt geri getirilemez
- Eski siparişlerdeki kitap referansı kırılır (FK violation veya null)
- "Bu kitap ne zaman silindi?" sorusuna cevap verilemez

50K kullanıcıda audit trail + geri alma + hukuki saklama zorunluluğu → soft delete kaçınılmaz.

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| Soft delete | İyi alışkanlık — basit IsDeleted flag | Zorunlu — audit ve geri alma |
| Global Query Filter | Her zaman kullan (güvenlik katmanı) | Her zaman kullan |
| SaveChangesInterceptor | Opsiyonel — elle yönetilebilir | Ekip büyükse interceptor güvenli |
| Cascade soft delete | Basit ilişkilerde | Domain event ile yönet |
| Filtered unique index | İhtiyaç varsa | Kesinlikle — email/username çakışması |
| Purge stratejisi | Gereksiz (tablo küçük) | Zorunlu — tablo şişmesini önle |

---

## Kontrol Soruları

1. Soft delete neden fiziksel silmeden daha güvenli? Ne zaman fiziksel silme zorunlu?
2. Global Query Filter olmadan soft delete neden tehlikeli?
3. IgnoreQueryFilters() ne zaman kullanılır?
4. SaveChangesInterceptor ile Remove() nasıl soft delete'e çevrilir?
5. Unique index soft-delete ile neden çakışır? Filtered index nasıl çözer?
6. Purge stratejisi neden gerekli? Ne kadar süre sonra fiziksel silmek uygun?
