// GÜN 104 — Domain Event Dispatch
// Domain event: "Sipariş oluşturuldu" → notification email + stok güncelle + analytics
// Dispatch stratejisi: DB kaydından SONRA dispatch → tutarlılık garantisi

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ornekler.gun104;

// --- 1. Domain event tanımı ---
public record SiparisOlusturulduEvent(
    int SiparisId,
    string KullaniciId,
    List<int> KitapIdler,
    decimal ToplamTutar) : INotification; // MediatR INotification

// --- 2. Aggregate root: domain event toplayan entity ---
public abstract class AggregateRoot
{
    private readonly List<INotification> _domainEvents = new();

    // ne yapar: dışarıya sadece okuma izni ver
    // bunu yazmasaydık: dışarıdan event eklenebilir, sınır ihlal edilirdi
    public IReadOnlyList<INotification> DomainEvents => _domainEvents.AsReadOnly();

    protected void DomainEventEkle(INotification domainEvent)
    {
        // ne yapar: event'i listeye ekler — SaveChanges'a kadar dispatch edilmez
        // bunu yazmasaydık: DB transaction olmadan event dispatch edilirdi → tutarsızlık riski
        _domainEvents.Add(domainEvent);
    }

    public void DomainEventleriTemizle() => _domainEvents.Clear();
}

// --- 3. Entity ---
public class Siparis : AggregateRoot
{
    public int Id { get; private set; }
    public string KullaniciId { get; private set; } = null!;
    public List<SiparisKalemi> Kalemler { get; private set; } = new();
    public decimal ToplamTutar { get; private set; }
    public string Durum { get; private set; } = "Beklemede";

    public static Siparis Olustur(string kullaniciId, List<(int kitapId, decimal fiyat)> kalemler)
    {
        var siparis = new Siparis
        {
            KullaniciId = kullaniciId,
            ToplamTutar = kalemler.Sum(k => k.fiyat),
            Kalemler = kalemler.Select(k => new SiparisKalemi { KitapId = k.kitapId, Fiyat = k.fiyat }).ToList()
        };

        // ne yapar: domain event'i entity içinden üret — DB kaydından sonra dispatch edilecek
        // bunu yazmasaydık: application service'de event üretmek zorunda kalırdık — domain logic dışına çıkardı
        siparis.DomainEventEkle(new SiparisOlusturulduEvent(
            siparis.Id,
            kullaniciId,
            kalemler.Select(k => k.kitapId).ToList(),
            siparis.ToplamTutar));

        return siparis;
    }
}

public class SiparisKalemi
{
    public int Id { get; set; }
    public int KitapId { get; set; }
    public decimal Fiyat { get; set; }
}

// --- 4. SaveChangesInterceptor: DB kaydı → event dispatch ---
public class DomainEventDispatchInterceptor : SaveChangesInterceptor
{
    private readonly IPublisher _publisher;

    public DomainEventDispatchInterceptor(IPublisher publisher) => _publisher = publisher;

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken ct = default)
    {
        // ne yapar: DB'ye BAŞARILI kaydedildikten SONRA event'leri dispatch eder
        // bunu yazmasaydık: DB kaydı başarısız olsa bile event gönderilirdi → hayalet notification
        if (eventData.Context is not null)
        {
            var eventler = eventData.Context.ChangeTracker
                .Entries<AggregateRoot>()
                .SelectMany(e => e.Entity.DomainEvents)
                .ToList();

            foreach (var e in eventData.Context.ChangeTracker.Entries<AggregateRoot>())
                e.Entity.DomainEventleriTemizle(); // ne yapar: bir daha dispatch edilmesin

            foreach (var domainEvent in eventler)
                await _publisher.Publish(domainEvent, ct);
        }

        return result;
    }
}

// --- 5. Event handler'lar ---
public class SiparisOlusturulduEmailHandler : INotificationHandler<SiparisOlusturulduEvent>
{
    public async Task Handle(SiparisOlusturulduEvent notification, CancellationToken ct)
    {
        // ne yapar: sipariş oluşturulunca konfirmasyon emaili gönderir
        // bunu yazmasaydık: sipariş oluşturan kod email gönderme sorumluluğunu da taşırdı — SRP ihlali
        Console.WriteLine($"Email gönderiliyor: {notification.KullaniciId}");
        await Task.CompletedTask;
    }
}

public class SiparisOlusturulduStokHandler : INotificationHandler<SiparisOlusturulduEvent>
{
    public async Task Handle(SiparisOlusturulduEvent notification, CancellationToken ct)
    {
        // ne yapar: sipariş edilen kitapların stokunu düşürür
        // bunu yazmasaydık: sipariş servisi stok servisiyle doğrudan bağımlı olurdu
        Console.WriteLine($"Stok güncelleniyor: {string.Join(", ", notification.KitapIdler)}");
        await Task.CompletedTask;
    }
}

// Program.cs:
// builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
// builder.Services.AddSingleton<DomainEventDispatchInterceptor>();
// builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
// {
//     opt.UseSqlServer(connectionString)
//        .AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>());
// });
