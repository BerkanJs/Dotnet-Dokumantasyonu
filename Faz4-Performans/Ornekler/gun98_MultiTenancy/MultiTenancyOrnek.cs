// GÜN 98 — Multi-Tenancy
// Row-Level isolation: tek DB, tüm tenantlar aynı tabloda, TenantId kolonu ile ayrım
// Global Query Filter ile tenant izolasyonu otomatik sağlanır

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ornekler.gun98;

// --- 1. Tenant context servisi ---
public interface ICurrentTenantService
{
    string TenantId { get; }
}

public class HttpContextTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContext;

    public HttpContextTenantService(IHttpContextAccessor httpContext)
        => _httpContext = httpContext;

    public string TenantId
    {
        get
        {
            var ctx = _httpContext.HttpContext;

            // ne yapar: subdomain'den tenant belirle (kitabevi1.app.com → kitabevi1)
            // bunu yazmasaydık: her request için tenant manuel çözülmek zorunda kalırdı
            var host = ctx?.Request.Host.Host ?? "";
            if (host.Split('.').Length > 2)
                return host.Split('.')[0];

            // Header fallback: X-Tenant-Id: kitabevi1
            return ctx?.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                ?? throw new InvalidOperationException("Tenant belirlenemedi");
        }
    }
}

// --- 2. Interface: tenant-aware entity ---
public interface ITenantEntity
{
    string TenantId { get; set; }
}

// --- 3. Entity ---
public class Kitap : ITenantEntity
{
    public int Id { get; set; }
    public string Ad { get; set; } = null!;
    public decimal Fiyat { get; set; }

    // ne yapar: bu kitabın hangi tenant'a ait olduğunu belirtir
    // bunu yazmasaydık: tenant A, tenant B'nin kitaplarını görebilirdi
    public string TenantId { get; set; } = null!;
}

// --- 4. SaveChangesInterceptor: TenantId otomatik set ---
public class TenantInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentTenantService _tenant;

    public TenantInterceptor(ICurrentTenantService tenant) => _tenant = tenant;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var yeniKayitlar = eventData.Context?.ChangeTracker
            .Entries<ITenantEntity>()
            .Where(e => e.State == EntityState.Added);

        if (yeniKayitlar is null) return base.SavingChangesAsync(eventData, result, ct);

        foreach (var entry in yeniKayitlar)
        {
            // ne yapar: yeni kayıt eklenirken TenantId otomatik set edilir
            // bunu yazmasaydık: her service'de _tenant.TenantId set etmeyi unutabilirdik
            entry.Entity.TenantId = _tenant.TenantId;
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}

// --- 5. DbContext: Global Query Filter ile izolasyon ---
public class TenantDbContext : DbContext
{
    private readonly ICurrentTenantService _tenant;

    public TenantDbContext(
        DbContextOptions<TenantDbContext> options,
        ICurrentTenantService tenant) : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Kitap> Kitaplar => Set<Kitap>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ne yapar: her sorguya WHERE TenantId = 'aktif_tenant' otomatik eklenir
        // bunu yazmasaydık: her repository metodunda .Where(k => k.TenantId == tenantId) yazmak zorunda kalırdık
        // — ve birini unuttuğumuzda tenant izolasyonu delindi
        modelBuilder.Entity<Kitap>()
            .HasQueryFilter(k => k.TenantId == _tenant.TenantId);
    }
}

// Program.cs:
// builder.Services.AddHttpContextAccessor();
// builder.Services.AddScoped<ICurrentTenantService, HttpContextTenantService>();
// builder.Services.AddSingleton<TenantInterceptor>();
// builder.Services.AddDbContext<TenantDbContext>((sp, opt) =>
// {
//     opt.UseSqlServer(connectionString);
//     opt.AddInterceptors(sp.GetRequiredService<TenantInterceptor>());
// });
