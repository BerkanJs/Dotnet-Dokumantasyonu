// Gün 13 — Dependency Injection: C# Perspektifinden
// Microsoft.Extensions.DependencyInjection paketi gereklidir.
// dotnet add package Microsoft.Extensions.DependencyInjection

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

// ─────────────────────────────────────────────
// Domain + Interface'ler
// ─────────────────────────────────────────────

public class Kitap
{
    public int    Id     { get; init; }
    public string Baslik { get; init; } = "";
    public decimal Fiyat { get; init; }
}

public interface IKitapRepository
{
    Task<List<Kitap>> TumunuGetirAsync();
    Task<Kitap?>      IdIleGetirAsync(int id);
}

public interface ILogServisi
{
    void Log(string mesaj);
}

public interface IOnbellekServisi
{
    string? Get(string anahtar);
    void    Set(string anahtar, string deger);
}

// ─────────────────────────────────────────────
// BÖLÜM 1 — Tight Coupling vs DI
// ─────────────────────────────────────────────

// Kötü — kendi bağımlılığını oluşturuyor
public class KitapServisiBagli
{
    // new ile direkt oluşturma — test edilemez, değiştirilemez
    private readonly KitapRepository _repo = new KitapRepository();

    public async Task<List<Kitap>> GetirAsync() => await _repo.TumunuGetirAsync();
}

// İyi — dışarıdan inject ediliyor
public class KitapServisi
{
    private readonly IKitapRepository _repo;
    private readonly ILogServisi      _log;

    public KitapServisi(IKitapRepository repo, ILogServisi log)
    {
        _repo = repo;
        _log  = log;
    }

    public async Task<List<Kitap>> TumunuGetirAsync()
    {
        _log.Log("Tüm kitaplar getiriliyor");
        return await _repo.TumunuGetirAsync();
    }

    public async Task<Kitap?> IdIleGetirAsync(int id)
    {
        _log.Log($"Kitap getiriliyor: {id}");
        return await _repo.IdIleGetirAsync(id);
    }
}

// ─────────────────────────────────────────────
// BÖLÜM 2 — Implementasyonlar
// ─────────────────────────────────────────────

// Gerçek implementasyon (normalde veritabanına gider)
public class KitapRepository : IKitapRepository
{
    private static readonly List<Kitap> _veri = new()
    {
        new() { Id = 1, Baslik = "Clean Code",   Fiyat = 75m  },
        new() { Id = 2, Baslik = "DDD",          Fiyat = 120m },
        new() { Id = 3, Baslik = "Pragmatic Prog.", Fiyat = 90m },
    };

    public Task<List<Kitap>>  TumunuGetirAsync()   => Task.FromResult(_veri);
    public Task<Kitap?>       IdIleGetirAsync(int id)
        => Task.FromResult(_veri.Find(k => k.Id == id));
}

// Test için kullanılacak fake implementasyon
public class InMemoryKitapRepository : IKitapRepository
{
    private readonly List<Kitap> _veri;
    public InMemoryKitapRepository(List<Kitap> veri) => _veri = veri;

    public Task<List<Kitap>> TumunuGetirAsync()     => Task.FromResult(_veri);
    public Task<Kitap?>      IdIleGetirAsync(int id)
        => Task.FromResult(_veri.Find(k => k.Id == id));
}

// Console log — gerçek implementasyon
public class ConsoleLogServisi : ILogServisi
{
    public void Log(string mesaj)
        => Console.WriteLine($"  [LOG {DateTime.Now:HH:mm:ss}] {mesaj}");
}

// Singleton cache implementasyonu
public class InMemoryOnbellekServisi : IOnbellekServisi
{
    private readonly Dictionary<string, string> _cache = new();

    public string? Get(string anahtar)
        => _cache.TryGetValue(anahtar, out var deger) ? deger : null;

    public void Set(string anahtar, string deger)
        => _cache[anahtar] = deger;
}

// ─────────────────────────────────────────────
// BÖLÜM 3 — Lifetime Farkları
// ─────────────────────────────────────────────

