# .NET Öğrenim Yolu

> C# ve .NET ekosistemini mid-seviyeden üst-orta seviyeye taşıyan yapılandırılmış notlar ve çalışan uygulama kodları.

Kapsam: CLR iç mekanizmaları → ASP.NET Core MVC → Mimari Pattern'lar (SOLID, Onion, CQRS) → Performans → Mikroservisler.

Tüm fazlar boyunca aynı domain büyür: **Kitabevi uygulaması** — basit model → MVC → Onion Architecture → Mikroservis.

---

## Proje Yapısı

```
DotnetDoku/
├── Faz1-CSharp-CLR/        C# ve CLR temelleri
├── Faz2-ASPNET-MVC/        ASP.NET Core MVC + EF Core + Test
│   └── KitabeviMVC/        Çalışan uygulama
├── Faz3-Mimari/            SOLID + Design Patterns + Onion + CQRS  ← şu an burada
│   ├── 01-SOLID/           Her prensip için izole demo
│   ├── 02-DesignPatterns/  Creational, Structural, Behavioral
│   ├── 03-OnionArchitecture/   KitabeviMVC → Onion refactor
│   └── 04-CQRS/            MediatR + Pipeline Behaviors
├── Faz4-Performans/        BenchmarkDotNet, Redis, SQL Index
└── Faz5-Mikroservisler/    Docker, RabbitMQ, YARP, Saga
```

---

## Faz 1 — C# / CLR Temelleri ✅

| Gün | Dosya | Konu |
|-----|-------|------|
| 1 | [gun1_clr_nedir.md](Faz1-CSharp-CLR/01-GC-Memory/gun1_clr_nedir.md) | CLR nedir, .NET runtime mimarisi |
| 2 | [gun2_stack_heap.md](Faz1-CSharp-CLR/01-GC-Memory/gun2_stack_heap.md) | Stack / Heap bellek modeli |
| 3 | [gun3_gc_idisposable.md](Faz1-CSharp-CLR/01-GC-Memory/gun3_gc_idisposable.md) | Garbage Collector, IDisposable, using |
| 4 | [gun4_value_ref_record.md](Faz1-CSharp-CLR/02-ValueRef-Types/gun4_value_ref_record.md) | Value/Reference type, record, boxing |
| 5 | [gun5_string_koleksiyonlar.md](Faz1-CSharp-CLR/02-ValueRef-Types/gun5_string_koleksiyonlar.md) | String davranışı, koleksiyonlar |
| 6 | [gun6_async_await.md](Faz1-CSharp-CLR/03-Async-Await/gun6_async_await.md) | async/await, Task, ConfigureAwait, deadlock |
| 7 | [gun7_hafta1_ozet.md](Faz1-CSharp-CLR/gun7_hafta1_ozet.md) | Hafta 1 özet |
| 8 | [gun8_generics.md](Faz1-CSharp-CLR/04-LINQ-Generics/gun8_generics.md) | Generics, constraints, variance |
| 9 | [gun9_linq.md](Faz1-CSharp-CLR/04-LINQ-Generics/gun9_linq.md) | LINQ, deferred execution, expression tree |
| 10 | [gun10_delegates_events.md](Faz1-CSharp-CLR/04-LINQ-Generics/gun10_delegates_events.md) | Delegates, Func/Action, Events |
| 11 | [gun11_reflection_attributes.md](Faz1-CSharp-CLR/04-LINQ-Generics/gun11_reflection_attributes.md) | Reflection, Attributes |
| 12 | [gun12_pattern_matching_modern_csharp.md](Faz1-CSharp-CLR/04-LINQ-Generics/gun12_pattern_matching_modern_csharp.md) | Pattern matching, modern C# |
| 13 | [gun13_dependency_injection.md](Faz1-CSharp-CLR/05-DI-Patterns/gun13_dependency_injection.md) | DI container, lifetime |

---

## Faz 2 — ASP.NET Core MVC ✅

