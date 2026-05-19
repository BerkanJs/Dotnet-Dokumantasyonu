# Gün 60 — CQRS + MediatR: Onion'a Entegrasyon

Gün 59'da `SiparisOlusturHandler` elle yazıldı, `Program.cs`'de elle register edildi. Küçük projede bu yeterli. Ama 20 use case olunca her handler'ı elle inject etmek hem yorucu hem hataya açık. MediatR bu dispatch sorununu çözüyor.

**CQRS:** Command (yaz) ve Query (oku) ayrı nesneler.  
**MediatR:** Command/Query nesnesini doğru handler'a yönlendiren in-process mediator.  
**Birlikte:** Onion katmanlarını birbirine bağlayan dispatch mekanizması.

---

## Faz2 ile Karşılaştırma

```csharp
// Faz2 — Controller her şeyi biliyor
public class SiparisController : Controller
{
    private readonly SiparisServisi _servis; // doğrudan servis bağımlılığı
    public async Task<IActionResult> Olustur(SiparisViewModel vm)
    {
        await _servis.OlusturAsync(vm.KullaniciId, vm.KitapId, vm.Adet);
        return RedirectToAction("Index");
    }
    // 50 action → 50 servis bağımlılığı — controller şişiyor
}
```

```csharp
// Onion + MediatR — Controller sadece dispatch ediyor
public class SiparisController : ControllerBase
{
    private readonly IMediator _mediator; // tek bağımlılık
    public async Task<IActionResult> Olustur([FromBody] SiparisOlusturCommand cmd)
    {
        var sonuc = await _mediator.Send(cmd);
        return CreatedAtAction(..., sonuc);
    }
    // 50 endpoint → hepsi _mediator.Send() — yeni handler eklemek controller'ı değiştirmiyor
}
```

---

## MediatR Kurulumu

```bash
dotnet add KitabeviOnion.Application package MediatR
dotnet add KitabeviOnion.API package MediatR.Extensions.Microsoft.DependencyInjection
```

---

## 1. IRequest ile Command ve Query

MediatR'da her istek `IRequest<TResponse>` interface'ini implement eder.

### `UseCases/KitapListele/KitapListeleQuery.cs`

```csharp
namespace KitabeviOnion.Application.UseCases.KitapListele;

public record KitapListeleQuery : IRequest<IReadOnlyList<KitapDto>>;
//                                 ↑ "bu isteğin cevabı IReadOnlyList<KitapDto>"
//                                   bunu yazmasaydık → MediatR hangi handler'ı çağıracağını bilemezdi
```

### `UseCases/SiparisOlustur/SiparisOlusturCommand.cs`

```csharp
namespace KitabeviOnion.Application.UseCases.SiparisOlustur;

public record SiparisOlusturCommand(
    string KullaniciId,
    int KitapId,
    int Adet
) : IRequest<SiparisOlusturResult>;
//  ↑ Command → IRequest: MediatR bu nesneyi alır, doğru handler'ı bulur, sonucu döner
//    bunu yazmasaydık → _mediator.Send(cmd) derlenmezdi
```

---

## 2. IRequestHandler ile Handler

Her handler `IRequestHandler<TRequest, TResponse>` implement eder.

### `UseCases/KitapListele/KitapListeleHandler.cs`

**Gün59 ile karşılaştırma — değişen tek şey:** sınıf imzası.

```csharp
namespace KitabeviOnion.Application.UseCases.KitapListele;

public class KitapListeleHandler : IRequestHandler<KitapListeleQuery, IReadOnlyList<KitapDto>>
//                                  ↑ MediatR'ın bilmesi gereken type bilgisi
//                                    "KitapListeleQuery gelince ben handle ederim, IReadOnlyList dönerim"
//                                    bunu yazmasaydık → MediatR bu handler'ı bulamazdı
{
    private readonly IKitapRepository _kitapRepo;

    public KitapListeleHandler(IKitapRepository kitapRepo)
    {
        _kitapRepo = kitapRepo;
    }

    public async Task<IReadOnlyList<KitapDto>> Handle(
        KitapListeleQuery request,  // ← parametre adı MediatR convention'ı
        CancellationToken cancellationToken)
    {
        var kitaplar = await _kitapRepo.TumunuGetirAsync(cancellationToken);

        return kitaplar
            .Select(k => new KitapDto(
                k.Id,
                k.Baslik,
                k.Isbn.Deger,
                k.Fiyat.Deger,
                k.Fiyat.ParaBirimi,
                k.StokAdedi))
            .ToList()
            .AsReadOnly();
    }
}
```