// Nesnenin kaç kez oluşturulduğunu görmek için
public class OmurGostergesi
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Tip { get; init; } = "";
}

public class TransientServis  { public OmurGostergesi Omur { get; } = new() { Tip = "Transient"  }; }
public class ScopedServis     { public OmurGostergesi Omur { get; } = new() { Tip = "Scoped"     }; }
public class SingletonServis  { public OmurGostergesi Omur { get; } = new() { Tip = "Singleton"  }; }

// ─────────────────────────────────────────────
// BÖLÜM 4 — Extension Method Pattern
// ─────────────────────────────────────────────

public static class KitabeviServiceExtensions
{
    // Program.cs'i şişirmek yerine buraya topla
    public static IServiceCollection AddKitabeviServices(
        this IServiceCollection services)
    {
        services.AddScoped<IKitapRepository, KitapRepository>();
        services.AddScoped<KitapServisi>();
        services.AddTransient<ILogServisi, ConsoleLogServisi>();
        services.AddSingleton<IOnbellekServisi, InMemoryOnbellekServisi>();
        return services;
    }
}

// ─────────────────────────────────────────────
// MAIN
// ─────────────────────────────────────────────

public static class Program
{
    public static async Task Main()
    {
        // ── BÖLÜM 1: Tight Coupling vs DI ──────────────────

        Console.WriteLine("=== BÖLÜM 1: Tight Coupling vs Constructor Injection ===\n");

        // Pure DI — container olmadan, elle oluştur
        var repo   = new KitapRepository();
        var log    = new ConsoleLogServisi();
        var servis = new KitapServisi(repo, log);  // bağımlılıklar dışarıdan

        var kitaplar = await servis.TumunuGetirAsync();
        Console.WriteLine($"  Getirilen kitap sayısı: {kitaplar.Count}");

        // Test senaryosu — gerçek repo yerine InMemory geç
        var testVeri = new List<Kitap>
        {
            new() { Id = 99, Baslik = "Test Kitabı", Fiyat = 10m }
        };
        var testRepo   = new InMemoryKitapRepository(testVeri);
        var testServis = new KitapServisi(testRepo, log);  // aynı interface, farklı impl
        var testSonuc  = await testServis.TumunuGetirAsync();
        Console.WriteLine($"  Test repo kitap sayısı: {testSonuc.Count}");

        // ── BÖLÜM 2: Container ile Kayıt ve Çözümleme ──────

        Console.WriteLine("\n=== BÖLÜM 2: DI Container ===\n");

        var services = new ServiceCollection();
        services.AddKitabeviServices();  // extension method ile toplu kayıt

        var provider = services.BuildServiceProvider();

        // Container'dan servis al — container bağımlılıkları kendisi çözdü
        var kitapServisi = provider.GetRequiredService<KitapServisi>();
        var tumKitaplar  = await kitapServisi.TumunuGetirAsync();
        Console.WriteLine($"  Container'dan alınan servis çalıştı. Kitap sayısı: {tumKitaplar.Count}");

        var tekKitap = await kitapServisi.IdIleGetirAsync(2);
        Console.WriteLine($"  Id=2 kitap: {tekKitap?.Baslik ?? "bulunamadı"}");

        // ── BÖLÜM 3: Lifetime Farkları ──────────────────────

        Console.WriteLine("\n=== BÖLÜM 3: Lifetime Farkları ===\n");

        var lifetimeServices = new ServiceCollection();
        lifetimeServices.AddTransient<TransientServis>();
        lifetimeServices.AddScoped<ScopedServis>();
        lifetimeServices.AddSingleton<SingletonServis>();

        var lifetimeProvider = lifetimeServices.BuildServiceProvider();

        // Scope 1 — birinci "request"i simüle eder
        using (var scope1 = lifetimeProvider.CreateScope())
        {
            var t1 = scope1.ServiceProvider.GetRequiredService<TransientServis>();
            var t2 = scope1.ServiceProvider.GetRequiredService<TransientServis>();
            var s1 = scope1.ServiceProvider.GetRequiredService<ScopedServis>();
            var s2 = scope1.ServiceProvider.GetRequiredService<ScopedServis>();
            var sg = scope1.ServiceProvider.GetRequiredService<SingletonServis>();

            Console.WriteLine("  [Scope 1]");
            Console.WriteLine($"    Transient #1 : {t1.Omur.Id}");
            Console.WriteLine($"    Transient #2 : {t2.Omur.Id}  ← farklı (her istekte yeni)");
            Console.WriteLine($"    Scoped    #1 : {s1.Omur.Id}");
            Console.WriteLine($"    Scoped    #2 : {s2.Omur.Id}  ← aynı (scope içinde tek)");
            Console.WriteLine($"    Singleton    : {sg.Omur.Id}");
        }

        // Scope 2 — ikinci "request"i simüle eder
        using (var scope2 = lifetimeProvider.CreateScope())
        {
            var s3 = scope2.ServiceProvider.GetRequiredService<ScopedServis>();
            var sg = scope2.ServiceProvider.GetRequiredService<SingletonServis>();

            Console.WriteLine("\n  [Scope 2]");
            Console.WriteLine($"    Scoped       : {s3.Omur.Id}  ← yeni scope, yeni nesne");
            Console.WriteLine($"    Singleton    : {sg.Omur.Id}  ← hep aynı nesne");
        }

        // ── BÖLÜM 4: Captive Dependency — Hata Tespiti ─────

        Console.WriteLine("\n=== BÖLÜM 4: Captive Dependency (Hata) ===\n");

        try
        {
            var hataServices = new ServiceCollection();
            hataServices.AddSingleton<CaptiveSingleton>();  // Singleton
            hataServices.AddScoped<IKitapRepository, KitapRepository>();  // Scoped

            // ValidateScopes = true → container başlangıçta kontrol eder
            var hataProvider = hataServices.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true });

