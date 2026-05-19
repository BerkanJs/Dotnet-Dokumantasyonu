# Gün 59 — Layered Architecture'dan Onion'a Geçiş

Faz2'de KitabeviMVC N-Layer mimarisindeydi: Controller → Service → Repository → DB. Bu yaklaşım küçük projede işe yarar ama büyüyünce kırılır. Bugün neden kırıldığını ve Onion'ın ne çözdüğünü anlıyoruz.

---

## Geleneksel N-Layer'ın Sorunu

```
Presentation (Controller)
        ↓
Business (Service)
        ↓
Data Access (Repository/DbContext)
        ↓
Database
```

**Bağımlılık yönü:** Üstten alta, her katman altındakini biliyor.

Faz2'de:
```csharp
// KitapServisi doğrudan EF Core biliyor
public class KitapServisi
{
    private readonly KitabeviDbContext _context; // ↑ Data katmanına doğrudan bağımlı
    // DB değişince burası değişir
    // Test için gerçek DB veya in-memory DB gerekir
}
```

**Sorun:** DB değişirse (SQL Server → PostgreSQL) Business katmanı da değişmek zorunda. Business katmanı Infrastructure detayını biliyor — bu ihlal.

---

## Üç Mimari, Bir Fikir

**Onion Architecture** — Jeffrey Palermo (2008)
**Clean Architecture** — Robert C. Martin (2012)
**Hexagonal Architecture (Ports & Adapters)** — Alistair Cockburn (2005)

Üçü de aynı temel fikri farklı ifade eder:

```
Domain merkezdedir.
Dış dünya (DB, UI, API, email) detaydır.
Bağımlılık daima dıştan içe akar — asla içten dışa.
```

---

## N-Layer vs Onion — Fark

```
N-Layer:                    Onion:
UI → Business → Data        API
                            ↓
                        Application
                            ↓
                          Domain  ← merkez
                            ↑
                        Infrastructure
                        (DB, Email, vb.)
```

**N-Layer'da:** Business, Data'ya bağımlı.
**Onion'da:** Infrastructure, Domain'e bağımlı. Domain hiçbir şeyi bilmiyor.

---

## Faz2'den Faz3'e

| Konu | Faz2 N-Layer | Faz3 Onion |
|---|---|---|
| DB bağımlılığı | Service içinde | Infrastructure katmanında izole |
| Test | Gerçek DB gerekir | Domain testi için DB gerekmez |
| DB değişimi | Service değişir | Sadece Infrastructure değişir |
| Bağımlılık yönü | Üstten alta | Dıştan içe (Domain merkez) |

---

## 500 vs 50k

| Konu | 500 | 50k |
|---|---|---|
| N-Layer yeterli mi? | ✅ Küçük ekip, basit domain | ❌ DB değişince her katman etkilenir |
| Onion gerekli mi? | ⚠️ Opsiyonel | ✅ Ekip büyüyünce katman izolasyonu şart |

---

## Sorular

1. N-Layer'da Business katmanı neden Infrastructure'a bağımlıdır? Bu neden sorun?
2. Onion'da bağımlılık yönü neden tersine çevrilir?
3. Hexagonal Architecture'daki "Port" ve "Adapter" kavramları Onion'ın hangi kavramlarına karşılık gelir?
