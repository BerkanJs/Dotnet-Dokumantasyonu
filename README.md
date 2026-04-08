# .NET Kaynakça

> ASP.NET Core, C#, mimari pattern'lar ve mikroservis konularında yapılandırılmış referans notları ve uygulama kodları.

## Amaç

Bu repo, .NET ekosistemini **derinlemesine kavramak** amacıyla oluşturulmuş kişisel bir kaynakçadır. Her konu için teorik notlar (`*.md`) ve kavramı doğrudan gösteren uygulama kodları (`*.cs`) bir arada tutulur.

Kapsam: CLR iç mekanizmaları → ASP.NET Core MVC → Mimari pattern'lar (Onion, CQRS) → Performans → Mikroservisler.

Her konu **"prodüksiyonda ne işe yarar?"** sorusu etrafında işlenir. Aynı domain (Kitabevi) tüm fazlar boyunca evrilir: basit model → MVC → Onion Architecture → Mikroservis.

---

## Proje Yapısı

```
DotnetDoku/
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
├── Faz3-Mimari/                (planlandı)
├── Faz4-Performans/            (planlandı)
└── Faz5-Mikroservisler/        (planlandı)
```

---

## İşlenen Konular

### Faz 1 — C# / CLR Temelleri ✅

| Gün | Dosya | Konu |
|-----|-------|------|
| 1 | [gun1_clr_nedir.md](Faz1-CSharp-CLR/01-GC-Memory/gun1_clr_nedir.md) | CLR nedir, .NET runtime mimarisi |
| 2 | [gun2_stack_heap.md](Faz1-CSharp-CLR/01-GC-Memory/gun2_stack_heap.md) | Stack / Heap bellek modeli |
| 3 | [gun3_gc_idisposable.md](Faz1-CSharp-CLR/01-GC-Memory/gun3_gc_idisposable.md) | Garbage Collector, IDisposable, using pattern |
| 4 | [gun4_value_ref_record.md](Faz1-CSharp-CLR/02-ValueRef-Types/gun4_value_ref_record.md) | Value type / Reference type, record, boxing |
| 5 | [gun5_string_koleksiyonlar.md](Faz1-CSharp-CLR/02-ValueRef-Types/gun5_string_koleksiyonlar.md) | String davranışı, koleksiyonlar |
| 6 | [gun6_async_await.md](Faz1-CSharp-CLR/03-Async-Await/gun6_async_await.md) | async/await, Task, ConfigureAwait, deadlock |
| 7 | [gun7_hafta1_ozet.md](Faz1-CSharp-CLR/gun7_hafta1_ozet.md) | Hafta 1 özet ve tekrar |
| 8 | [gun8_generics.md](Faz1-CSharp-CLR/04-LINQ-Generics/gun8_generics.md) | Generics — constraints, covariance/contravariance |
| 9 | [gun9_linq.md](Faz1-CSharp-CLR/04-LINQ-Generics/gun9_linq.md) | LINQ — deferred execution, expression tree |
| 10 | [gun10_delegates_events.md](Faz1-CSharp-CLR/04-LINQ-Generics/gun10_delegates_events.md) | Delegates, Func/Action, Events, lambda |
| 11 | [gun11_reflection_attributes.md](Faz1-CSharp-CLR/04-LINQ-Generics/gun11_reflection_attributes.md) | Reflection, Attributes, custom attribute |
| 12 | [gun12_pattern_matching_modern_csharp.md](Faz1-CSharp-CLR/04-LINQ-Generics/gun12_pattern_matching_modern_csharp.md) | Pattern matching, modern C# özellikleri |
| 13 | [gun13_dependency_injection.md](Faz1-CSharp-CLR/05-DI-Patterns/gun13_dependency_injection.md) | Dependency Injection — container, lifetime |

---

### Faz 2 — ASP.NET Core MVC ✅ (devam ediyor)

| Gün | Dosya | Konu |
|-----|-------|------|
| 15 | [gun15_middleware_pipeline.md](Faz2-ASPNET-MVC/gun15_middleware_pipeline.md) | Middleware pipeline — IMiddleware, Use/Run/Map |
| 16 | [gun16_hosting_konfigurasyon.md](Faz2-ASPNET-MVC/gun16_hosting_konfigurasyon.md) | Hosting modeli, appsettings, IOptions pattern |
| 17 | [gun17_routing.md](Faz2-ASPNET-MVC/gun17_routing.md) | Routing — conventional vs attribute routing |
| 18 | [gun18_mvc_controller_view.md](Faz2-ASPNET-MVC/gun18_mvc_controller_view.md) | MVC — Controller, Action, View, Partial View |
| 19 | [gun19_filters.md](Faz2-ASPNET-MVC/gun19_filters.md) | Filters — Action/Result/Exception/Resource filter |
| 20 | [gun20_auth.md](Faz2-ASPNET-MVC/gun20_auth.md) | Authentication & Authorization — Cookie, Policy, Resource-based |
| 22 | [gun22_rest_api.md](Faz2-ASPNET-MVC/gun22_rest_api.md) | REST API — HTTP semantikleri, versioning, ProblemDetails |
| 23 | [gun23_minimal_api.md](Faz2-ASPNET-MVC/gun23_minimal_api.md) | Minimal API — endpoint filter, TypedResults |
| 24 | [gun24_openapi_swagger.md](Faz2-ASPNET-MVC/gun24_openapi_swagger.md) | OpenAPI / Swagger — .NET 9 built-in, Scalar UI |
| 25 | [gun25_background_services.md](Faz2-ASPNET-MVC/gun25_background_services.md) | BackgroundService, PeriodicTimer, Hangfire, Channel |
| 26 | [gun26_caching.md](Faz2-ASPNET-MVC/gun26_caching.md) | Caching — IMemoryCache, IDistributedCache, OutputCache |
| 27 | [gun27_testing.md](Faz2-ASPNET-MVC/gun27_testing.md) | Testing — xUnit, Moq, WebApplicationFactory, TestContainers |
| 29 | [gun29_efcore_dbcontext.md](Faz2-ASPNET-MVC/gun29_efcore_dbcontext.md) | EF Core — DbContext, Change Tracker, AsNoTracking |

