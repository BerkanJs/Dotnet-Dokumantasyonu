# Gün 98 — Multi-Tenancy Mimarisi

---

## Multi-Tenancy Nedir?

Tek bir uygulama, birden fazla müşteriye (tenant) hizmet veriyor. Her tenant kendi verisini görüyor, başkasının verisine erişemiyor.

**Analoji:** Bir apartman binası. Her daire ayrı bir aile (tenant). Bina tek (uygulama tek) ama her ailenin kapısı, kilidi, eşyaları ayrı. Bir aile diğerinin dairesine giremiyor.

**Gerçek örnekler:**
- **Slack** — her şirket (workspace) bir tenant. Şirket A, Şirket B'nin mesajlarını göremez.
- **Shopify** — her mağaza bir tenant. Aynı altyapı, binlerce farklı mağaza.
- **SaaS muhasebe yazılımı** — her firma bir tenant. Aynı uygulama, farklı veriler.

**Neden multi-tenant?**
- Her müşteriye ayrı uygulama deploy etmek → maliyet patlar (100 müşteri = 100 sunucu?)
- Tek uygulama + veri izolasyonu → maliyet düşük, bakım kolay, tek deploy

---

## İzolasyon Stratejileri

### 1. Database-per-Tenant — Her Müşteriye Ayrı Veritabanı

```
Tenant A → DB_TenantA
Tenant B → DB_TenantB
Tenant C → DB_TenantC
```

| Avantaj | Dezavantaj |
|---------|------------|
| Tam izolasyon — veri kesinlikle karışmaz | Her tenant için ayrı DB maliyeti |
| Tenant bazlı backup/restore kolay | Connection string yönetimi karmaşık |
| Performans: bir tenant diğerini etkilemez | Migration'ı N kez çalıştırman lazım |
| Yasal gereklilik varsa (veri aynı ülkede kalmalı) | 1000 tenant = 1000 DB (operasyon yükü) |

**Ne zaman:** Büyük kurumsal müşteriler, yasal zorunluluk, tam izolasyon şart.

### 2. Schema-per-Tenant — Aynı DB, Farklı Schema

```
Tek DB:
  schema "tenantA".Kitaplar
  schema "tenantB".Kitaplar
  schema "tenantC".Kitaplar
```

| Avantaj | Dezavantaj |
|---------|------------|
| İyi izolasyon (schema seviyesi) | PostgreSQL'de doğal, SQL Server'da sınırlı |
| Tek DB — maliyet düşük | Migration her schema'ya ayrı uygulanmalı |
| Tenant bazlı tablo boyutu yönetimi | Çok tenant olunca schema sayısı şişer |

**Ne zaman:** PostgreSQL kullanıyorsan, orta seviye izolasyon yeterli, 10-100 tenant.

### 3. Row-Level (Shared DB) — Aynı Tablo, TenantId Kolonu

```
Kitaplar tablosu:
  Id=1, Ad="Clean Code",  TenantId="acme"
  Id=2, Ad="DDD",         TenantId="acme"
  Id=3, Ad="Refactoring", TenantId="beta"
```

| Avantaj | Dezavantaj |
|---------|------------|
| En düşük maliyet — tek DB, tek tablo | İzolasyon yazılıma bağlı (bug = veri sızıntısı) |
| Migration basit — tek schema | Bir tenant'ın büyük verisi diğerlerini yavaşlatabilir |
| Sınırsız tenant eklenebilir | WHERE TenantId = X unutursan → felaket |
| EF Core Global Query Filter ile güvenli | Cross-tenant raporlama karmaşık |

**Ne zaman:** SaaS uygulamalar, çok sayıda küçük tenant, maliyet öncelikli. **En yaygın strateji.**

---

## Tenant Resolving — Tenant'ı Nereden Alırsın?

Her gelen istekte "bu istek hangi tenant'a ait?" sorusunu cevaplamalısın:

```csharp
// 1. Subdomain'den: acme.app.com → tenant = "acme"
public class SubdomainTenantResolver : ITenantResolver
{
    public string? Resolve(HttpContext context)
    {
        var host = context.Request.Host.Host;    // "acme.app.com"
        var tenant = host.Split('.')[0];          // "acme"
        return tenant == "app" ? null : tenant;
        // ne yapar → subdomain'den tenant ID çıkarır
        // ne zaman kullan → müşteriye özel domain (slack tarzı)
    }
}

// 2. HTTP Header'dan: X-Tenant-Id: acme
public class HeaderTenantResolver : ITenantResolver
{
    public string? Resolve(HttpContext context)
    {
        return context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        // ne yapar → API client'ı her istekte header gönderir
        // ne zaman kullan → API-first uygulamalar, mobil client'lar
    }
}

// 3. JWT Claim'den: token içinde "tenant_id" claim'i
public class ClaimTenantResolver : ITenantResolver
{
    public string? Resolve(HttpContext context)
    {
        return context.User.FindFirst("tenant_id")?.Value;
        // ne yapar → login sırasında token'a tenant bilgisi gömülmüş
        // ne zaman kullan → auth zaten varsa, ek header/subdomain istemiyorsan
    }
}

// 4. Route'dan: /api/tenants/{tenantId}/kitaplar
// URL'de açıkça belirtilir — genelde admin API'larında
```

