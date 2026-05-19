# Gün 68 — Unit of Work Pattern

Bugünün amacı: birden fazla repository'ye yayılan değişiklikleri tek bir transaction altında yönetmek için Unit of Work pattern'ını anlamak.

---

## Unit of Work Nedir?

Unit of Work (UoW), bir "iş birimi" boyunca yapılan tüm değişiklikleri takip eder ve tek seferde commit ya da rollback eder.

Klasik tanım şu soruyu cevaplar:  
> "Birden fazla repository'de değişiklik yaptım — bunların hepsi ya commit edilsin ya da hiçbiri edilmesin. Bunu nasıl garanti ederim?"

---

## EF Core Zaten Unit of Work'tür

`DbContext`, Unit of Work pattern'ının hazır implementasyonudur:

```csharp
public class KitabeviDbContext : DbContext
{
    // DbContext tüm entity'lerin değişikliklerini ChangeTracker ile takip eder
    // SaveChangesAsync() çağrısında hepsini tek transaction'da yazar

    public DbSet<Kitap> Kitaplar { get; set; }
    public DbSet<Siparis> Siparisler { get; set; }
    public DbSet<SiparisKalemi> SiparisKalemleri { get; set; }
}
```

```csharp
// Kullanım — explicit transaction olmadan:
_context.Kitaplar.Add(yeniKitap);           // değişiklik queue'ya alındı — henüz DB'ye yazılmadı
                                             // bunu yazmasaydık → kitap takip edilmez, SaveChanges'da görünmez
_context.Siparisler.Add(yeniSiparis);       // ikinci değişiklik de queue'da
SaveChangesAsync() → tek seferde commit    // her iki Insert tek transaction içinde yürütür
                                             // bunu yazmasaydık → değişiklikler kalıcı olmaz, bellekte kalır
```

Yani küçük-orta projelerde `DbContext`'i direkt kullanmak yeterlidir.

---

## Faz2 ile Karşılaştırma

Faz2 KitabeviMVC'de transaction yönetimi yoktu:

```csharp
// Faz2 — KitabeviMVC SiparisController (yaklaşık)
public async Task<IActionResult> SiparisOlustur(SiparisViewModel vm)
{
    var siparis = new Siparis { ... };
    _db.Siparisler.Add(siparis);
    await _db.SaveChangesAsync();           // sadece siparis kaydedildi

    // Stok düşürme ayrı SaveChanges
    kitap.StokAdedi -= vm.Adet;
    await _db.SaveChangesAsync();           // eğer burada hata olursa → siparis var, stok düşmedi
}
```

Bu yapıda ilk `SaveChanges` başarılı, ikincisi hata verirse tutarsız veri kalır.

Faz3'te Unit of Work ile:

```csharp
// Faz3 — tek transaction garantisi
await using var transaction = await _context.Database.BeginTransactionAsync(ct);
// bunu yazmasaydık → iki SaveChanges birbirinden bağımsız çalışır, rollback imkansız

_context.Siparisler.Add(siparis);
kitap.StokAdedi -= request.Adet;

await _context.SaveChangesAsync(ct);       // her iki değişiklik tek seferde yazılır
await transaction.CommitAsync(ct);         // her şey başarılı → kalıcı hale gel
// commit olmazsa → transaction otomatik rollback olur
```

---

## Explicit Unit of Work Interface — Ne Zaman Yazılır?

EF Core yeterliyken neden ayrı interface?

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    // bunu interface olarak tanımlamasaydık → test sırasında gerçek DbContext bağlamak zorunda kalırdık
}
```

```csharp
public class UnitOfWork : IUnitOfWork
{
    private readonly KitabeviDbContext _context;

