// Gün 7 — Hafta 1 Özet: Hepsini Bir Arada Gösteren Şablon
// Bu dosya bir web API katmanının iskeletini simüle eder.
// Faz 2'de bunları gerçek ASP.NET Core projesiyle yapacağız.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// ============================================================
// Simülasyon: controller → service → repository zinciri
// ============================================================

var db = new FakeDbContext();
var repository = new KitapRepository(db);
var service = new KitapService(repository);

var cts = new CancellationTokenSource();

// Controller gibi davranıyoruz
Console.WriteLine("=== GET /kitaplar?maxFiyat=60 ===");
var liste = await service.UcuzKitaplariGetirAsync(maxFiyat: 60, cts.Token);
foreach (var k in liste)
    Console.WriteLine($"  {k}");

Console.WriteLine();

Console.WriteLine("=== POST /kitaplar ===");
var yeniKitap = await service.KitapEkleAsync(
    new CreateKitapRequest("Pragmatic Programmer", "Hunt & Thomas", 55m),
    cts.Token
);
Console.WriteLine($"  Oluşturuldu: {yeniKitap}");

Console.WriteLine();

Console.WriteLine("=== GET /kitaplar/1 ===");
var tekKitap = await service.KitapGetirAsync(1, cts.Token);
Console.WriteLine($"  {tekKitap}");

// ============================================================
// Tip tanımları
// ============================================================

// record — API request/response modelleri (Gün 4)
record CreateKitapRequest(string Baslik, string Yazar, decimal Fiyat);
record KitapResponse(int Id, string Baslik, string Yazar, decimal Fiyat);

// class — entity (EF Core mutable olmasını bekler)
class Kitap
{
    public int Id { get; set; }
    public string Baslik { get; set; } = "";
    public string Yazar { get; set; } = "";
    public decimal Fiyat { get; set; }
}

// ============================================================
// Repository — IDisposable + IQueryable kullanımı (Gün 3, 5)
// ============================================================
class KitapRepository : IDisposable
{
    private readonly FakeDbContext _db;
    private bool _disposed = false;

    public KitapRepository(FakeDbContext db)
    {
        _db = db;
    }

    // IQueryable döndürüyor — servis katmanı filtre ekleyebilir
    // ToList() burada ÇAĞRILMIYOR — sorgu henüz veritabanına gitmiyor
    public IQueryable<Kitap> GetAll() => _db.Kitaplar.AsQueryable();

    public async Task<Kitap?> GetByIdAsync(int id, CancellationToken ct)
    {
        await Task.Delay(20, ct);  // DB gecikmesi simülasyonu
        return _db.Kitaplar.FirstOrDefault(k => k.Id == id);
    }

    public async Task<Kitap> AddAsync(Kitap kitap, CancellationToken ct)
    {
        await Task.Delay(20, ct);
        kitap.Id = _db.Kitaplar.Count + 1;
        _db.Kitaplar.Add(kitap);
        return kitap;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _db.Dispose();  // bağlantıyı kapat (Gün 3)
        _disposed = true;
        Console.WriteLine("  [Repository] Dispose çağrıldı — bağlantı kapatıldı");
    }
}

// ============================================================
// Service — iş mantığı, async/await, CancellationToken (Gün 6)
// ============================================================
class KitapService
{
    private readonly KitapRepository _repository;

    public KitapService(KitapRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<KitapResponse>> UcuzKitaplariGetirAsync(
        decimal maxFiyat,
        CancellationToken ct)
    {
        // IQueryable üzerinde filtre — SQL'e çevrilir (Gün 5)
        // ToList() çağrılana kadar veritabanına gidilmez
        var sorgu = _repository.GetAll()
            .Where(k => k.Fiyat <= maxFiyat)
            .OrderBy(k => k.Baslik);

        // Şimdi veritabanına gidiyoruz
        await Task.Delay(30, ct);  // ToListAsync simülasyonu
        var kitaplar = sorgu.ToList();

        // record ile response dönüştürme (Gün 4)
        return kitaplar
            .Select(k => new KitapResponse(k.Id, k.Baslik, k.Yazar, k.Fiyat))
            .ToList();
    }

    public async Task<KitapResponse> KitapGetirAsync(int id, CancellationToken ct)
    {
        var kitap = await _repository.GetByIdAsync(id, ct);
        if (kitap is null)
            throw new Exception($"Kitap bulunamadı: {id}");

        return new KitapResponse(kitap.Id, kitap.Baslik, kitap.Yazar, kitap.Fiyat);
    }

    public async Task<KitapResponse> KitapEkleAsync(
        CreateKitapRequest request,
        CancellationToken ct)
    {
        var kitap = new Kitap
        {
            Baslik = request.Baslik,
            Yazar = request.Yazar,
            Fiyat = request.Fiyat
        };

        var eklenen = await _repository.AddAsync(kitap, ct);
        return new KitapResponse(eklenen.Id, eklenen.Baslik, eklenen.Yazar, eklenen.Fiyat);
    }
}

// ============================================================
// FakeDbContext — gerçek EF Core DbContext simülasyonu
// ============================================================
class FakeDbContext : IDisposable
{
    public List<Kitap> Kitaplar { get; } = new()
    {
        new Kitap { Id = 1, Baslik = "Clean Code",   Yazar = "Martin",          Fiyat = 45m },
        new Kitap { Id = 2, Baslik = "DDD",           Yazar = "Evans",           Fiyat = 85m },
        new Kitap { Id = 3, Baslik = "SICP",          Yazar = "Abelson",         Fiyat = 30m },
        new Kitap { Id = 4, Baslik = "Refactoring",   Yazar = "Fowler",          Fiyat = 55m },
    };

    public void Dispose()
    {
        Console.WriteLine("  [DbContext] Bağlantı kapatıldı");
    }
}