---

## ITenantService — DI ile Tenant Context

```csharp
public interface ICurrentTenantService
{
    string TenantId { get; }
}

public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentTenantService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string TenantId
    {
        get
        {
            // Resolver stratejine göre (header, subdomain, claim):
            var tenantId = _accessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (string.IsNullOrEmpty(tenantId))
                throw new UnauthorizedAccessException("Tenant belirlenemedi");
            return tenantId;
            // ne yapar → her istekte aktif tenant'ı belirler
            // bunu yazmasaydık → hangi tenant'ın verisini gösterdiğini bilemezsin
        }
    }
}

// Program.cs:
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();
// Scoped → her HTTP isteği kendi tenant context'ini alır
```

---

## EF Core Global Query Filter ile Tenant İzolasyonu

Gün 95'te soft delete için Global Query Filter kullandık. Aynı mekanizma tenant izolasyonu için:

```csharp
public class AppDbContext : DbContext
{
    private readonly ICurrentTenantService _tenantService;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenantService tenantService)
        : base(options)
    {
        _tenantService = tenantService;
    }

    public DbSet<Kitap> Kitaplar => Set<Kitap>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Kitap>()
            .HasQueryFilter(k => k.TenantId == _tenantService.TenantId);
        // ne yapar → her sorguya otomatik WHERE TenantId = 'acme' ekler
        // bunu yazmasaydık → her sorguda elle .Where(k => k.TenantId == ...) yazardın
        // BİR KERE unutursan → başka tenant'ın verisi sızar (güvenlik açığı!)

        // Index ekle — tenant bazlı sorgular hızlı olsun:
        modelBuilder.Entity<Kitap>()
            .HasIndex(k => k.TenantId);
        // bunu yazmasaydık → her sorgu full scan yapar (TenantId'ye göre filtreleme yavaş)
    }
}

// Entity:
public class Kitap
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public string TenantId { get; set; } = null!;  // her entity'de olmalı
}
```

Artık:
```csharp
// Acme tenant'ı giriş yaptığında:
var kitaplar = await _context.Kitaplar.ToListAsync();
// SQL: SELECT ... FROM Kitaplar WHERE TenantId = 'acme'
// Beta tenant'ın kitapları GÖRÜNMEz — framework garantiliyor
```

### Yeni Kayıt Eklerken TenantId Otomatik Set Et

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
    {
        if (entry.State == EntityState.Added)
        {
            entry.Entity.TenantId = _tenantService.TenantId;
            // ne yapar → yeni kayıtta TenantId otomatik dolar
            // bunu yazmasaydık → geliştirici TenantId'yi elle set etmeli (unutma riski)
        }
    }
    return await base.SaveChangesAsync(ct);
}
```

---

## PostgreSQL Row Level Security — DB Seviyesinde İzolasyon

Global Query Filter uygulama seviyesinde çalışır — bypass edilebilir (raw SQL, IgnoreQueryFilters). DB seviyesinde garanti istiyorsan → Row Level Security (RLS).

```sql
-- PostgreSQL'de:
ALTER TABLE "Kitaplar" ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON "Kitaplar"
    USING ("TenantId" = current_setting('app.tenant_id'));