### `UseCases/SiparisOlustur/SiparisOlusturHandler.cs`

```csharp
namespace KitabeviOnion.Application.UseCases.SiparisOlustur;

public class SiparisOlusturHandler : IRequestHandler<SiparisOlusturCommand, SiparisOlusturResult>
{
    private readonly IKitapRepository _kitapRepo;
    private readonly ISiparisRepository _siparisRepo;
    private readonly IEmailService _emailService;
    private readonly IMediator _mediator;
    //               ↑ handler başka event publish edebilmek için mediator tutabilir

    public SiparisOlusturHandler(
        IKitapRepository kitapRepo,
        ISiparisRepository siparisRepo,
        IEmailService emailService,
        IMediator mediator)
    {
        _kitapRepo = kitapRepo;
        _siparisRepo = siparisRepo;
        _emailService = emailService;
        _mediator = mediator;
    }

    public async Task<SiparisOlusturResult> Handle(
        SiparisOlusturCommand request,
        CancellationToken cancellationToken)
    {
        var kitap = await _kitapRepo.BulByIdAsync(request.KitapId, cancellationToken);

        if (kitap is null)
            throw new DomainException($"Kitap bulunamadı: {request.KitapId}");

        kitap.StokAzalt(request.Adet);

        var siparis = new Siparis(request.KullaniciId);
        siparis.KalemEkle(kitap.Id, kitap.Baslik, kitap.Fiyat, request.Adet);
        siparis.Onayla();

        await _siparisRepo.EkleAsync(siparis, cancellationToken);
        await _siparisRepo.KaydetAsync(cancellationToken);

        // Domain event'leri publish et
        foreach (var domainEvent in siparis.DomainEvents)
            await _mediator.Publish(new SiparisOlusturulduNotification(domainEvent), cancellationToken);
        //                          ↑ her event → ilgilenen tüm handler'lara dağıtılır
        //                            bunu yazmasaydık → email gönderme burada inline kalırdı,
        //                            yeni alıcı eklemek handler'ı değiştirirdi

        return new SiparisOlusturResult(siparis.Id, siparis.ToplamTutar());
    }
}
```

---

## 3. INotification ile Domain Event Dispatch

```csharp
// Application/Notifications/SiparisOlusturulduNotification.cs
namespace KitabeviOnion.Application.Notifications;

public record SiparisOlusturulduNotification(string EventData) : INotification;
//                                                                 ↑ IRequest değil INotification
//                                                                   fark: birden fazla handler olabilir
//                                                                   bunu yazmasaydık → Publish() derlenmezdi
```

### Email Handler

```csharp
// Application/Notifications/SiparisEmailHandler.cs
namespace KitabeviOnion.Application.Notifications;

public class SiparisEmailHandler : INotificationHandler<SiparisOlusturulduNotification>
//                                  ↑ INotificationHandler — sadece dinliyor, cevap dönmüyor
{
    private readonly IEmailService _emailService;

    public SiparisEmailHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task Handle(SiparisOlusturulduNotification notification, CancellationToken ct)
    {
        // EventData parse: "SiparisOnaylandi:42:kullanici@mail.com"
        var parcalar = notification.EventData.Split(':');
        var siparisId = parcalar[1];
        var kullaniciId = parcalar[2];

        await _emailService.GonderAsync(
            alici: kullaniciId,
            konu: "Siparişiniz Alındı",
            govde: $"Sipariş #{siparisId} başarıyla oluşturuldu.",
            ct: ct);
    }
}
```

### Bildirim Handler (yeni alıcı — handler değişmedi)

