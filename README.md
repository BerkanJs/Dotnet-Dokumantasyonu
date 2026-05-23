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
├── Faz3-Mimari/            SOLID + Design Patterns + Onion + CQRS
│   ├── 01-SOLID/           Her prensip için izole demo
│   ├── 02-DesignPatterns/  Creational, Structural, Behavioral
│   ├── 03-OnionArchitecture/   KitabeviMVC → Onion refactor
│   └── 04-CQRS/            MediatR + Pipeline Behaviors
├── Faz4-Performans/        BenchmarkDotNet, Redis, SQL Index, gRPC, SignalR
└── Faz5-Mikroservisler/    YARP, RabbitMQ, Kafka, Saga  ← şu an burada
    ├── ApiGateway/          YARP reverse proxy
    ├── CatalogService/      Faz3 Onion → mikroservis
    ├── OrderService/        Saga + Outbox + RabbitMQ
    ├── NotificationService/ RabbitMQ consumer
    └── docker/              docker-compose
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

---

## Faz 3 — Mimari ve Design Patterns ✅

| Gün | Dosya | Konu |
|-----|-------|------|
| 46 | [gun46_srp.md](Faz3-Mimari/gun46_srp.md) | Single Responsibility Principle |
| 47 | [gun47_ocp.md](Faz3-Mimari/gun47_ocp.md) | Open/Closed Principle |
| 48 | [gun48_lsp_isp.md](Faz3-Mimari/gun48_lsp_isp.md) | Liskov Substitution + Interface Segregation |
| 49 | [gun49_dip.md](Faz3-Mimari/gun49_dip.md) | Dependency Inversion Principle |
| 50 | [gun50_solid_sentez.md](Faz3-Mimari/gun50_solid_sentez.md) | SOLID Sentez |
| 51 | [gun51_creational_patterns.md](Faz3-Mimari/gun51_creational_patterns.md) | Factory Method, Builder, Singleton |
| 52 | [gun52_hafta7_ozet.md](Faz3-Mimari/gun52_hafta7_ozet.md) | Hafta 7 özet |
| 53 | [gun53_structural_patterns.md](Faz3-Mimari/gun53_structural_patterns.md) | Decorator, Adapter, Facade |
| 54 | [gun54_behavioral_patterns_1.md](Faz3-Mimari/gun54_behavioral_patterns_1.md) | Strategy, Observer, Command |
| 55 | [gun55_behavioral_patterns_2.md](Faz3-Mimari/gun55_behavioral_patterns_2.md) | Mediator, State, Iterator |
| 56 | [gun56_anti_patterns.md](Faz3-Mimari/gun56_anti_patterns.md) | Anti-Patterns |
| 57 | [gun57_ddd_taktiksel.md](Faz3-Mimari/gun57_ddd_taktiksel.md) | DDD Taktiksel |
| 58 | [gun58_hafta8_ozet.md](Faz3-Mimari/gun58_hafta8_ozet.md) | Hafta 8 özet |
| 58b | [gun58_onion_architecture.md](Faz3-Mimari/gun58_onion_architecture.md) | Onion Architecture |
| 59 | [gun59_onion_uygulama.md](Faz3-Mimari/gun59_onion_uygulama.md) | Onion Uygulama |
| 60 | [gun60_cqrs_mediatr.md](Faz3-Mimari/gun60_cqrs_mediatr.md) | CQRS & MediatR (derin) |
| 61 | [gun61_hafta_ozet.md](Faz3-Mimari/gun61_hafta_ozet.md) | Hafta özet |
| 61b | [gun61_kitap_ekle_usecase.md](Faz3-Mimari/gun61_kitap_ekle_usecase.md) | Kitap Ekle Use Case |
| 62 | [gun62_decorator_cache.md](Faz3-Mimari/gun62_decorator_cache.md) | Decorator & Cache |
| 63 | [gun63_mediatr_pipeline_behaviors.md](Faz3-Mimari/gun63_mediatr_pipeline_behaviors.md) | MediatR Pipeline Behaviors |
| 64 | [gun64_result_pattern_ve_hata_yonetimi.md](Faz3-Mimari/gun64_result_pattern_ve_hata_yonetimi.md) | Result Pattern & Hata Yönetimi |
| 65 | [gun65_hafta9_ozet.md](Faz3-Mimari/gun65_hafta9_ozet.md) | Hafta 9 özet |
| 66 | [gun66_event_driven_architecture_temelleri.md](Faz3-Mimari/gun66_event_driven_architecture_temelleri.md) | Event-Driven Architecture |
| 67 | [gun67_specification_pattern.md](Faz3-Mimari/gun67_specification_pattern.md) | Specification Pattern |
| 68 | [gun68_unit_of_work.md](Faz3-Mimari/gun68_unit_of_work.md) | Unit of Work |
| 69 | [gun69_validation_stratejileri.md](Faz3-Mimari/gun69_validation_stratejileri.md) | Validation Stratejileri |
| 70 | [gun70_vertical_slice.md](Faz3-Mimari/gun70_vertical_slice.md) | Vertical Slice Architecture |
| 71 | [gun71_moduler_monolith.md](Faz3-Mimari/gun71_moduler_monolith.md) | Modüler Monolith |
| 72 | [gun72_hafta10_ozet_faz3_kapanis.md](Faz3-Mimari/gun72_hafta10_ozet_faz3_kapanis.md) | Hafta 10 Özeti & Faz3 Kapanış |

