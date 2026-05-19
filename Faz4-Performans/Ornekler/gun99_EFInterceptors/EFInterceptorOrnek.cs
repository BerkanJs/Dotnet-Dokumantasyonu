// GÜN 99 — EF Core Interceptors
// Pipeline: DbCommand → DbConnection → SaveChanges → Materialization
// Her aşamayı intercept edip davranışı değiştirebilirsin

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Diagnostics;

namespace Ornekler.gun99;

// --- 1. IDbCommandInterceptor: yavaş sorgu loglama ---
public class YavasKomutInterceptor : DbCommandInterceptor
{
    private static readonly TimeSpan _esik = TimeSpan.FromMilliseconds(500);

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken ct = default)
    {
        // ne yapar: 500ms'den uzun süren sorguları loglar
        // bunu yazmasaydık: N+1 veya yavaş sorguları production'da fark edemezdik
        if (eventData.Duration > _esik)
        {
            Console.WriteLine($"[YAVAŞ SORGU] {eventData.Duration.TotalMs()}ms:\n{command.CommandText}");
        }

        return await base.ReaderExecutedAsync(command, eventData, result, ct);
    }
}

// --- 2. ISaveChangesInterceptor: domain event dispatch ---
public class DomainEventInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider _sp;

    public DomainEventInterceptor(IServiceProvider sp) => _sp = sp;

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken ct = default)
    {
        // ne yapar: DB kaydı başarılı olduktan SONRA domain event'leri dispatch eder
        // bunu yazmasaydık: event dispatch edip DB kaydı başarısız olsaydı tutarsızlık olurdu
        if (eventData.Context is not null)
            await DispatchDomainEventsAsync(eventData.Context, ct);

        return result;
    }

    private async Task DispatchDomainEventsAsync(DbContext db, CancellationToken ct)
    {
        var entitiesWithEvents = db.ChangeTracker.Entries<IAggregateRoot>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Any())
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            var events = entity.DomainEvents.ToList();
            entity.DomainEvents.Clear();    // ne yapar: bir daha dispatch edilmesin
            // bunu yazmasaydık: her SaveChanges'ta aynı event tekrar tekrar dispatch edilirdi

            foreach (var domainEvent in events)
            {
                // ne yapar: event'i MediatR veya benzeri bir dispatcher'a gönderir
                // bunu yazmasaydık: domain event'leri DB işleminden bağımsız çalıştırılamazdı
                await _sp.GetRequiredService<IDomainEventDispatcher>()
                    .DispatchAsync(domainEvent, ct);
            }
        }
    }
}

// --- 3. IDbConnectionInterceptor: bağlantı havuzu izleme ---
public class BaglantiInterceptor : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken ct = default)
    {
        // ne yapar: yeni DB bağlantısı açıldığında metric kaydeder
        // bunu yazmasaydık: bağlantı havuzu sorunlarını production'da anlayamazdık
        Console.WriteLine($"[DB BAĞLANTI] Açıldı: {connection.Database}");
        await base.ConnectionOpenedAsync(connection, eventData, ct);
    }

    public override async Task ConnectionClosedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        await base.ConnectionClosedAsync(connection, eventData);
    }
}

// Yardımcı tipler
public interface IAggregateRoot
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

public interface IDomainEvent { }
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct);
}

// Program.cs:
// builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
// {
//     opt.UseSqlServer(connectionString)
//        .AddInterceptors(
//            new YavasKomutInterceptor(),
//            sp.GetRequiredService<DomainEventInterceptor>(),
//            new BaglantiInterceptor());
// });

public static class Extensions
{
    public static double TotalMs(this TimeSpan ts) => ts.TotalMilliseconds;
}

public class AppDbContext : DbContext { }
