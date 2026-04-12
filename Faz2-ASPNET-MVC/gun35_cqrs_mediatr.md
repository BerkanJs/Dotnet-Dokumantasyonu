# Gün 35 — CQRS ve MediatR

---

## 1. CQRS Nedir?

**Command Query Responsibility Segregation** — okuma (Query) ve yazma (Command) operasyonlarını birbirinden ayırır.

```
Klasik yaklaşım — tek servis her şeyi yapar:
  IKitapServisi
    GetAllAsync()      → okuma
    GetByIdAsync()     → okuma
    AddAsync()         → yazma
    UpdateAsync()      → yazma
    DeleteAsync()      → yazma

Problem: servis büyüdükçe, okuma ve yazma ihtiyaçları birbirine karışır.
  Okuma: AsNoTracking, DTO döndür, tek tablo bile yetebilir
  Yazma: Validation, transaction, event fırlatma, audit log

CQRS yaklaşımı — iki ayrı yol:
  Query  → sadece okur, veri değiştirmez
  Command → veri değiştirir, sonuç döndürmesi gerekmez (ya da sadece Id döndürür)
```

```
Akış:

Controller → MediatR.Send(KitapListeQuery)
                  ↓
          KitapListeQueryHandler.Handle()
                  ↓
          IList<KitapDto> döner → View'a geçer

Controller → MediatR.Send(KitapEkleCommand)
                  ↓
          KitapEkleCommandHandler.Handle()
                  ↓
          Validation + SaveChanges + int(yeni Id) döner
```

---

## 2. MediatR Kurulumu

MediatR, mesaj tabanlı iletişim için bir in-process mediator kütüphanesidir. Controller "kime gideceğini" bilmez; MediatR doğru handler'ı bulur.

```bash
dotnet add package MediatR
# MediatR.Extensions.Microsoft.DependencyInjection artık MediatR içinde gömülü (v12+)
```

```csharp
// Program.cs

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
// Assembly taraması: aynı projede IRequestHandler implement eden tüm sınıfları bulur ve kaydeder
// RegisterServicesFromAssembly yazmasaydık: handler'lar DI'a kayıtlı olmaz,
// MediatR "handler bulunamadı" exception fırlatırdı
```

---

## 3. Query — Okuma Tarafı

```csharp
// Features/Kitaplar/KitapListeQuery.cs
// IRequest<T>: bu mesaj gönderilince T tipinde cevap bekliyoruz

public record KitapListeQuery(string? Kategori = null) : IRequest<IList<KitapDto>>;
// record: immutable, value-equality — query nesneleri değişmemeli
// string? Kategori: isteğe bağlı filtre parametresi
// bunu class yazsaydık: GetHashCode/Equals override etmek zorunda kalırdık
```

```csharp
// Features/Kitaplar/KitapDto.cs
// Sadece view'a gereken alanlar — entity doğrudan döndürme

public record KitapDto(int Id, string Baslik, string Yazar, decimal Fiyat, int StokAdedi);
// Entity döndürseydi: navigation property'ler serialization sırasında döngüye girebilir
// ve view'a gereksiz alanlar (EklemeTarihi, silindi bayrağı vb.) de giderdi
```

```csharp
// Features/Kitaplar/KitapListeQueryHandler.cs

public class KitapListeQueryHandler : IRequestHandler<KitapListeQuery, IList<KitapDto>>
{
    private readonly KitabeviDbContext _context;

    public KitapListeQueryHandler(KitabeviDbContext context)
    {
        _context = context;
        // Repository pattern ile birlikte kullanabilirsin ama query tarafında
        // doğrudan DbContext kullanmak da geçerli — query'ler basit, test gereksiz
    }

    public async Task<IList<KitapDto>> Handle(KitapListeQuery request, CancellationToken ct)
    {
        var sorgu = _context.Kitaplar.AsNoTracking();
        // AsNoTracking: sadece okuyoruz, Change Tracker'a gerek yok
        // yazmasaydık: her sonuç nesnesi tracked → bellek + CPU gereksiz harcar

        if (!string.IsNullOrEmpty(request.Kategori))
            sorgu = sorgu.Where(k => k.Kategori == request.Kategori);
            // filtre koşullu: null gelirse tüm kitaplar, değer gelirse filtreli

        return await sorgu
            .OrderBy(k => k.Baslik)
            .Select(k => new KitapDto(k.Id, k.Baslik, k.Yazar, k.Fiyat, k.StokAdedi))
            // Select: sadece ihtiyaç duyulan kolonlar SQL'e girer
            // Select yazmasaydık: SELECT * → gereksiz kolon transferi
            .ToListAsync(ct);
            // ct (CancellationToken): kullanıcı isteği iptal ederse sorgu durdurulur
    }
}
```

---

## 4. Command — Yazma Tarafı

```csharp
// Features/Kitaplar/KitapEkleCommand.cs

public record KitapEkleCommand(
    string Baslik,
    string Yazar,
    decimal Fiyat,
    string Kategori,
    int StokAdedi
) : IRequest<int>;  // int: yeni kitabın Id'si döner
// IRequest<Unit> yazmak da mümkün — Unit = "sonuç yok" anlamında MediatR'ın void'i
```