---

## Faz 4 — Performans & İleri Konular ✅

| Gün | Dosya | Konu |
|-----|-------|------|
| 73 | [gun73_allocation_benchmarkdotnet.md](Faz4-Performans/gun73_allocation_benchmarkdotnet.md) | BenchmarkDotNet & Allokasyon |
| 74 | [gun74_span_memory_arraypool.md](Faz4-Performans/gun74_span_memory_arraypool.md) | Span, Memory & ArrayPool |
| 75 | [gun75_struct_readonly_ref.md](Faz4-Performans/gun75_struct_readonly_ref.md) | Struct, Readonly & Ref |
| 76 | [gun76_string_optimizasyonlari.md](Faz4-Performans/gun76_string_optimizasyonlari.md) | String Optimizasyonları |
| 77 | [gun77_async_performans_valuetask.md](Faz4-Performans/gun77_async_performans_valuetask.md) | Async Performans & ValueTask |
| 78 | [gun78_memory_leak.md](Faz4-Performans/gun78_memory_leak.md) | Memory Leak Tespiti |
| 79 | [gun79_production_diagnostics.md](Faz4-Performans/gun79_production_diagnostics.md) | Production Diagnostics |
| 80 | [gun80_streams_pipelines.md](Faz4-Performans/gun80_streams_pipelines.md) | Streams & Pipelines |
| 81 | [gun81_hafta11_ozet.md](Faz4-Performans/gun81_hafta11_ozet.md) | Hafta 11 özet |
| 82 | [gun82_thread_safety.md](Faz4-Performans/gun82_thread_safety.md) | Thread Safety |
| 83 | [gun83_tpl.md](Faz4-Performans/gun83_tpl.md) | TPL (Task Parallel Library) |
| 84 | [gun84_channel_producer_consumer.md](Faz4-Performans/gun84_channel_producer_consumer.md) | Channel & Producer-Consumer |
| 85 | [gun85_threadpool_task_scheduling.md](Faz4-Performans/gun85_threadpool_task_scheduling.md) | ThreadPool & Task Scheduling |
| 86 | [gun86_immutability_functional.md](Faz4-Performans/gun86_immutability_functional.md) | Immutability & Functional Style |
| 88 | [gun88_compression_caching_cdn.md](Faz4-Performans/gun88_compression_caching_cdn.md) | Compression, Caching & CDN |
| 89 | [gun89_rate_limiting.md](Faz4-Performans/gun89_rate_limiting.md) | Rate Limiting |
| 90 | [gun90_minimal_api_performance.md](Faz4-Performans/gun90_minimal_api_performance.md) | Minimal API Performans |
| 91 | [gun91_sql_index_stratejisi.md](Faz4-Performans/gun91_sql_index_stratejisi.md) | SQL Index Stratejisi |
| 92 | [gun92_redis_distributed_cache.md](Faz4-Performans/gun92_redis_distributed_cache.md) | Redis Distributed Cache |
| 93 | [gun93_health_checks.md](Faz4-Performans/gun93_health_checks.md) | Health Checks |
| 94 | [gun94_hafta13_ozet.md](Faz4-Performans/gun94_hafta13_ozet.md) | Hafta 13 özet |
| 95 | [gun95_soft_delete_query_filters.md](Faz4-Performans/gun95_soft_delete_query_filters.md) | Soft Delete & Query Filters |
| 96 | [gun96_audit_trail.md](Faz4-Performans/gun96_audit_trail.md) | Audit Trail |
| 97 | [gun97_optimistic_concurrency.md](Faz4-Performans/gun97_optimistic_concurrency.md) | Optimistic Concurrency |
| 98 | [gun98_multi_tenancy.md](Faz4-Performans/gun98_multi_tenancy.md) | Multi-Tenancy |
| 99 | [gun99_ef_core_interceptors.md](Faz4-Performans/gun99_ef_core_interceptors.md) | EF Core Interceptors |
| 100 | [gun100_idempotency.md](Faz4-Performans/gun100_idempotency.md) | Idempotency |
| 101 | [gun101_structured_logging.md](Faz4-Performans/gun101_structured_logging.md) | Yapısal Loglama |
| 102 | [gun102_problem_details.md](Faz4-Performans/gun102_problem_details.md) | Problem Details |
| 103 | [gun103_data_protection_sifreleme.md](Faz4-Performans/gun103_data_protection_sifreleme.md) | Data Protection & Şifreleme |
| 104 | [gun104_domain_event_dispatch.md](Faz4-Performans/gun104_domain_event_dispatch.md) | Domain Event Dispatch |
| 105 | [gun105_feature_flags.md](Faz4-Performans/gun105_feature_flags.md) | Feature Flags |
| 106 | [gun106_oauth_jwt_api_key.md](Faz4-Performans/gun106_oauth_jwt_api_key.md) | OAuth, JWT & API Key |
| 107 | [gun107_cors_csrf.md](Faz4-Performans/gun107_cors_csrf.md) | CORS & CSRF |
| 108 | [gun108_sql_injection_xss_headers.md](Faz4-Performans/gun108_sql_injection_xss_headers.md) | SQL Injection, XSS & Headers |
| 109 | [gun109_owasp_api_top10.md](Faz4-Performans/gun109_owasp_api_top10.md) | OWASP API Top 10 |
| 110 | [gun110_hybrid_cache.md](Faz4-Performans/gun110_hybrid_cache.md) | Hybrid Cache |
| 111 | [gun111_identity_server_keycloak_oidc.md](Faz4-Performans/gun111_identity_server_keycloak_oidc.md) | Identity Server & Keycloak OIDC |
| 112 | [gun112_grpc_protobuf_temelleri.md](Faz4-Performans/gun112_grpc_protobuf_temelleri.md) | gRPC & Protobuf Temelleri |
| 113 | [gun113_grpc_service_types_aspnetcore.md](Faz4-Performans/gun113_grpc_service_types_aspnetcore.md) | gRPC Service Types & ASP.NET Core |
| 114 | [gun114_grpc_json_transcoding_rest_vs_grpc.md](Faz4-Performans/gun114_grpc_json_transcoding_rest_vs_grpc.md) | gRPC JSON Transcoding & REST vs gRPC |
| 115 | [gun115_realtime_temelleri_signalr_giris.md](Faz4-Performans/gun115_realtime_temelleri_signalr_giris.md) | Realtime Temelleri & SignalR Giriş |
| 116 | [gun116_signalr_hub_groups_connections.md](Faz4-Performans/gun116_signalr_hub_groups_connections.md) | SignalR Hub, Groups & Connections |
| 117 | [gun117_signalr_scale_out_auth_resilience.md](Faz4-Performans/gun117_signalr_scale_out_auth_resilience.md) | SignalR Scale-Out, Auth & Resilience |
| 118 | [gun118_signalr_vs_grpc_karsilastirma.md](Faz4-Performans/gun118_signalr_vs_grpc_karsilastirma.md) | SignalR vs gRPC Karşılaştırma |

