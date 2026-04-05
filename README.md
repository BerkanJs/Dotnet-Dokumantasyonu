# .NET Kaynakça

> ASP.NET Core, C#, mimari pattern'lar ve mikroservis konularında yapılandırılmış referans notları ve uygulama kodları.

## Amaç

Bu repo, .NET ekosistemini **derinlemesine kavramak** amacıyla oluşturulmuş kişisel bir kaynakçadır. Her konu için teorik notlar (`*.md`) ve kavramı doğrudan gösteren uygulama kodları (`*.cs`) bir arada tutulur.

Kapsam: CLR iç mekanizmaları → ASP.NET Core MVC → Mimari pattern'lar (Onion, CQRS) → Performans → Mikroservisler.

Her konu **"prodüksiyonda ne işe yarar?"** sorusu etrafında işlenir. Aynı domain (Kitabevi) tüm fazlar boyunca evrilir: basit model → MVC → Onion Architecture → Mikroservis.

---

## Proje Yapısı

```
NetPerfectionKursu/
├── Faz1-CSharp-CLR/
│   ├── 01-GC-Memory/           GC, Stack/Heap, IDisposable
│   ├── 02-ValueRef-Types/      struct, class, boxing, Span<T>
│   ├── 03-Async-Await/         Task, ValueTask, async patterns
│   ├── 04-LINQ-Generics/       LINQ derinlik, generics, delegates, reflection
│   └── 05-DI-Patterns/         DI container, lifetime, extension methods
│
├── Faz2-ASPNET-MVC/
│   └── KitabeviMVC/            ASP.NET Core MVC uygulaması
│
├── Faz3-Mimari/
│   ├── 01-SOLID/
│   ├── 02-DesignPatterns/
│   ├── 03-OnionArchitecture/
│   ├── 04-CQRS/
│   └── 05-Tests/
│
├── Faz4-Performans/
│   ├── 01-Benchmarks/
│   ├── 02-Memory-Optimization/
│   └── 03-EFCore-Tuning/
│
└── Faz5-Mikroservisler/
    ├── ApiGateway/             YARP tabanlı gateway
    ├── CatalogService/
    ├── OrderService/           Saga pattern
    ├── NotificationService/    RabbitMQ consumer
    └── docker/
```

---

## İşlenen Konular (04 Nisan 2026'ya kadar)

### Faz 1 — C# / CLR Temelleri ✅

| Gün | Konu |
|-----|------|
| 1 | CLR nedir, .NET runtime mimarisi |
| 2 | Stack / Heap bellek modeli |
| 3 | Garbage Collector, IDisposable, using pattern |
| 4 | Value type / Reference type, record, boxing |
| 5 | String davranışı, koleksiyonlar (List, Dictionary, HashSet) |
| 6 | async/await, Task, ConfigureAwait, deadlock senaryoları |
| 7 | Hafta 1 özet ve tekrar |
| 8 | Generics — constraints, covariance/contravariance |
| 9 | LINQ — deferred execution, expression tree temelleri |
| 10 | Delegates, Func/Action, Events, lambda |
| 11 | Reflection, Attributes, custom attribute yazımı |
| 12 | Pattern matching, modern C# özellikleri (switch expression, records, init) |
| 13 | Dependency Injection — container, lifetime (Singleton/Scoped/Transient) |

### Faz 2 — ASP.NET Core MVC (devam ediyor)

| Gün | Konu |
|-----|------|
| 14 | ASP.NET Core'a giriş — pipeline genel bakış *(müfredat notu)* |
| 15 | Middleware pipeline — IMiddleware, Use/Run/Map, sıralama önemi |
| 16 | Hosting modeli, `appsettings.json`, IOptions pattern, ortam yönetimi |
| 17 | Routing — conventional vs attribute routing, route constraints |
| 18 | MVC — Controller, Action, View, ViewData/ViewBag/TempData, Partial View |
| 19 | Filters — Action/Result/Exception/Resource filter, IAsyncActionFilter, filter pipeline sırası |

#### KitabeviMVC Projesi (Faz 2 uygulama kodu)

`Faz2-ASPNET-MVC/KitabeviMVC/` — öğrenilen her kavramı gerçek kod üzerinde gösteren çalışan MVC uygulaması.

Şu ana kadar eklenenler:

- **Middleware** — `IstekLoglamaMiddleware`: her isteği metod/path/süre ile loglar
- **Controller** — `KitapController`: CRUD aksiyonları, servis katmanı üzerinden çalışır
- **Service katmanı** — `IKitapServisi` / `KitapServisi`: controller'ı veri erişiminden ayırır
- **DbContext** — `KitabeviDbContext` (EF Core)
- **Filters**
  - `PerformansFilter` — action süresini ölçer, eşik aşılırsa uyarı loglar
  - `AuditFilter` — kimin hangi aksiyonu çağırdığını kaydeder
  - `GlobalHataFilter` — uygulama geneli exception yakalama
  - `ValidationFilter` — ModelState geçersizse aksiyona girmeden 400 döner
- **Configuration** — `JwtAyarlari`: strongly-typed options pattern örneği
- **Token** — `TokenServisi`: JWT üretimi

---

## Sıradaki Konular

- **Gün 20** — Model Binding, Validation (DataAnnotations, IValidatableObject)
- **Gün 21** — EF Core temelleri, Code First migration
- **Gün 22** — Repository pattern, Unit of Work
- **Gün 23** — Authentication & Authorization (Cookie auth, Identity)
- **Gün 24** — JWT Authentication
- **Gün 25** — Faz 2 özet / KitabeviMVC tamamlama

Ardından **Faz 3 — Mimari** (SOLID → Design Patterns → Onion Architecture → CQRS/MediatR).

---

## Kullanılan Teknolojiler

- .NET 8 / C# 12
- ASP.NET Core MVC
- Entity Framework Core
- xUnit, Moq, TestContainers *(Faz 3'te)*
- BenchmarkDotNet *(Faz 4'te)*
- Docker, RabbitMQ, Redis, YARP *(Faz 5'te)*