            // Singleton içinde Scoped inject etmeye çalış
            var _ = hataProvider.GetRequiredService<CaptiveSingleton>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Beklenen hata yakalandı:");
            Console.WriteLine($"  {ex.Message[..Math.Min(120, ex.Message.Length)]}...");
        }

        // ── BÖLÜM 5: IServiceProvider — Kabul Edilebilir Kullanım ──

        Console.WriteLine("\n=== BÖLÜM 5: Factory Pattern ile IServiceProvider ===\n");

        var factoryServices = new ServiceCollection();
        factoryServices.AddScoped<IKitapRepository, KitapRepository>();
        factoryServices.AddTransient<ILogServisi, ConsoleLogServisi>();
        factoryServices.AddScoped<KitapServisi>();
        factoryServices.AddScoped<KitapServisFactory>();

        var factoryProvider = factoryServices.BuildServiceProvider();

        using var factoryScope = factoryProvider.CreateScope();
        var factory = factoryScope.ServiceProvider.GetRequiredService<KitapServisFactory>();
        var normalServis  = factory.Olustur("normal");
        var sonuclar      = await normalServis.TumunuGetirAsync();
        Console.WriteLine($"  Factory'den alınan servis kitap sayısı: {sonuclar.Count}");
    }
}

// ─────────────────────────────────────────────
// Yardımcı Sınıflar
// ─────────────────────────────────────────────

// Captive dependency örneği için — Singleton içine Scoped inject ediliyor
public class CaptiveSingleton
{
    public CaptiveSingleton(IKitapRepository repo) { }  // repo Scoped, bu Singleton
}

// Factory — IServiceProvider'ı kabul edilebilir şekilde kullanan tek yer
public class KitapServisFactory
{
    private readonly IServiceProvider _provider;
    public KitapServisFactory(IServiceProvider provider) => _provider = provider;

    public KitapServisi Olustur(string tip) => tip switch
    {
        "normal" => _provider.GetRequiredService<KitapServisi>(),
        _        => _provider.GetRequiredService<KitapServisi>()
    };
}
