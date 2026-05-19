# Gün 61 — Bağımlılık Kuralı: Dependency Rule

"Bağımlılık daima içe doğru akar" — Onion'ın tek ve değişmez kuralı. Bu kural ihlal edilirse mimari çöker.

---

## Kural

```
Infrastructure → Application → Domain
      ↑               ↑
   dışarıda        ortada       merkezde

Ters yön yasak:
Domain → Application    ❌
Domain → Infrastructure ❌
Application → Infrastructure ❌ (interface üzerinden değilse)
```

---

## Neden Bu Kural?

Domain değişim nedenine sahip olmamalı. Sadece iş kuralları değişince değişmeli.

```csharp
// ❌ Kural ihlali: Domain EF Core biliyor
namespace KitabeviOnion.Domain.Entities;
using Microsoft.EntityFrameworkCore; // ← Domain'de Infrastructure bağımlılığı

public class Kitap
{
    [Key] // EF Core attribute — Domain, Infrastructure detayını biliyor
    public int Id { get; set; }
}
// DB değişince Domain değişmek zorunda → kural ihlali
```

```csharp
// ✅ Doğru: Domain hiçbir şeyi bilmiyor
namespace KitabeviOnion.Domain.Entities;
// using yok — standart C# dışında hiçbir şey

public class Kitap
{
    public int Id { get; private set; }
    // EF Core konfigürasyonu Infrastructure'da AppDbContext'te
}
```

---

## `IRepository<T>` Neden Domain'de Tanımlanır?

```csharp
// ❌ Yanlış: Interface Infrastructure'da
namespace KitabeviOnion.Infrastructure.Repositories;
public interface IKitapRepository { ... }
// Application bu interface'i kullanmak için Infrastructure'a bağımlı olur
// Infrastructure → Application → Infrastructure → kısır döngü

// ✅ Doğru: Interface Domain'de
namespace KitabeviOnion.Domain.Interfaces;
public interface IKitapRepository { ... }
// Application → Domain (interface) ← Infrastructure (implement)
// Bağımlılık yönü korunuyor
```

---

## Pratik Mimari Kararlar

### DTO nerede yaşamalı?

```csharp
// ✅ Application katmanında
namespace KitabeviOnion.Application.DTOs;
public record KitapDto(int Id, string Baslik, decimal Fiyat);

// Neden Application'da?
// Domain entity → dışa açılmaz (private set'ler, Value Object'ler)
// Infrastructure DTO bilmemeli
// Controller DTO'yu Application'dan alır
```

### Mapper nerede çalışmalı?

```csharp
// ✅ Application Handler içinde — entity → DTO dönüşümü
public class KitapListeleHandler
{
    public async Task<IReadOnlyList<KitapDto>> Handle(...)
    {
        var kitaplar = await _repo.TumunuGetirAsync(ct);
        return kitaplar.Select(k => new KitapDto(k.Id, k.Baslik, k.Fiyat.Deger))
                       .ToList().AsReadOnly();
        // ↑ mapping burada — Controller bilmez, Infrastructure bilmez
    }
}

// ❌ Controller'da mapping — Presentation, Domain'i biliyor demek
public IActionResult Listele()
{
    var kitaplar = _repo.TumunuGetirAsync();
    return Ok(kitaplar.Select(k => new { k.Isbn.Deger, k.Fiyat.Deger })); // Controller Domain biliyor
}
```

### Validation nerede yapılmalı?

```
Domain validation   → invariant'lar (stok negatif olamaz, ISBN format)
Application validation → use case kuralları (ISBN zaten kayıtlı mı?)
Presentation validation → input format (boş alan, max uzunluk)
```

---

## Faz2 Karşılaştırma

```csharp
// Faz2 — bağımlılık kuralı yok
public class KitapServisi // Business katmanı
{
    private readonly KitabeviDbContext _context; // ← Infrastructure bağımlılığı
    // Business → Infrastructure → kural ihlali
    // DB değişince KitapServisi de değişmek zorunda
}
```

---

## 500 vs 50k

| Konu | 500 | 50k |
|---|---|---|
| Kural ihlali toleransı | Küçük projede geçici | ❌ Teknik borç birikir |
| DTO ayrımı | Entity döndür | ✅ API kontratı kararlı kalır |
| Domain'de NuGet | Tolere edilir | ❌ Her güncelleme Domain'i etkiler |

---

## Sorular

1. Application katmanı Infrastructure'daki bir sınıfı doğrudan kullansa ne olur?
2. `AutoMapper` neden Presentation'da değil Application'da konfigüre edilmeli?
3. Validation'ı tamamen Domain'e taşısaydık ne kaybederdin?