| Gün | Dosya | Konu |
|-----|-------|------|
| 15 | [gun15_middleware_pipeline.md](Faz2-ASPNET-MVC/gun15_middleware_pipeline.md) | Middleware pipeline |
| 16 | [gun16_hosting_konfigurasyon.md](Faz2-ASPNET-MVC/gun16_hosting_konfigurasyon.md) | Hosting, appsettings, IOptions |
| 17 | [gun17_routing.md](Faz2-ASPNET-MVC/gun17_routing.md) | Routing — conventional vs attribute |
| 18 | [gun18_mvc_controller_view.md](Faz2-ASPNET-MVC/gun18_mvc_controller_view.md) | MVC — Controller, Action, View |
| 19 | [gun19_filters.md](Faz2-ASPNET-MVC/gun19_filters.md) | Filters — Action/Result/Exception |
| 20 | [gun20_auth.md](Faz2-ASPNET-MVC/gun20_auth.md) | Authentication & Authorization |
| 22 | [gun22_rest_api.md](Faz2-ASPNET-MVC/gun22_rest_api.md) | REST API, versioning, ProblemDetails |
| 23 | [gun23_minimal_api.md](Faz2-ASPNET-MVC/gun23_minimal_api.md) | Minimal API, endpoint filter |
| 24 | [gun24_openapi_swagger.md](Faz2-ASPNET-MVC/gun24_openapi_swagger.md) | OpenAPI / Swagger |
| 25 | [gun25_background_services.md](Faz2-ASPNET-MVC/gun25_background_services.md) | BackgroundService, Hangfire, Channel |
| 26 | [gun26_caching.md](Faz2-ASPNET-MVC/gun26_caching.md) | IMemoryCache, IDistributedCache, OutputCache |
| 27 | [gun27_testing.md](Faz2-ASPNET-MVC/gun27_testing.md) | xUnit, Moq, WebApplicationFactory |
| 28 | [gun28_ihttpclientfactory.md](Faz2-ASPNET-MVC/gun28_ihttpclientfactory.md) | IHttpClientFactory, Typed Client, DelegatingHandler |
| 29 | [gun29_efcore_dbcontext.md](Faz2-ASPNET-MVC/gun29_efcore_dbcontext.md) | EF Core, DbContext, Change Tracker |
| 30 | [gun30_iqueryable_linq_to_sql.md](Faz2-ASPNET-MVC/gun30_iqueryable_linq_to_sql.md) | IQueryable, LINQ-to-SQL, expression tree |
| 31 | [gun31_n_plus_1_ve_loading_stratejileri.md](Faz2-ASPNET-MVC/gun31_n_plus_1_ve_loading_stratejileri.md) | N+1 problemi, eager/lazy loading |
| 32 | [gun32_migration_stratejileri.md](Faz2-ASPNET-MVC/gun32_migration_stratejileri.md) | Migration — code-first, production |
| 33 | [gun33_ef_core_performance_tuning.md](Faz2-ASPNET-MVC/gun33_ef_core_performance_tuning.md) | EF Core performans, batch operations |
| 34 | [gun34_repository_pattern.md](Faz2-ASPNET-MVC/gun34_repository_pattern.md) | Repository Pattern, Unit of Work |
| 35 | [gun35_cqrs_mediatr.md](Faz2-ASPNET-MVC/gun35_cqrs_mediatr.md) | CQRS, MediatR, Pipeline Behavior |
| 36 | [gun36_serilog_loglama.md](Faz2-ASPNET-MVC/gun36_serilog_loglama.md) | Serilog, structured logging |
| 37 | [gun37_docker_containerization.md](Faz2-ASPNET-MVC/gun37_docker_containerization.md) | Docker, Dockerfile, docker-compose |
| 38 | [gun38_xunit_test_temelleri.md](Faz2-ASPNET-MVC/gun38_xunit_test_temelleri.md) | xUnit temelleri, test organizasyonu |
| 39 | [gun39_test_doubles_moq.md](Faz2-ASPNET-MVC/gun39_test_doubles_moq.md) | Test doubles, Moq |
| 40 | [gun40_fluentassertions.md](Faz2-ASPNET-MVC/gun40_fluentassertions.md) | FluentAssertions |
| 41 | [gun41_integration_testing.md](Faz2-ASPNET-MVC/gun41_integration_testing.md) | Integration testing, WebApplicationFactory |
| 42 | [gun42_testcontainers.md](Faz2-ASPNET-MVC/gun42_testcontainers.md) | TestContainers — gerçek DB ile test |
| 43 | [gun43_architecture_testing.md](Faz2-ASPNET-MVC/gun43_architecture_testing.md) | Architecture testing, NetArchTest |
| 44 | [gun44_tdd_test_stratejisi.md](Faz2-ASPNET-MVC/gun44_tdd_test_stratejisi.md) | TDD, test stratejisi |

### KitabeviMVC — Uygulama Kodu

`Faz2-ASPNET-MVC/KitabeviMVC/` — tüm Faz2 kavramlarını çalışan kod üzerinde gösteren ASP.NET Core MVC uygulaması.

