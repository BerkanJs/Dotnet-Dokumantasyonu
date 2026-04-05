# Gün 13 — Dependency Injection: C# Perspektifinden

---

## 1. Problem: Bağımlılıkları Kim Oluşturur?

`KitapServisi`'nin veritabanına ulaşmak için `KitapRepository`'ye ihtiyacı var.

```csharp
// Kötü yol — KitapServisi kendi bağımlılığını kendisi oluşturuyor
public class KitapServisi
{
    private readonly KitapRepository _repo;

    public KitapServisi()
    {
        _repo = new KitapRepository();  // ← sıkı bağlantı (tight coupling)
    }
}
```

Sorunlar:
- `KitapRepository`'yi test için mock'layamazsın — `new` ile direkt oluşturuldu
- `KitapRepository` değişirse `KitapServisi` de değişmek zorunda
- Farklı bir implementasyon (örn. InMemory repo) geçiremezsin

**Çözüm:** Bağımlılıkları dışarıdan ver — Dependency Injection.

---

## 2. DI'nin Özü: Dışarıdan Geçir

```csharp
// İyi yol — bağımlılık constructor'dan geliyor
public class KitapServisi
{
    private readonly IKitapRepository _repo;

    public KitapServisi(IKitapRepository repo)  // ← inject
    {
        _repo = repo;
    }
}
```

`KitapServisi` artık şunu söylüyor: "Bana bir `IKitapRepository` ver, gerisini ben hallederim."  
Kim verecek? **DI Container**.

Spring'deki karşılığı:
- `@Autowired` constructor injection = .NET constructor injection
- `@Component` / `@Service` = `builder.Services.AddScoped<T>()`
- Spring IoC Container = .NET `IServiceProvider`

---

## 3. DI Container Nasıl Çalışır?

Container iki şey yapar:

1. **Kayıt (Registration):** "Bu interface istenirse şu sınıfı ver" kuralını öğrenir
2. **Çözümleme (Resolution):** İstendiğinde nesneyi oluşturur, bağımlılıklarını da çözer

```csharp
// Program.cs — kayıt aşaması (uygulama başlarken bir kez)
builder.Services.AddScoped<IKitapRepository, KitapRepository>();
builder.Services.AddScoped<KitapServisi>();

// ASP.NET Core controller'ı — çözümleme (her request'te)
public class KitapController : ControllerBase
{
    private readonly KitapServisi _servis;

    public KitapController(KitapServisi servis)  // container inject eder
    {
        _servis = servis;
    }
}
```

Container, `KitapController` oluşturulurken şöyle düşünür:
1. `KitapController` → `KitapServisi` lazım
2. `KitapServisi` → `IKitapRepository` lazım
3. `IKitapRepository` → `KitapRepository` kayıtlı, onu oluştur
4. Hepsini birbirine bağla, `KitapController`'ı ver

Buna **object graph inşası** denir.

---

## 4. Lifetime: Transient, Scoped, Singleton

Nesnenin ne zaman oluşturulup ne zaman yok edileceğini belirler.

### Transient
Her istendiğinde **yeni nesne** oluşturulur.

```csharp
builder.Services.AddTransient<IEmailGonderici, SmtpEmailGonderici>();
```

- A controller istedi → yeni nesne
- B controller istedi → yeni nesne
- Aynı request içinde iki kez istense → iki farklı nesne

Ne zaman kullan: **stateless, hafif, bağımsız** nesneler. Hesap yapıcılar, formatlayıcılar.

---

### Scoped
Aynı **HTTP request** içinde tek nesne, farklı request'lerde yeni nesne.

```csharp
builder.Services.AddScoped<IKitapRepository, KitapRepository>();
builder.Services.AddScoped<KitapServisi>();
```

- Request 1 → tek KitapRepository
- Request 2 → yeni KitapRepository

Ne zaman kullan: **veritabanı bağlantıları**, Unit of Work. EF Core `DbContext` her zaman Scoped olur.

Spring'deki karşılığı: `@RequestScope`

---

### Singleton
Uygulama boyunca **tek nesne**. İlk istendiğinde oluşturulur, sonra hep aynı döner.

```csharp
builder.Services.AddSingleton<IOnbellekServisi, RedisOnbellekServisi>();
builder.Services.AddSingleton<IKonfigürasyon, Konfigurasyon>();
```

- Request 1 → X nesnesi
- Request 2 → aynı X nesnesi

Ne zaman kullan: **paylaşılan durum, cache, konfigürasyon**. Thread-safe olmalı.

Spring'deki karşılığı: Default `@Component` scope (Spring'de Singleton default'tur).

---

### Özet Tablo

| Lifetime | Ne zaman yeni nesne? | Kullanım yeri |
|---|---|---|
| Transient | Her istek | Stateless, hafif nesneler |
| Scoped | Her HTTP request | DbContext, repository, servisler |
| Singleton | Uygulama boyunca bir kez | Cache, konfigürasyon, HTTP client |

---

## 5. Captive Dependency Antipattern

**En sık yapılan hata:** Uzun ömürlü nesne, kısa ömürlü nesneyi tutar.

```csharp
// Singleton servis, Scoped bağımlılık alıyor
public class BildirimServisi  // Singleton olarak kayıtlı
{
    private readonly IKitapRepository _repo;  // Scoped olarak kayıtlı!

    public BildirimServisi(IKitapRepository repo)
    {
        _repo = repo;  // ← ilk request'teki Scoped nesne burada kaldı
    }
}
```

