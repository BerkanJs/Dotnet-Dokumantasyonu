using KitabeviMVC.Data;
using KitabeviMVC.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KitabeviMVC.Services;

// ─────────────────────────────────────────────────────────────────────
// Gün 32: Özel Seed Servisi.
//
// HasData() neden burada değil?
//   → HasData() migration'a gömülür: veri değişince yeni migration gerekir.
//   → HasData() InMemory provider'da çalışmaz → testler ve geliştirme kırılır.
//   → Referans integrity sırasını yönetmek güçleşir (Yazarlar önce, Kitaplar sonra).
//
// DbSeeder neden daha iyi?
//   → Uygulama başlangıcında çalışır, migration'dan bağımsız.
//   → AnyAsync() kontrolü → idempotent (kaç kez çalışırsa çalışsın, duplicate yok).
//   → Ortama göre farklı veri: Development'ta 50 kitap, Staging'de 5 kitap.
//   → DI üzerinden gelir → test edilebilir (mock context geçirilebilir).
//
// DI kaydı: Scoped — DbContext Scoped olduğu için bu servis de Scoped OLMAK ZORUNDA.
// Singleton yapılsaydı: "cannot consume scoped service from singleton" hatası alınırdı.
// ─────────────────────────────────────────────────────────────────────
public class DbSeeder
{
    private readonly KitabeviDbContext _context;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(KitabeviDbContext context, ILogger<DbSeeder> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task SeedAsync()
    {
        // ─────────────────────────────────────────────────────────────
        // Adım 1: Yazarlar — Kitaplar YazarId FK'ına bağlı olduğu için
        // Yazarlar tablosu ÖNCE doldurulmalı.
        // Sırayı tersine çevirseydik: Kitap INSERT sırasında YazarId
        // çözümsüz kalır → FK constraint hatası (SQL Server'da).
        // InMemory'de FK kontrolü yok ama alışkanlık olarak sıra korunur.
        // ─────────────────────────────────────────────────────────────
        if (!await _context.Yazarlar.AnyAsync())
        // AnyAsync: "tabloda herhangi bir satır var mı?"
        // SQL: SELECT CASE WHEN EXISTS (SELECT 1 FROM Yazarlar) THEN 1 ELSE 0
        // bunu yazmadan AddRange yapsaydık her uygulama başlangıcında
        // aynı yazarlar tekrar eklenirdi → duplicate kayıt veya unique constraint hatası
        {
            var yazarlar = new List<Yazar>
            {
                new() { Id = 1, Ad = "Robert",  Soyad = "Martin", Biyografi = "Clean Code ve Clean Architecture yazarı." },
                new() { Id = 2, Ad = "Eric",    Soyad = "Evans",  Biyografi = "Domain-Driven Design kitabının yazarı."   },
                new() { Id = 3, Ad = "Andrew",  Soyad = "Hunt",   Biyografi = "The Pragmatic Programmer yazarlarından."  },
                new() { Id = 4, Ad = "Fyodor",  Soyad = "Dostoyevski", Biyografi = "Rus edebiyatının büyük ustası."     },
                new() { Id = 5, Ad = "Yuval",   Soyad = "Harari", Biyografi = "Sapiens ve Homo Deus yazarı."            }
            };

            await _context.Yazarlar.AddRangeAsync(yazarlar);
            // AddRangeAsync: tek tek Add() yerine toplu ekleme
            // her entity State: Added → SaveChanges ile hepsi tek transaction'da INSERT

            await _context.SaveChangesAsync();
            // bunu yazmadan Kitaplar AddRange'e geçseydik:
            // InMemory'de sorun olmaz ama SQL Server'da YazarId FK'ı
            // henüz DB'de olmayan yazarlara işaret eder → hata

            _logger.LogInformation(
                "Seed: {Adet} yazar eklendi.", yazarlar.Count);
        }
        else
        {
            _logger.LogDebug("Seed: Yazarlar tablosu dolu, atlandı.");
            // LogDebug → sadece Debug seviyesinde görünür
            // LogInformation yazsaydık her uygulama başlangıcında log kirliliği olurdu
        }

        // ─────────────────────────────────────────────────────────────
        // Adım 2: Kitaplar — YazarId artık güvenle set edilebilir
        // ─────────────────────────────────────────────────────────────
        if (!await _context.Kitaplar.AnyAsync())
        {
            var kitaplar = new List<Kitap>
            {
                new() { Id = 1, Baslik = "Clean Code",           YazarId = 1, Yazar = "Robert Martin", Fiyat = 45m,  Kategori = "Yazılım", StokAdedi = 10, EklemeTarihi = DateTime.UtcNow },
                // YazarId = 1 → Yazarlar tablosundaki Robert Martin'e işaret eder
                // Yazar string alanı da doluyor → YazarNavigation null olsa bile
                // eski kod (Gün 28 öncesi) kırılmıyor (backward compat. fallback)

                new() { Id = 2, Baslik = "DDD",                  YazarId = 2, Yazar = "Eric Evans",    Fiyat = 85m,  Kategori = "Mimari",  StokAdedi = 5,  EklemeTarihi = DateTime.UtcNow },
                new() { Id = 3, Baslik = "The Pragmatic Prog.",   YazarId = 3, Yazar = "Hunt & Thomas", Fiyat = 55m,  Kategori = "Yazılım", StokAdedi = 8,  EklemeTarihi = DateTime.UtcNow },
                new() { Id = 4, Baslik = "Suç ve Ceza",           YazarId = 4, Yazar = "Dostoyevski",   Fiyat = 89m,  Kategori = "Roman",   StokAdedi = 12, EklemeTarihi = DateTime.UtcNow },
                new() { Id = 5, Baslik = "Sapiens",               YazarId = 5, Yazar = "Harari",        Fiyat = 140m, Kategori = "Tarih",   StokAdedi = 20, EklemeTarihi = DateTime.UtcNow },
                new() { Id = 6, Baslik = "Clean Architecture",    YazarId = 1, Yazar = "Robert Martin", Fiyat = 60m,  Kategori = "Mimari",  StokAdedi = 7,  EklemeTarihi = DateTime.UtcNow },
                // YazarId = 1 tekrar: Robert Martin'in ikinci kitabı — one-to-many ilişki
                new() { Id = 7, Baslik = "Idiot",                 YazarId = 4, Yazar = "Dostoyevski",   Fiyat = 75m,  Kategori = "Roman",   StokAdedi = 0,  EklemeTarihi = DateTime.UtcNow }
                // StokAdedi = 0 → stokta yok; HepsiniGetirAsync'te WHERE StokAdedi > 0 ile filtrelenir
            };

            await _context.Kitaplar.AddRangeAsync(kitaplar);
            await _context.SaveChangesAsync();
            // tüm kitaplar tek transaction'da INSERT edildi
            // biri başarısız olursa hepsi geri alınır (atomicity)

            _logger.LogInformation(
                "Seed: {Adet} kitap eklendi.", kitaplar.Count);
        }
        else
        {
            _logger.LogDebug("Seed: Kitaplar tablosu dolu, atlandı.");
        }
    }
}
