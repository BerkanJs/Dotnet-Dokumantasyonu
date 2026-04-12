# Gün 34 — Repository Pattern & Unit of Work

---

## 1. Neden Repository Pattern?

EF Core'u controller veya service içinde doğrudan kullanmak çalışır — ama sorunlar çıkar:

```
Sorun 1: Test yazamazsın
  Controller: new KitabeviDbContext(...) kullanıyor
  Test: gerçek DB bağlantısı şart → entegrasyon testi, unit test değil

Sorun 2: Sorgu mantığı her yere dağılıyor
  KitapController'da: _context.Kitaplar.Where(k => k.StokAdedi > 0).AsNoTracking()...
  SepetController'da:  _context.Kitaplar.Where(k => k.StokAdedi > 0).AsNoTracking()...
  → Aynı filtre iki yerde, biri güncellendi diğeri unutuldu

Sorun 3: ORM değiştiremezsin
  Yarın EF Core → Dapper geçmek istersen controller koduna dokunmak zorundasın
```

Repository Pattern, veri erişimini soyutlar. Controller sadece arayüzü bilir; arkada EF Core mu Dapper mı bilmez.

```
Controller → IKitapRepository (arayüz)
                   ↓
             EfKitapRepository (EF Core ile gerçek uygulama)
             InMemoryKitapRepository (test için sahte uygulama)
```

---

## 2. Generic Repository

```csharp
// Repositories/IRepository.cs
// Tüm entity'lerin ortak CRUD operasyonlarını tanımlar

public interface IRepository<T> where T : class
{
    Task<T?>           GetByIdAsync(int id);
    Task<IList<T>>     GetAllAsync();
    Task               AddAsync(T entity);
    void               Update(T entity);   // EF Core'da update senkron — tracking zaten var
    void               Delete(T entity);
    Task               SaveAsync();
}
```

```csharp
// Repositories/EfRepository.cs
// Generic implementasyon — her entity için tekrar yazılmaz

public class EfRepository<T> : IRepository<T> where T : class
{
    protected readonly KitabeviDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public EfRepository(KitabeviDbContext context)
    {
        _context = context;                    // DI ile gelir — new yazmıyoruz
        _dbSet   = context.Set<T>();           // T'ye karşılık gelen tablo
                                               // bunu yazmasaydık her metodda context.Kitaplar
                                               // gibi elle belirtmek zorunda kalırdık
    }

    public async Task<T?> GetByIdAsync(int id)
        => await _dbSet.FindAsync(id);
        // FindAsync: önce Change Tracker'a bakar, yoksa DB'ye gider
        // FirstOrDefaultAsync(x => x.Id == id) yazsaydık her seferinde DB'ye giderdi

    public async Task<IList<T>> GetAllAsync()
        => await _dbSet.AsNoTracking().ToListAsync();
        // AsNoTracking: sadece okuyacağız, izleme gerekmiyor
        // yazmasaydık 1000 kayıt için 1000 EntityEntry bellekte yaşardı

    public async Task AddAsync(T entity)
        => await _dbSet.AddAsync(entity);
        // AddAsync sadece "tracked" durumuna alır, DB'ye gitmez
        // DB'ye gitmek için SaveAsync şart

    public void Update(T entity)
        => _context.Entry(entity).State = EntityState.Modified;
        // Entry(entity).State = Modified: "bu nesne değişti, SaveChanges'te UPDATE üret"
        // bunu yazmasaydık ve entity zaten tracked değilse EF Core değişikliği görmezdi

    public void Delete(T entity)
        => _dbSet.Remove(entity);

    public async Task SaveAsync()
        => await _context.SaveChangesAsync();
        // Tüm Add/Update/Delete'leri tek transaction'da DB'ye yazar
}
```

---

## 3. Özel Repository — Entity'ye Özgü Sorgular

Generic repository temel CRUD'u verir. Karmaşık sorgular için entity'ye özel arayüz açarız.