-- ne yapar → DB seviyesinde: hangi SQL çalışırsa çalışsın,
-- sadece current_setting ile eşleşen satırlar görünür
-- uygulama bypass etse bile DB engelliyor (savunma derinliği)
```

```csharp
// Her istek başında tenant context set et:
await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();
await using var cmd = conn.CreateCommand();
cmd.CommandText = $"SET app.tenant_id = '{tenantId}'";
await cmd.ExecuteNonQueryAsync();
// ne yapar → bu bağlantı boyunca RLS tenant_id'yi bilir
```

**Ne zaman RLS?** Güvenlik kritik (finans, sağlık), uygulama katmanına güvenmiyorsun, compliance zorunluluğu var.

---

## Cross-Tenant Erişim — Admin Tenant

Bazen tüm tenant'ların verisini görmen lazım (platform admin, raporlama):

```csharp
// Admin endpoint — IgnoreQueryFilters ile:
[Authorize(Roles = "PlatformAdmin")]
[HttpGet("admin/tum-kitaplar")]
public async Task<IActionResult> TumKitaplar()
{
    var hepsi = await _context.Kitaplar
        .IgnoreQueryFilters()    // tenant filtresi kaldırıldı
        .ToListAsync();
    return Ok(hepsi);
    // ne zaman kullan → platform yönetimi, analytics, destek ekibi
    // neden yetki kontrolü şart → yoksa herkes IgnoreQueryFilters'ı tetikleyebilir
}
```

---

## Database-per-Tenant: Connection String Yönetimi

```csharp
// Tenant'a göre connection string seç:
public class TenantDbContextFactory
{
    private readonly ICurrentTenantService _tenantService;
    private readonly ITenantStore _tenantStore;

    public AppDbContext CreateContext()
    {
        var tenant = _tenantStore.GetTenant(_tenantService.TenantId);
        // ne yapar → tenant kaydından connection string alır

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(tenant.ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}

// Tenant store (basit versiyon):
public class TenantStore : ITenantStore
{
    private readonly Dictionary<string, TenantInfo> _tenants = new()
    {
        ["acme"] = new("Server=db1;Database=AcmeDb;..."),
        ["beta"] = new("Server=db1;Database=BetaDb;..."),
    };
    // production'da → ayrı bir "tenant registry" DB'sinden veya config'den okunur
}
```

### Migration Yönetimi — N Tenant, N Database

```csharp
// Her tenant DB'sine migration uygula:
public class MigrationService
{
    private readonly ITenantStore _store;

    public async Task MigrateAllAsync()
    {
        foreach (var tenant in _store.GetAllTenants())
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(tenant.ConnectionString)
                .Options;

            await using var context = new AppDbContext(options);
            await context.Database.MigrateAsync();
            // ne yapar → her tenant DB'sine sırayla migration uygular
            // dikkat → yeni tenant eklendiğinde de migration çalışmalı
        }
    }
}
```

---

## Finbuckle.MultiTenant — Hazır Kütüphane

Her şeyi sıfırdan yazmak yerine Finbuckle kütüphanesi tenant resolving, store ve EF Core entegrasyonunu sağlar:

```csharp
// NuGet: Finbuckle.MultiTenant, Finbuckle.MultiTenant.EntityFrameworkCore
builder.Services.AddMultiTenant<TenantInfo>()
    .WithHeaderStrategy("X-Tenant-Id")       // header'dan resolve et
    .WithEFCoreStore<TenantStoreDbContext>()  // tenant bilgisi EF Core'da
    .WithPerTenantAuthentication();           // tenant bazlı auth

// DbContext'te:
public class AppDbContext : MultiTenantDbContext  // Finbuckle base class
{
    // HasQueryFilter otomatik uygulanır — TenantId kolonu olan entity'lere
}
```

**Ne zaman Finbuckle?** Production-ready multi-tenant hızlıca istiyorsan. **Ne zaman elle yaz?** Basit senaryo (sadece row-level, 1-2 resolver) veya framework bağımlılığı istemiyorsan.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC tek tenant — tüm veri tek bir kullanıcı grubunun. Multi-tenant ihtiyacı yoktu. 50K kullanıcıda SaaS yapıyorsan → 100 farklı firma aynı uygulamayı kullanıyor → tenant izolasyonu zorunlu.

---

## 500 vs 50K Kullanıcı

| Teknik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|--------|-------------------|-------------------|
| Multi-tenancy gerekli mi? | Tek firma kullanıyorsa hayır | SaaS ise zorunlu |
| Row-level (shared DB) | En basit başlangıç | Çoğu SaaS için yeterli |
| Database-per-tenant | Gereksiz | Büyük kurumsal müşterilerde |
| Global Query Filter | Temel güvenlik | Zorunlu + RLS düşün |
| Finbuckle | Küçük projede gereksiz | Zaman kazandırır |

---

## Kontrol Soruları

1. Üç izolasyon stratejisi (DB-per-tenant, schema, row-level) arasındaki trade-off nedir?
2. Global Query Filter tenant izolasyonunda neden kritik? Unutursan ne olur?
3. Tenant resolving'de subdomain vs header vs claim ne zaman tercih edilir?
4. Row Level Security (RLS) neden uygulama katmanı filter'ına ek olarak değerli?
5. Database-per-tenant'ta migration nasıl yönetilir?
6. Cross-tenant erişim neden yetki kontrolü ile korunmalı?