---

### KitabeviMVC Projesi — Uygulama Kodu

`Faz2-ASPNET-MVC/KitabeviMVC/` — her kavramı çalışan kod üzerinde gösteren ASP.NET Core MVC uygulaması.

#### Katmanlar ve Sorumluluklar

```
Controllers/
  KitapController.cs      → MVC — CRUD aksiyonları, view döndürür
  KitapApiController.cs   → REST API — JSON döndürür, versioning (v1/v2)
  HesapController.cs      → Cookie auth — giriş/çıkış

Services/
  IKitapServisi.cs        → async arayüz (Gün 29'da async'e çevrildi)
  EfKitapServisi.cs       → EF Core implementasyonu (Gün 29 — AsNoTracking, Change Tracker)
  KitapServisi.cs         → In-memory implementasyon (test/fake olarak kullanılır)
  CachedKitapServisi.cs   → Decorator pattern — cache katmanı (Gün 26)
  TokenServisi.cs         → JWT üretimi

Data/
  KitabeviDbContext.cs    → DbContext — Kitaplar + Yazarlar, Fluent API, ilişkiler (Gün 29)

Models/
  Entities/Kitap.cs       → Entity — YazarId FK ile (Gün 29)
  Entities/Yazar.cs       → Entity — navigation property ile (Gün 29)
  ViewModels/             → Controller ↔ View arası
  Dto/                    → API request/response

Middleware/
  IstekLoglamaMiddleware  → Her isteği metod/path/süre ile loglar

Filters/
  PerformansFilter        → Yavaş action tespiti (500ms eşiği)
  AuditFilter             → Kim, ne zaman, hangi kaynağa
  GlobalHataFilter        → Yakalanmamış hata → ProblemDetails
  ValidationFilter        → ModelState geçersizse aksiyona girmeden 400

Endpoints/
  KitapEndpoints.cs       → Minimal API — /api/minimal/kitaplar

BackgroundServices/
  StokKontrolServisi      → PeriodicTimer ile 10dk'da bir stok kontrolü
  SiparisOnayServisi      → Channel tabanlı producer/consumer
  HangfireJoblar          → Fire-and-forget, recurring, continuation

Authorization/
  KitapDuzenlemeHandler   → Resource-based authorization
```

#### Gün 29 — EF Core Değişiklikleri

- **`KitabeviDbContext`**: `Yazarlar` DbSet eklendi, Fluent API ile ilişki ve index tanımları, `Kitap-Yazar` one-to-many ilişkisi
- **`EfKitapServisi`**: DbContext tabanlı yeni implementasyon — `AsNoTracking()` okuma sorgularında, Change Tracker üzerinden güncelleme/silme, `AnyAsync` ile benzersizlik kontrolü
- **`IKitapServisi`**: Tüm metodlar async'e çevrildi (`Task<T>` döndürür)
- **`KitapServisi`**: `Task.FromResult` wrapper'ları ile arayüz uyumu (in-memory, test double olarak kullanılır)
- **`CachedKitapServisi`**: `SemaphoreSlim.WaitAsync()` ile async stampede koruması
- **`Program.cs`**: `UseInMemoryDatabase("KitabeviDb")` — SQL Server gerektirmez, uygulama başlangıcında seed data eklenir; `EfKitapServisi` Scoped olarak kayıtlı

---

## Sıradaki Konular

### Hafta 5 — Entity Framework Core

| Gün | Konu |
|-----|------|
| 30 | IQueryable ve LINQ-to-SQL — expression tree, client vs server evaluation, N+1 problemi |
| 31 | N+1 Problemi ve Çözümleri — eager/lazy/explicit loading, compiled queries |
| 32 | Migration Stratejileri — code-first, idempotency, production migration |
| 33 | EF Core Performance Tuning — AsNoTracking, batch operations, connection pooling |
| 34 | Dapper: Micro ORM karşılaştırma |
| 35 | Hafta 5 özet |

Ardından **Faz 3 — Mimari** (SOLID → Design Patterns → Onion Architecture → CQRS/MediatR).

---

## Kullanılan Teknolojiler

- .NET 9 / C# 13
- ASP.NET Core MVC
- Entity Framework Core 9 (InMemory + SqlServer)
- Hangfire (background jobs)
- xUnit, Moq, WebApplicationFactory, TestContainers
- BenchmarkDotNet *(Faz 4'te)*
- Docker, RabbitMQ, Redis, YARP *(Faz 5'te)*