```csharp
// Features/Kitaplar/KitapEkleCommandHandler.cs

public class KitapEkleCommandHandler : IRequestHandler<KitapEkleCommand, int>
{
    private readonly KitabeviDbContext _context;

    public KitapEkleCommandHandler(KitabeviDbContext context)
        => _context = context;

    public async Task<int> Handle(KitapEkleCommand request, CancellationToken ct)
    {
        var kitap = new Kitap
        {
            Baslik       = request.Baslik,
            Yazar        = request.Yazar,
            Fiyat        = request.Fiyat,
            Kategori     = request.Kategori,
            StokAdedi    = request.StokAdedi,
            EklemeTarihi = DateTime.UtcNow
        };

        _context.Kitaplar.Add(kitap);
        await _context.SaveChangesAsync(ct);
        // SaveChangesAsync(ct): ct ile iptal edilebilir
        // SaveChanges() yazsaydık: iş parçacığını bloke eder, async yarar azalır

        return kitap.Id;
        // EF Core SaveChanges sonrası Id'yi doldurur — biz okuyoruz, atamıyoruz
        // SaveChanges öncesi kitap.Id okusaydık: 0 dönerdi
    }
}
```

---

## 5. Controller — MediatR ile İnce Yapı

```csharp
// Controllers/KitapController.cs

public class KitapController : Controller
{
    private readonly IMediator _mediator;

    public KitapController(IMediator mediator)
    {
        _mediator = mediator;
        // Controller handler'ları, servisleri, context'i bilmiyor
        // sadece "bu mesajı gönder" diyor
        // bağımlılık sayısı düşer: tek IMediator
    }

    public async Task<IActionResult> Index(string? kategori)
    {
        var kitaplar = await _mediator.Send(new KitapListeQuery(kategori));
        // MediatR uygun handler'ı (KitapListeQueryHandler) bulur ve çalıştırır
        // handler kayıtlı değilse: InvalidOperationException fırlatır
        return View(kitaplar);
    }

    [HttpGet]
    public IActionResult Ekle() => View();

    [HttpPost]
    public async Task<IActionResult> Ekle(KitapEkleCommand command)
    {
        if (!ModelState.IsValid) return View(command);

        var yeniId = await _mediator.Send(command);
        // command doğrudan form'dan bind edilir (property adları eşleşiyor)
        // bunu record yapmasaydık: ModelBinder primary constructor'ı bind edemeyebilir
        // .NET 8+ record bağlama destekliyor

        return RedirectToAction(nameof(Index));
    }
}
```

---

## 6. Pipeline Behaviour — Cross-Cutting Concerns

MediatR, her request'i pipeline'dan geçirir. Validation veya logging'i her handler'a yazmak yerine bir kez yazarsın.

```csharp
// Behaviours/LoggingBehaviour.cs
// Her Send() çağrısında otomatik devreye girer

public class LoggingBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,  // sonraki pipeline adımı (gerçek handler)
        CancellationToken ct)
    {
        var ad = typeof(TRequest).Name;

        _logger.LogInformation("→ {Request}", ad);
        // bunu yazmadan handler içine loglama koysaydık: her handler ayrı log kodu

        var sw = Stopwatch.StartNew();
        var result = await next();          // asıl handler çalışır
        sw.Stop();

        _logger.LogInformation("← {Request} {Ms}ms", ad, sw.ElapsedMilliseconds);
        // yavaş sorgu tespiti: bu değeri threshold ile karşılaştırabilirsin

        return result;
    }
}
```

```csharp
// Program.cs — behaviour kaydı

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
    // Generic open type: tüm request/response çiftleri için LoggingBehaviour devreye girer
    // Sıra önemli: birden fazla behaviour varsa kayıt sırasına göre pipeline oluşur
});
```

---

## 7. Klasör Yapısı

```
Features/
  Kitaplar/
    KitapListeQuery.cs
    KitapListeQueryHandler.cs
    KitapDto.cs
    KitapEkleCommand.cs
    KitapEkleCommandHandler.cs
    KitapGuncelleCommand.cs
    KitapGuncelleCommandHandler.cs
    KitapSilCommand.cs
    KitapSilCommandHandler.cs
Behaviours/
  LoggingBehaviour.cs
  ValidationBehaviour.cs   (ileride FluentValidation ile)
```

```
Her feature kendi klasöründe → yeni özellik eklemek mevcut kodu değiştirmez
Controller şişmez → sadece Send() çağrısı
Handler'lar küçük ve tek sorumlu → test yazmak kolay
```

---

## 8. Özet

```
CQRS
  Query  → okuma, AsNoTracking, DTO döndür
  Command → yazma, validation, SaveChanges, Id döndür

MediatR
  IRequest<T>          → mesaj tanımı (Query veya Command)
  IRequestHandler<,>   → mesajı işleyen sınıf
  _mediator.Send()     → handler'ı bul ve çalıştır
  IPipelineBehavior    → logging/validation tüm handler'larda otomatik

DI Kaydı
  AddMediatR(cfg => cfg.RegisterServicesFromAssembly(...))
  Behaviour: cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>))
```

---

## Sonraki Gün

Gün 36'da Serilog ile yapılandırılmış loglama: structured logging, sink'ler (dosya, konsol, seq), log level yönetimi ve request/response loglama.
