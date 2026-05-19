using FluentValidation;
using KitabeviMediatr.Application.Behaviors;
using KitabeviMediatr.Application.Interfaces;
using KitabeviMediatr.Application.UseCases.SiparisOlustur;
using KitabeviMediatr.Application.Validators;
using KitabeviMediatr.Domain.Interfaces;
using KitabeviMediatr.Infrastructure.Persistence;
using KitabeviMediatr.Infrastructure.Persistence.Repositories;
using KitabeviMediatr.Infrastructure.Services;
using KitabeviMediatr.API.Middleware;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MediatR — Application assembly'sindeki tüm handler'ları otomatik bul
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(SiparisOlusturHandler).Assembly);
    //                                ↑ Assembly scan: IRequestHandler implement edenleri bul
    //                                  Gün59'da: AddScoped<KitapListeleHandler>() elle yazılıyordu
    //                                  Gün60'da: yeni handler eklemek bu satırı değiştirmiyor

    // Pipeline sırası önemli: Logging → Validation → Handler
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    //  ↑ sıra: Logging önce — isteği loglar, sonra validation
    //    bunu tersine yazsaydık → validation hatası loglanmadan fırlatılırdı
});

// FluentValidation — Application assembly'sindeki tüm validator'ları bul
builder.Services.AddValidatorsFromAssembly(typeof(SiparisOlusturCommandValidator).Assembly);
//              ↑ bunu yazmasaydık → ValidationBehavior IValidator inject edemez, boş liste alırdı
//                yeni validator eklemek bu satırı değiştirmiyor

// Infrastructure
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=kitabevi.db"));

// Cache
builder.Services.AddMemoryCache();
//               ↑ IMemoryCache DI container'a kaydet — olmadan CachingKitapRepository inject edilemez

// KitapRepository — Decorator zinciri: Handler → Logging → Cache → DB
builder.Services.AddScoped<KitapRepository>();
//               ↑ interface değil concrete tip — decorator içine inject edilecek
//                 bunu yazmasaydık → sp.GetRequiredService<KitapRepository>() hata verirdi

builder.Services.AddScoped<IKitapRepository>(sp =>
{
    var dbRepo = sp.GetRequiredService<KitapRepository>();
    //             ↑ en içteki: gerçek DB katmanı

    var cache   = sp.GetRequiredService<IMemoryCache>();
    var logger  = sp.GetRequiredService<ILogger<LoggingKitapRepository>>();

    var cachingRepo = new CachingKitapRepository(dbRepo, cache);
    //                                             ↑ DB'yi sar: cache katmanı

    var loggingRepo = new LoggingKitapRepository(cachingRepo, logger);
    //                                             ↑ cache'i sar: logging katmanı

    return loggingRepo;
    // Zincir: Handler → LoggingRepo → CachingRepo → KitapRepository (DB)
    // bunu yazmasaydık → IKitapRepository resolve edilince düz KitapRepository gelirdi, cache yok
});

builder.Services.AddScoped<ISiparisRepository, SiparisRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Global exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
//               ↑ RFC 7807 Problem Details formatında hata dön — Controller try/catch yok
//                 Gün59'da: her Controller kendi catch bloğuyla hadle ediyordu

builder.Services.AddControllers();

var app = builder.Build();

app.UseExceptionHandler();
//  ↑ GlobalExceptionHandler devreye girer
//    bunu yazmasaydık → exception middleware çalışmaz, handler register edilse de kullanılmazdı

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapControllers();
app.Run();