```
Controllers/
  KitapController.cs        MVC CRUD, view döndürür
  KitapApiController.cs     REST API, JSON, versioning (v1/v2)
  HesapController.cs        Cookie auth — giriş/çıkış

Services/
  IKitapServisi.cs          Async arayüz
  EfKitapServisi.cs         EF Core — AsNoTracking, Change Tracker, compiled query
  CachedKitapServisi.cs     Decorator pattern — cache katmanı
  IKitapSorguServisi.cs     IQueryable, Include, AsSplitQuery
  IKitapBatchServisi.cs     ExecuteUpdate/Delete — toplu operasyonlar
  KitapApiIstemcisi.cs      Typed HTTP Client — dış API adapter

Repositories/
  IRepository.cs / EfRepository.cs     Generic repository
  IKitapRepository.cs / EfKitapRepository.cs
  IUnitOfWork.cs / EfUnitOfWork.cs

Features/ (CQRS + MediatR)
  Kitaplar/Queries/         IRequest<T> sorgu handler'ları
  Kitaplar/Commands/        IRequest<T> komut handler'ları
  Behaviours/               Pipeline: logging, validation

Data/
  KitabeviDbContext.cs      DbContext, Fluent API, ilişkiler

Filters/
  ValidationFilter, PerformansFilter, AuditFilter, GlobalHataFilter
```

---

## Faz 3 — Mimari ve Design Patterns 🔄 (devam ediyor)

| Gün | Dosya | Konu |
|-----|-------|------|
| 46 | [gun46_srp.md](Faz3-Mimari/gun46_srp.md) | Single Responsibility Principle |
| 47 | [gun47_ocp.md](Faz3-Mimari/gun47_ocp.md) | Open/Closed Principle |
| 48 | [gun48_lsp_isp.md](Faz3-Mimari/gun48_lsp_isp.md) | Liskov Substitution + Interface Segregation |
| 49 | [gun49_dip.md](Faz3-Mimari/gun49_dip.md) | Dependency Inversion Principle |
| 50 | [gun50_solid_sentez.md](Faz3-Mimari/gun50_solid_sentez.md) | SOLID Sentez — teknik borç, test edilebilirlik |
| 51 | [gun51_creational_patterns.md](Faz3-Mimari/gun51_creational_patterns.md) | Factory Method, Builder, Singleton |
| 52 | [gun52_hafta7_ozet.md](Faz3-Mimari/gun52_hafta7_ozet.md) | Hafta 7 özet |
| 53 | [gun53_structural_patterns.md](Faz3-Mimari/gun53_structural_patterns.md) | Decorator, Adapter, Facade |
| 54 | [gun54_behavioral_patterns_1.md](Faz3-Mimari/gun54_behavioral_patterns_1.md) | Strategy, Observer, Command |
| 55 | [gun55_behavioral_patterns_2.md](Faz3-Mimari/gun55_behavioral_patterns_2.md) | Mediator, State, Iterator |

### Demo Projeleri

```
Faz3-Mimari/
├── 01-SOLID/
│   ├── SrpDemo/        SRP: UserService god class → ayrılmış servisler
│   ├── OcpDemo/        OCP: if/switch → IIndirimStrategy
│   ├── LspIspDemo/     LSP: DijitalKitap kalıtım sorunu / ISP: fat interface bölme
│   └── DipDemo/        DIP: IKitapRepository domain'de, EfKitapRepository infrastructure'da
│
└── 02-DesignPatterns/
    ├── CreationalDemo/ Factory Method (exporter), Builder (fluent sorgu)
    ├── StructuralDemo/ Decorator (cache), Adapter (dış API), Facade (sipariş akışı)
    ├── BehavioralDemo1/ Strategy (sıralama), Observer (event), Command (undo)
    └── BehavioralDemo2/ Mediator (handler yönlendirme), State (sipariş durumu), Iterator (yield/async)
```

---

## Teknolojiler

| Teknoloji | Faz |
|-----------|-----|
| .NET 9 / C# 13 | Tüm fazlar |
| ASP.NET Core MVC | Faz 2 |
| Entity Framework Core 9 | Faz 2 |
| MediatR | Faz 2–3 |
| Serilog | Faz 2 |
| xUnit, Moq, FluentAssertions, TestContainers | Faz 2 |
| NetArchTest | Faz 2–3 |
| Docker, docker-compose | Faz 2–5 |
| BenchmarkDotNet, Redis | Faz 4 |
| RabbitMQ, YARP, Saga | Faz 5 |