```csharp
// Application/Notifications/SiparisBildirimHandler.cs
public class SiparisBildirimHandler : INotificationHandler<SiparisOlusturulduNotification>
{
    private readonly ILogger<SiparisBildirimHandler> _logger;

    public SiparisBildirimHandler(ILogger<SiparisBildirimHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(SiparisOlusturulduNotification notification, CancellationToken ct)
    {
        _logger.LogInformation("Yeni sipariş bildirimi: {Data}", notification.EventData);
        return Task.CompletedTask;
    }
}
// ↑ yeni handler eklendi — SiparisOlusturHandler'a tek satır dokunmadık
//   50k'da bu güç önemli: yeni bildirim kanalı (SMS, push) → yeni handler, mevcut kod değişmez
```

---

## 4. Pipeline Behavior: Cross-Cutting Concerns

MediatR'ın en güçlü özelliği: her request geçmeden önce çalışan middleware.

### `Behaviors/LoggingBehavior.cs`

```csharp
namespace KitabeviOnion.Application.Behaviors;

public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
//    ↑ generic: her request/response tipi için çalışır
//      bunu yazmasaydık → her handler'a ayrı logging yazmak zorunda kalırdık
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next, // ← sonraki pipeline adımı
        CancellationToken ct)
    {
        var requestAdi = typeof(TRequest).Name;

        _logger.LogInformation("→ {Request} başladı: {@Payload}", requestAdi, request);

        var sw = Stopwatch.StartNew();
        var response = await next();
        //                    ↑ bir sonraki behavior veya asıl handler çağrılıyor
        //                      bunu yazmasaydık → request handler'a hiç ulaşmazdı
        sw.Stop();

        _logger.LogInformation("← {Request} tamamlandı: {Ms}ms", requestAdi, sw.ElapsedMilliseconds);

        return response;
    }
}
```

### `Behaviors/ValidationBehavior.cs`

```csharp
namespace KitabeviOnion.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    //               ↑ FluentValidation validator'ları inject — birden fazla olabilir

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!_validators.Any())
            return await next();
        //                ↑ bu request için validator yoksa direkt geç

        var context = new ValidationContext<TRequest>(request);

        var hatalar = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToList();

        if (hatalar.Any())
            throw new ValidationException(hatalar);
        //  ↑ handler'a ulaşmadan validation hatası fırlar
        //    bunu yazmasaydık → her handler başında if-if-if yazardık

        return await next();
    }
}
```

### `Validators/SiparisOlusturCommandValidator.cs`

```csharp
namespace KitabeviOnion.Application.Validators;

public class SiparisOlusturCommandValidator : AbstractValidator<SiparisOlusturCommand>
{
    public SiparisOlusturCommandValidator()
    {
        RuleFor(x => x.KullaniciId)
            .NotEmpty().WithMessage("Kullanıcı Id boş olamaz");

        RuleFor(x => x.KitapId)
            .GreaterThan(0).WithMessage("Geçerli bir kitap seçilmeli");

        RuleFor(x => x.Adet)
            .InclusiveBetween(1, 10).WithMessage("Adet 1 ile 10 arasında olmalı");
        // ↑ iş kuralı gibi görünse de bu "input validation" — domain kuralı değil
        //   Domain: stok < adet → hata (veri tabanına bakarak)
        //   Validation: adet <= 0 → girmeden önce reddet (DB'ye gitme)
    }
}
```

---

## 5. Güncellenmiş Controller

```csharp
namespace KitabeviOnion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SiparisController : ControllerBase
{
    private readonly IMediator _mediator;
    //               ↑ tek bağımlılık — 50 endpoint olsa da hep aynı

    public SiparisController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Olustur(
        [FromBody] SiparisOlusturCommand cmd,
        CancellationToken ct)
    {
        // ValidationBehavior zaten çalıştı — geldiyse geçerli demek
        var sonuc = await _mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(Olustur), new { id = sonuc.SiparisId }, sonuc);
    }
    // ↑ try/catch yok — exception middleware halleder (sonraki ders)
}

[ApiController]
[Route("api/[controller]")]
public class KitapController : ControllerBase
{
    private readonly IMediator _mediator;

    public KitapController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Listele(CancellationToken ct)
        => Ok(await _mediator.Send(new KitapListeleQuery(), ct));
    //                         ↑ tek satır — tüm logic handler'da
}
```

---

## 6. Program.cs — Tümünü Bağlama