Ne olur?
- `BildirimServisi` Singleton — uygulama boyunca tek nesne, hep aynı `_repo`'yu tutar
- `IKitapRepository` Scoped — her request'te farklı olması gerekir
- Ama Singleton onu yakaladığı için hep **ilk request'teki repo** kullanılır
- Farklı kullanıcıların request'leri aynı veritabanı bağlantısını paylaşır → hata

.NET bunu başlangıçta yakalar ve exception fırlatır:
```
InvalidOperationException: Cannot consume scoped service 'IKitapRepository'
from singleton 'BildirimServisi'.
```

**Kural:** Kısa ömürlü nesneyi uzun ömürlüye inject etme.
```
Singleton → Singleton ✓
Singleton → Scoped   ✗  (Captive Dependency)
Singleton → Transient ✗
Scoped    → Scoped   ✓
Scoped    → Transient ✓
Transient → her şey  ✓
```

---

## 6. IServiceProvider ve Service Locator Antipattern

`IServiceProvider` container'ın kendisidir — istediğin servisi runtime'da alabilirsin:

```csharp
public class KitapServisi
{
    private readonly IServiceProvider _provider;

    public KitapServisi(IServiceProvider provider)
    {
        _provider = provider;
    }

    public void Islem()
    {
        // runtime'da iste — Service Locator antipattern
        var repo = _provider.GetRequiredService<IKitapRepository>();
    }
}
```

Bu **Service Locator antipattern** — neden kötü?
- Bağımlılıklar constructor'da görünmez, gizlidir
- Test için ne mock'lanacağını bilemezsin
- Kod okunduğunda "bu sınıf neye bağımlı?" sorusu cevaplanamaz

Ne zaman **mecbur** kalırsın? Factory pattern içinde runtime'da tip belirlenmesi gerektiğinde:

```csharp
// Kabul edilebilir kullanım — factory içinde
public class ServisFactory
{
    private readonly IServiceProvider _provider;

    public ServisFactory(IServiceProvider provider) => _provider = provider;

    public IKitapServisi Olustur(string tip) => tip switch
    {
        "premium" => _provider.GetRequiredService<PremiumKitapServisi>(),
        _         => _provider.GetRequiredService<KitapServisi>()
    };
}
```

---

## 7. IServiceCollection Extension Method Pattern

Kayıt kodunu gruplamak için extension method yaz:

```csharp
// Kötü — Program.cs şişiyor
builder.Services.AddScoped<IKitapRepository, KitapRepository>();
builder.Services.AddScoped<KitapServisi>();
builder.Services.AddScoped<SiparisServisi>();
builder.Services.AddScoped<StokServisi>();
// ... 50 satır daha

// İyi — grupla
public static class KitabeviServiceExtensions
{
    public static IServiceCollection AddKitabeviServices(
        this IServiceCollection services)
    {
        services.AddScoped<IKitapRepository, KitapRepository>();
        services.AddScoped<KitapServisi>();
        services.AddScoped<SiparisServisi>();
        services.AddScoped<StokServisi>();
        return services;
    }
}

// Program.cs — tek satır
builder.Services.AddKitabeviServices();
```

ASP.NET Core'un kendi kodu da böyle çalışır:  
`builder.Services.AddControllers()` → aslında onlarca kayıt yapan bir extension method.

---

## 8. Pure DI — Container Olmadan

Küçük projelerde container olmadan da DI yapılabilir:

```csharp
// Bütün nesne grafiğini elle oluştur
var repo    = new KitapRepository(connectionString);
var servis  = new KitapServisi(repo);
var kontrol = new KitapController(servis);
```

**Ne zaman tercih edilir?**
- Çok küçük uygulama / CLI tool
- Container bağımlılığından kaçınmak istiyorsan
- Test için basit setup

**Ne zaman container kullanırsın?**
- Onlarca servis ve lifetime yönetimi var
- Framework (ASP.NET Core) zaten container sağlıyor
- Middleware, filter, interceptor gibi framework entegrasyonu gerekiyor

Faz 2'den itibaren hep container kullanacaksın — ASP.NET Core'da Pure DI pratikte kullanılmaz.

---

## 9. Web Geliştirmede Nerede Görünür?

```csharp
// Program.cs — tüm kayıtlar burada
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<KitabeviDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IKitapRepository, KitapRepository>();
builder.Services.AddScoped<KitapServisi>();
builder.Services.AddSingleton<IOnbellekServisi, RedisOnbellekServisi>();

var app = builder.Build();
```

```csharp
// Controller — constructor injection
[ApiController]
[Route("api/[controller]")]
public class KitapController : ControllerBase
{
    private readonly KitapServisi _servis;

    public KitapController(KitapServisi servis)
    {
        _servis = servis;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _servis.TumKitaplariGetirAsync());
}
```

Faz 2'de her controller böyle yazılacak. Container, request geldiğinde controller'ı ve tüm bağımlılıklarını otomatik oluşturacak.

---

## 10. Kontrol Soruları

1. Scoped ile Singleton arasındaki fark nedir? Her birini hangi durumda kullanırsın?

2. Captive Dependency nedir? Neden sorun yaratır?

3. Service Locator neden antipattern sayılır? Constructor injection'dan farkı ne?

4. `DbContext`'in neden her zaman Scoped olması gerektiğini açıkla.

5. `IServiceCollection` extension method pattern neden kullanılır?