```csharp
// Repositories/IKitapRepository.cs

public interface IKitapRepository : IRepository<Kitap>
{
    // Generic'te olmayan, Kitap'a özel operasyonlar:
    Task<IList<Kitap>> GetStokluKitaplarAsync();
    Task<IList<Kitap>> GetKategoriyleAsync(string kategori);
    Task<Kitap?>        GetDetayliAsync(int id);   // Include'larla
}
```

```csharp
// Repositories/EfKitapRepository.cs

public class EfKitapRepository : EfRepository<Kitap>, IKitapRepository
{
    public EfKitapRepository(KitabeviDbContext context) : base(context) { }
    // base(context): KitabeviDbContext'i üst sınıfa iletiyoruz
    // bunu yazmasaydık _context ve _dbSet null kalırdı

    public async Task<IList<Kitap>> GetStokluKitaplarAsync()
        => await _dbSet
            .AsNoTracking()
            .Where(k => k.StokAdedi > 0)          // filtre SQL'e dönüşür — bellekte değil
            .OrderBy(k => k.Baslik)
            .ToListAsync();

    public async Task<IList<Kitap>> GetKategoriyleAsync(string kategori)
        => await _dbSet
            .AsNoTracking()
            .Where(k => k.Kategori == kategori)
            .ToListAsync();

    public async Task<Kitap?> GetDetayliAsync(int id)
        => await _dbSet
            .Include(k => k.YazarNavigation)       // Yazar bilgisi JOIN ile gelsin
                                                   // bunu yazmasaydık YazarNavigation null gelirdi
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id);
}
```

---

## 4. Unit of Work

Birden fazla repository aynı `DbContext` örneğini paylaşmalıdır. Aksi takdirde farklı context örnekleri → farklı transaction → tutarsız kayıt.

```
Senaryo: Kitap ekle + Stok kaydı ekle (aynı anda başarılı olmalı)

YANLIŞ — iki ayrı context:
  KitapRepository  → KitabeviDbContext (A)  → SaveChanges()
  StokRepository   → KitabeviDbContext (B)  → SaveChanges()
  A başarılı, B başarısız → kitap var ama stok yok → tutarsız veri

DOĞRU — Unit of Work:
  UnitOfWork       → tek KitabeviDbContext
  KitapRepository  ┐
  StokRepository   ┤ → aynı context
  UnitOfWork.SaveAsync() → tek transaction, ikisi birden ya geçer ya dönülür
```

```csharp
// Repositories/IUnitOfWork.cs

public interface IUnitOfWork : IDisposable
{
    IKitapRepository Kitaplar { get; }
    // İleride: IYazarRepository Yazarlar { get; }

    Task<int> SaveAsync();
    // int: etkilenen satır sayısı — loglama veya doğrulama için kullanılabilir
}
```

```csharp
// Repositories/EfUnitOfWork.cs

public class EfUnitOfWork : IUnitOfWork
{
    private readonly KitabeviDbContext _context;

    // Lazy initialization: ilk erişimde oluştur, tekrar erişimde aynısını ver
    private IKitapRepository? _kitaplar;

    public EfUnitOfWork(KitabeviDbContext context)
    {
        _context = context;
        // context DI'dan Scoped olarak gelir
        // Scoped: request başında bir örnek, request sonunda dispose edilir
        // Singleton yapmadık: Singleton UoW → tek context tüm request'ler → thread-safe değil
    }

    public IKitapRepository Kitaplar
        => _kitaplar ??= new EfKitapRepository(_context);
        // ??= (null-coalescing assignment): null ise ata, değilse mevcut nesneyi döndür
        // bunu yazmadan her erişimde new EfKitapRepository() yazsaydık
        // her seferinde yeni nesne oluşur ama aynı context paylaşılırdı (zararsız ama gereksiz)

    public async Task<int> SaveAsync()
        => await _context.SaveChangesAsync();
        // Tüm repository'lerdeki değişiklikler tek çağrıyla DB'ye gider
        // her repository kendi SaveAsync'ini çağırsaydık: ayrı transaction riski

    public void Dispose()
        => _context.Dispose();
        // DI zaten Scoped context'i dispose eder, bu güvence katmanı
}
```