    public UnitOfWork(KitabeviDbContext context)
    {
        _context = context;                 // bağımlılığı dışarıdan al — test edilebilir
                                             // bunu new ile yazsaydık → test sırasında taklit edemezdik
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);   // DbContext'e deleg et — mantık burada değil
}
```

**Interface yazmak şu durumda anlam kazanır:**
- Application layer'da DbContext'e bağımlılık olmasın istiyorsun (Onion/Clean arch kuralı)
- Test sırasında `IUnitOfWork`'ü mock/fake edeceksin

---

## Multiple Aggregate, Tek Transaction

Domain-driven tasarımda her aggregate kendi repository'sine sahiptir.  
Ama bazen iki aggregate'in değişikliği birlikte commit edilmesi gerekir.

```csharp
public class SiparisOlusturHandler
    : IRequestHandler<SiparisOlusturCommand, int>
{
    private readonly ISiparisRepository _siparisRepo;   // siparis aggregate
    private readonly IKitapRepository _kitapRepo;        // kitap aggregate
    private readonly IUnitOfWork _uow;                   // commit kontrolü burada

    public async Task<int> Handle(SiparisOlusturCommand request, CancellationToken ct)
    {
        var kitap = await _kitapRepo.GetByIdAsync(request.KitapId, ct);
        // bunu yazmasaydık → hangi kitabın stoku düşeceğini bilmeyiz

        kitap.StokuDus(request.Adet);                   // domain logic — Kitap entity içinde
                                                          // bunu entity dışında yazsaydık → domain kuralı sızdı
        var siparis = Siparis.Olustur(kitap, request.Adet, request.MusteriId);
        // bunu factory method ile yazsak → nesne her zaman geçerli durumda oluşur

        await _siparisRepo.AddAsync(siparis, ct);        // takibe al
        await _uow.SaveChangesAsync(ct);                 // HER İKİSİ aynı anda commit
                                                          // bunu ayrı ayrı SaveChanges yapsaydık → tutarsızlık riski
        return siparis.Id;
    }
}
```

---

## EF Core'da Transaction Yönetimi (Açık Versiyon)

Karmaşık senaryolar için `BeginTransactionAsync` kullanılır:

```csharp
public async Task<Result> KarmasikIslemAsync(CancellationToken ct)
{
    await using var transaction = await _context.Database.BeginTransactionAsync(ct);
    // bunu yazmasaydık → her SaveChanges otomatik kendi transaction'ını açar/kapatır

    try
    {
        // 1. işlem
        _context.Kitaplar.Add(kitap);
        await _context.SaveChangesAsync(ct);            // henüz commit değil, transaction açık

        // 2. işlem
        _context.Siparisler.Add(siparis);
        await _context.SaveChangesAsync(ct);            // hâlâ transaction içinde

        await transaction.CommitAsync(ct);              // ikisi birden kalıcı olur
        return Result.Success();
    }
    catch
    {
        await transaction.RollbackAsync(ct);            // herhangi biri patlarsa → ikisi de geri döner
        // bunu yazmasaydık → yarım işlem DB'de kalabilir
        return Result.Failure("İşlem başarısız");
    }
}
```

---

## Distributed Transaction — Neden Kaçınılır?

İki farklı veritabanı veya servis arasında işlem yapılıyorsa (örn. SQL + Redis + harici ödeme API) **distributed transaction** gerekir.

Sorunlar:
- Koordinatör bir servis başarısız olursa tüm sistemin işlemlerini geri almak çok zordur
- Performans maliyeti çok yüksektir (2-phase commit protokolü)
- Modern cloud ortamlarında güvenilir şekilde çalışmaz

**Alternatif:** Outbox Pattern veya Saga Pattern kullanılır.

> Şimdilik: distributed transaction yok, tek DB'de transaction yönetimi yeterli.

---

## Faz2 ile Karşılaştırma Özeti

| Konu | Faz2 KitabeviMVC | Faz3 Onion + UoW |
|---|---|---|
| Transaction yönetimi | Her SaveChanges bağımsız | IUnitOfWork ile tek commit noktası |
| Tutarsızlık riski | Yüksek (iki SaveChanges arası hata) | Düşük (rollback garantisi) |
| Test edilebilirlik | DbContext direkt → test zor | IUnitOfWork mock edilebilir |
| Birden fazla aggregate | Birbirinden habersiz | Aynı DbContext paylaşımıyla senkron |

---

## 500 vs 50K Kullanıcı

| Konu | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---|---|---|
| `DbContext` direkt kullanım | Yeterli, ayrı UoW gerekmez | Hâlâ yeterli; interface test için gerekli |
| Explicit transaction | Nadiren gerekli | Birden fazla aggregate değişince zorunlu |
| IUnitOfWork interface | Overengineering olabilir | Test edilebilirlik için mantıklı |
| Distributed transaction | Kesinlikle erken | Mikroservislere geçişte gündem olur |

**Overengineering sinyali:** Tek bir aggregate değişiyorsa `DbContext.SaveChangesAsync()` direkt yeterlidir. Ayrı UoW sınıfı sadece boilerplate olur.

---

## Mini Özet

- `DbContext` zaten Unit of Work'tür — çoğu proje için fazlası gerekmez.
- `IUnitOfWork` interface'i Onion/Clean mimarisinde test edilebilirlik için yazılır.
- Birden fazla aggregate'in aynı anda commit edilmesi gerekiyorsa aynı DbContext'i paylaştırmak yeterlidir.
- Distributed transaction'dan uzak dur — Outbox/Saga bunun yerine kullanılır.

---

## Kontrol Soruları

1. EF Core `DbContext` neden Unit of Work pattern'ının bir implementasyonu sayılır?
2. İki farklı repository'de değişiklik yaptıktan sonra `SaveChangesAsync` tek kez çağrılırsa ne olur?
3. `IUnitOfWork` interface'i yazmak ne zaman anlamlıdır, ne zaman overengineering'dir?
4. Distributed transaction neden tehlikelidir ve yerine ne kullanılır?