```csharp
var builder = WebApplication.CreateBuilder(args);

// MediatR — Application assembly'sindeki tüm handler'ları otomatik bul
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(SiparisOlusturHandler).Assembly);
    //                                ↑ Assembly scan: IRequestHandler implement edenleri bul
    //                                  bunu yazmasaydık → her handler'ı AddScoped ile elle register etmek zorunda kalırdık

    // Pipeline sırası önemli: Logging → Validation → Handler
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// FluentValidation — Application assembly'sindeki tüm validator'ları bul
builder.Services.AddValidatorsFromAssembly(typeof(SiparisOlusturCommandValidator).Assembly);
//              ↑ bunu yazmasaydık → ValidationBehavior IValidator inject edemez, boş liste alırdı

// Infrastructure
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=kitabevi.db"));

builder.Services.AddScoped<IKitapRepository, KitapRepository>();
builder.Services.AddScoped<ISiparisRepository, SiparisRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Global exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
//               ↑ RFC 7807 Problem Details formatında hata dön — Controller try/catch yok

builder.Services.AddControllers();

var app = builder.Build();

app.UseExceptionHandler();
//  ↑ GlobalExceptionHandler devreye girer
app.MapControllers();
app.Run();
```

---

## 7. Global Exception Handler

```csharp
// API/Middleware/GlobalExceptionHandler.cs
namespace KitabeviOnion.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx,
        Exception exception,
        CancellationToken ct)
    {
        var (statusCode, baslik) = exception switch
        {
            DomainException     => (400, "İş Kuralı Hatası"),
            ValidationException => (422, "Doğrulama Hatası"),
            _                   => (500, "Sunucu Hatası")
        };
        // ↑ switch expression: exception tipine göre HTTP kodu
        //   bunu yazmasaydık → her Controller ayrı try/catch yazardı, kod tekrarı olurdu

        if (statusCode == 500)
            _logger.LogError(exception, "Beklenmedik hata");

        ctx.Response.StatusCode = statusCode;

        await ctx.Response.WriteAsJsonAsync(new
        {
            tip = exception.GetType().Name,
            baslik,
            mesaj = exception.Message,
            // 500 hatalarında stack trace döndürme — güvenlik
            detay = statusCode < 500 ? exception.Message : null
        }, ct);

        return true;
        // ↑ true: "ben hallettim, başkası bakmasın"
    }
}
```

---

## Request Akışı — Özet

```
HTTP POST /api/siparis
    │
    ▼
SiparisController.Olustur()
    │  _mediator.Send(cmd)
    ▼
LoggingBehavior         ← "→ SiparisOlusturCommand başladı"
    │  next()
    ▼
ValidationBehavior      ← KullaniciId boş mu? Adet 1-10 arası mı?
    │  next()  (başarılıysa)
    ▼
SiparisOlusturHandler
    │  kitap.StokAzalt()    ← Domain kuralı
    │  siparis.Onayla()     ← Domain kuralı + event topla
    │  SaveChanges()
    │  mediator.Publish()   ← SiparisOlusturulduNotification
    │      ├─ SiparisEmailHandler    ← email gönder
    │      └─ SiparisBildirimHandler ← log yaz
    ▼
SiparisOlusturResult
    │
    ▼
LoggingBehavior         ← "← SiparisOlusturCommand tamamlandı: 43ms"
    │
    ▼
201 Created { siparisId, toplamTutar }
```

---

## 500 vs 50k Kullanıcı

| Konu | 500 | 50k |
|---|---|---|
| **MediatR gerekli mi?** | Hayır — elle register yeterli | ✅ Handler sayısı arttıkça şart |
| **Pipeline Behavior** | Overkill — inline logging yaz | ✅ Cross-cutting concerns merkezi |
| **INotification / Publish** | Tek alıcıysa direkt çağır | ✅ Loose coupling, yeni alıcı kolayca eklenir |
| **FluentValidation + Behavior** | DataAnnotations yeterli | ✅ Karmaşık kural, cross-field validation |
| **GlobalExceptionHandler** | Her Controller try/catch | ✅ Tek yer — tutarlı hata formatı |

---

## Sorular

1. `IRequest<TResponse>` ile `INotification` farkı ne? Ne zaman hangisi?
2. Pipeline Behavior'da `next()` çağrılmazsa ne olur?
3. `ValidationBehavior` domain kuralını da koyabilir miyiz? Neden koymamalıyız?
