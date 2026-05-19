using KitabeviOnion.Application.Interfaces;
using KitabeviOnion.Application.UseCases.KitapListele;
using KitabeviOnion.Application.UseCases.KitapSil;
using KitabeviOnion.Application.UseCases.SiparisOlustur;
using KitabeviOnion.Domain.Interfaces;
using KitabeviOnion.Infrastructure.Persistence;
using KitabeviOnion.Infrastructure.Persistence.Repositories;
using KitabeviOnion.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=kitabevi.db"));
//                 ↑ Faz2'de SQL Server vardı, burada SQLite — Domain ve Application değişmedi

// Repository registrations
builder.Services.AddScoped<IKitapRepository, KitapRepository>();
//                          ↑ interface       → implementation
//                            Domain         → Infrastructure
//                            bunu yazmasaydık → DI container IKitapRepository'yi çözemezdi

builder.Services.AddScoped<ISiparisRepository, SiparisRepository>();

// Application services
builder.Services.AddScoped<IEmailService, EmailService>();
//                          ↑ Application interface → Infrastructure implementation

// Handlers
builder.Services.AddScoped<KitapListeleHandler>();
builder.Services.AddScoped<KitapSilHandler>();
builder.Services.AddScoped<SiparisOlusturHandler>();
// ↑ MediatR yok — elle register ettik, her handler sınıfını açıkça görüyoruz
//   bunu yazmasaydık → Controller inject edemez, 500 hata alırdık

builder.Services.AddControllers();

var app = builder.Build();

// Migration — startup'ta otomatik uygula (dev ortamı için)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    //          ↑ production'da Migrate() kullanılır, burada hızlı başlatma
}

app.MapControllers();
app.Run();