---

## Faz 5 — Mikroservisler 🔄 (devam ediyor)

| Gün | Dosya | Konu |
|-----|-------|------|
| 119 | [gun119_monolith_microservice.md](Faz5-Mikroservisler/gun119_monolith_microservice.md) | Monolith → Microservice: Ne Zaman? |
| 120 | [gun120_bounded_context.md](Faz5-Mikroservisler/gun120_bounded_context.md) | Bounded Context ve Servis Sınırları |
| 121 | [gun121_servisler_arasi_iletisim.md](Faz5-Mikroservisler/gun121_servisler_arasi_iletisim.md) | Servisler Arası İletişim |
| 122 | [gun122_api_gateway.md](Faz5-Mikroservisler/gun122_api_gateway.md) | API Gateway & YARP |
| 123 | [gun123_service_discovery.md](Faz5-Mikroservisler/gun123_service_discovery.md) | Service Discovery & Load Balancing |
| 124 | [gun124_hafta17_ozet.md](Faz5-Mikroservisler/gun124_hafta17_ozet.md) | Hafta 17 Özeti |
| 125 | [gun125_rabbitmq.md](Faz5-Mikroservisler/gun125_rabbitmq.md) | RabbitMQ: AMQP, Exchange, DLQ, MassTransit |

---

## Teknolojiler

| Teknoloji | Faz |
|-----------|-----|
| .NET 9 / C# 13 | Tüm fazlar |
| ASP.NET Core MVC | Faz 2 |
| Entity Framework Core 9 | Faz 2–3 |
| MediatR | Faz 2–3 |
| Serilog | Faz 2 |
| xUnit, Moq, FluentAssertions, TestContainers | Faz 2 |
| NetArchTest | Faz 2–3 |
| Docker, docker-compose | Faz 2–5 |
| BenchmarkDotNet, Redis, SignalR, gRPC | Faz 4 |
| YARP, RabbitMQ, Kafka, Saga, .NET Aspire | Faz 5 |
