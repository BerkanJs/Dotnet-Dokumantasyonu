using KitabeviMVC.Data;
using KitabeviMVC.Models.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KitabeviMVC.Tests.Infrastructure;

// ─────────────────────────────────────────────────────────────────────────────
// TestWebApplicationFactory — Yeniden kullanılabilir test host yapılandırması
//
// Neden ayrı factory sınıfı?
//   Her test class'ında WithWebHostBuilder kodu tekrarlamak yerine
//   merkezi bir sınıfta yapılandırma yapılır.
//   Yapılandırma değişince tek yer güncellenmiş olur.
//
// Nasıl çalışır?
//   WebApplicationFactory<Program>: Program.cs'teki tüm konfigürasyonu devralır,
//   ConfigureWebHost ile üzerine yazabilirsin.
//   public partial class Program {} → Program.cs'te tanımlı (test projesine görünür).
// ─────────────────────────────────────────────────────────────────────────────
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // "Testing" ortamı:
        //   Program.cs'te: if (!app.Environment.IsEnvironment("Testing")) bloğu Hangfire job'larını atlar.
        //   Hangfire statik API (RecurringJob.AddOrUpdate) test ortamında JobStorage initialize edilmeden
        //   çağrılırsa exception fırlatır.
        // Bunu yazmasaydık: test host başlarken Hangfire hatası alırdık.

        builder.ConfigureServices(services =>
        {
            // ── Gerçek DbContext kaydını kaldır ──────────────────────────────────
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<KitabeviDbContext>));
            // SingleOrDefault: kayıt yoksa null döner; exception fırlatmaz.
            // Bunu yazmassaydık iki DbContext kaydı çakışırdı (InMemory + mevcut).

            if (descriptor is not null)
                services.Remove(descriptor);
            // Mevcut kaydı kaldır — InMemory ile değiştireceğiz.

            // ── InMemory DB ekle ─────────────────────────────────────────────────
            services.AddDbContext<KitabeviDbContext>(opts =>
                opts.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));
            // Guid.NewGuid(): her factory instance için benzersiz DB adı.
            // Sabit "TestDb" yazmak: birden fazla test class çalışırken veri çakışması.
            // IClassFixture sayesinde tek factory oluşturulur → birden fazla test aynı DB'yi paylaşır.
            // Bu kabul edilebilir çünkü testler yazma yapıyorsa sıra bağımlılığı sorun olabilir.
            // Çözüm: her test kendi verisini seed'ler ve başka testin verisine bağımlı olmaz.

            // ── Seed verisi ekle ─────────────────────────────────────────────────
            var sp = services.BuildServiceProvider();
            // BuildServiceProvider: DI container'ı derle — henüz uygulamada değil, test setup'ında.
            // Bunu yazmassaydık GetRequiredService çağıramayız, DbContext'e ulaşamayız.

            using var scope = sp.CreateScope();
            // using: scope dispose edilir → DbContext de dispose edilir.
            // Bunu yazmassaydık: DbContext kaynak sızıntısı.

            var db = scope.ServiceProvider.GetRequiredService<KitabeviDbContext>();
            db.Database.EnsureCreated();
            // EnsureCreated: InMemory DB için şema oluşturur (migration uygulanmaz).
            // SQL Server'da Database.Migrate() kullanılır.
            // Bunu yazmassaydık: tablo yok, sorgu exception fırlatır.

            if (!db.Kitaplar.Any())
            // Idempotent: zaten seed yapılmışsa tekrar yapma.
            // Factory birden fazla test class'ı tarafından paylaşılabilir;
            // her seferinde aynı seed tekrar çalışmamalı.
            {
                db.Kitaplar.AddRange(
                    new Kitap
                    {
                        Id        = 1,
                        Baslik    = "Clean Code",
                        Yazar     = "Robert C. Martin",
                        Fiyat     = 120m,
                        Kategori  = "Yazılım",
                        StokAdedi = 10
                    },
                    new Kitap
                    {
                        Id        = 2,
                        Baslik    = "Domain-Driven Design",
                        Yazar     = "Eric Evans",
                        Fiyat     = 150m,
                        Kategori  = "Yazılım",
                        StokAdedi = 5
                    },
                    new Kitap
                    {
                        Id        = 3,
                        Baslik    = "Sapiens",
                        Yazar     = "Yuval Noah Harari",
                        Fiyat     = 85m,
                        Kategori  = "Tarih",
                        StokAdedi = 20
                    }
                );
                db.SaveChanges();
                // Senkron SaveChanges: ConfigureWebHost async değil, await kullanamayız.
                // SaveChangesAsync().GetAwaiter().GetResult() da çalışır ama deadlock riski.
            }
        });
    }
}
