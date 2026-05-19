# Gün 60 — Onion Katmanları: Detaylı Analiz

Dün Onion'ın neden gerektiğini gördük. Bugün 4 katmanın her birini tek tek inceliyoruz.

---

## Katman 1: Domain (En İçte)

**Ne içerir:**
- Entity, Value Object, Aggregate, Domain Event
- Domain Service (birden fazla aggregate'i ilgilendiren logic)
- Repository Interface (implementation değil — sadece sözleşme)

**Kural:** Hiçbir dış bağımlılık yok. Sadece C# standart kütüphanesi.

```csharp
// ✅ Domain katmanı — hiçbir NuGet paketi yok
namespace KitabeviOnion.Domain.Entities;

public class Kitap
{
    public Isbn Isbn { get; private set; } = null!; // Value Object
    public void StokAzalt(int adet) { ... }         // Domain logic burada
}

// ✅ Repository interface Domain'de — implementation değil
namespace KitabeviOnion.Domain.Interfaces;
public interface IKitapRepository
{
    Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default);
}
// ↑ kim implement edecek bilmiyor — EF Core mu, PostgreSQL mi, in-memory mi?
```

---

## Katman 2: Application

**Ne içerir:**
- Use Case → Command / Query (CQRS)
- Application Service
- DTO'lar (Domain entity'yi dışa açmaz)
- Infrastructure interface'leri (IEmailService, IFileStorage)

**Kural:** Sadece Domain katmanına bağımlı. EF Core, SMTP, HTTP bilmez.

```csharp
// ✅ Application — sadece Domain interface'i görüyor
public class KitapListeleHandler
{
    private readonly IKitapRepository _repo; // Domain interface
    // EF Core, DbContext yok — kim implement ediyor bilmiyor
}

// ✅ Infrastructure interface Application'da tanımlanır
namespace KitabeviOnion.Application.Interfaces;
public interface IEmailService
{
    Task GonderAsync(string alici, string konu, string govde, CancellationToken ct = default);
}
// ↑ SMTP detayı Infrastructure'da — Application sadece sözleşmeyi görür
```

---

## Katman 3: Infrastructure

**Ne içerir:**
- EF Core repository implementasyonları
- Email, dosya, dış API implementasyonları
- DB migration konfigürasyonu

**Kural:** Application ve Domain'e bağımlı. Presentation'ı bilmez.

```csharp
// ✅ Infrastructure — Domain interface'ini implement ediyor
public class KitapRepository : IKitapRepository // Domain interface
{
    private readonly AppDbContext _context; // EF Core sadece burada
    public async Task<Kitap?> BulByIdAsync(int id, CancellationToken ct = default)
        => await _context.Kitaplar.FirstOrDefaultAsync(k => k.Id == id, ct);
}
```

---

## Katman 4: Presentation (En Dışta)

**Ne içerir:**
- Controller / Minimal API endpoint
- Request/Response model (API DTO)
- DI composition root (Program.cs)

**Kural:** Diğer tüm katmanlara bağımlı olabilir — ama Domain ve Application'ı doğrudan çağırmamalı.

```csharp
// ✅ Controller sadece Handler görüyor
public class KitapController : ControllerBase
{
    private readonly KitapListeleHandler _handler; // Application handler
    // DbContext, Repository görmüyor
}
```

---

## Katmanlar Arası Bağımlılık Özeti

```
Presentation  → Application + Infrastructure (DI için)
Infrastructure → Application + Domain
Application   → Domain
Domain        → Hiçbir şey
```

---

## Faz2 vs Faz3 Karşılaştırma

| Katman | Faz2 N-Layer | Faz3 Onion |
|---|---|---|
| Controller | DbContext biliyor | Sadece Handler biliyor |
| Service | DbContext inject alıyor | IRepository interface görüyor |
| Repository | DbContext içinde | Infrastructure izole |
| Domain/Entity | Sadece getter/setter | İş kuralları burada |

---

## 500 vs 50k

| Konu | 500 | 50k |
|---|---|---|
| Katman sayısı | 2-3 yeterli | ✅ 4 katman — her biri farklı ekip |
| DTO ayrımı | Entity döndür | ✅ Entity API'ye sızmaz |
| Infra interface | Gereksiz soyutlama | ✅ DB swap, test izolasyonu |

---

## Sorular

1. Domain katmanında neden hiçbir NuGet paketi olamaz?
2. `IEmailService` neden Infrastructure'da değil Application'da tanımlanır?
3. Presentation katmanı neden Infrastructure'a bağımlı olmak zorundadır? (DI)