---

## 5. DI Kaydı ve Controller'da Kullanım

```csharp
// Program.cs

builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
// Scoped: her HTTP request için bir örnek
// Transient yaparsaydık: her inject noktasında yeni UoW → yeni context → transaction paylaşımı bozulur
// Singleton yaparsaydık: tüm request'ler aynı context → thread-safety sorunu
```

```csharp
// Controllers/KitapController.cs

public class KitapController : Controller
{
    private readonly IUnitOfWork _uow;

    public KitapController(IUnitOfWork uow)
    {
        _uow = uow;
        // KitabeviDbContext doğrudan inject etmiyoruz — repository katmanı aracı
        // doğrudan context inject etseydi: test sırasında fake repository kullanamaz,
        // her test gerçek DB bağlantısı ister
    }

    public async Task<IActionResult> Index()
    {
        var kitaplar = await _uow.Kitaplar.GetStokluKitaplarAsync();
        // sorgu mantığı controller'da değil, repository'de
        // yarın filtre değişirse tek yeri düzeltirsin
        return View(kitaplar);
    }

    [HttpPost]
    public async Task<IActionResult> Ekle(KitapViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var kitap = new Kitap { Baslik = vm.Baslik, Fiyat = vm.Fiyat, /* ... */ };
        await _uow.Kitaplar.AddAsync(kitap);
        await _uow.SaveAsync();         // tek çağrı, tek transaction
                                        // bunu çağırmasaydık: entity bellekte tracked ama DB'ye gitmez
        return RedirectToAction(nameof(Index));
    }
}
```

---

## 6. Test'te Fake Repository

Pattern'in asıl değeri: DB olmadan unit test yazabilirsin.

```csharp
// Tests/FakeKitapRepository.cs

public class FakeKitapRepository : IKitapRepository
{
    private readonly List<Kitap> _data = new();

    public Task<IList<Kitap>> GetAllAsync()
        => Task.FromResult<IList<Kitap>>(_data);

    public Task<IList<Kitap>> GetStokluKitaplarAsync()
        => Task.FromResult<IList<Kitap>>(_data.Where(k => k.StokAdedi > 0).ToList());

    public Task AddAsync(Kitap entity)
    {
        _data.Add(entity);
        return Task.CompletedTask;
    }

    public Task<Kitap?> GetByIdAsync(int id)
        => Task.FromResult(_data.FirstOrDefault(k => k.Id == id));

    // Diğer metodlar benzer şekilde in-memory davranır
    public void Update(Kitap entity) { }
    public void Delete(Kitap entity) => _data.Remove(entity);
    public Task SaveAsync()          => Task.CompletedTask;
    public Task<IList<Kitap>> GetKategoriyleAsync(string k) => Task.FromResult<IList<Kitap>>(new List<Kitap>());
    public Task<Kitap?> GetDetayliAsync(int id) => GetByIdAsync(id);
}

// Test:
// var repo    = new FakeKitapRepository();
// var uow     = new FakeUnitOfWork(repo);
// var ctrl    = new KitapController(uow);
// var result  = await ctrl.Index();
// → DB yok, gerçek EF Core yok, saniyeler içinde çalışır
```

---

## 7. Özet

```
Repository Pattern
  IRepository<T>       → generic CRUD arayüzü
  IKitapRepository     → entity'ye özel sorgular
  EfKitapRepository    → EF Core ile gerçek uygulama
  FakeKitapRepository  → testlerde DB'siz çalışma

Unit of Work
  Tek DbContext tüm repository'lere paylaşılır
  SaveAsync() → tek transaction
  DI: Scoped olmalı (Transient/Singleton değil)

Kazanım
  Controller → sadece arayüzü bilir
  Test       → FakeRepository ile DB gerektirmez
  Değişim    → EF Core → Dapper geçişi controller'a dokunmaz
```

---

## Sonraki Gün

Gün 35'te CQRS (Command Query Responsibility Segregation) ve MediatR: okuma ile yazma modellerini ayırma, handler yapısı, request/response akışı.
