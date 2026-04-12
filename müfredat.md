# .NET Mid+ Seviye Geliştirici Müfredatı
## Java/Spring Boot / Go / Node.js Tecrübeli Geliştirici İçin

> **Hedef:** 22 haftada güçlü temeller üzerine inşa edilmiş, mikroservis mimarisi kurabilen, performans bilinciyle kod okuyabilen/tasarlayabilen Mid+ .NET geliştirici.
>
> **Felsefe:** Kod yazmak değil, mimariyi anlamak önce gelir. Her konuda "neden böyle tasarlandı?" sorusu sorulacak.
>
> **Çalışma Yöntemi:** Her faz öncesinde konu anlatımı yapılır, ardından birlikte kod yazılır. Karmaşık değil, doğrudan konuyu pekiştiren basit örnekler.

---

## PROJE YAPISI

Her faz için ayrı bir klasör var. Bir fazı bitirmeden diğerine geçilmiyor.
Her faz öncesinde konu anlatımı yapılır, ardından o klasörde birlikte kod yazılır.

```
NetPerfectionKursu/
│
├── Faz1-CSharp-CLR/
│   ├── 01-GC-Memory/          → GC, Stack/Heap, IDisposable demoları
│   ├── 02-ValueRef-Types/     → struct, class, boxing, Span<T> demoları
│   ├── 03-Async-Await/        → Task, ValueTask, async patterns
│   ├── 04-LINQ-Generics/      → LINQ derinlik, generics, delegates
│   └── 05-DI-Patterns/        → DI container, lifetime, extension methods
│
├── Faz2-ASPNET-MVC/
│   └── KitabeviMVC/           → Kitabevi uygulaması (MVC + EF Core + Auth)
│
├── Faz3-Mimari/
│   ├── 01-SOLID/              → SOLID prensip demoları
│   ├── 02-DesignPatterns/     → GoF pattern demoları
│   ├── 03-OnionArchitecture/  → Kitabevi Onion refactor
│   └── 04-CQRS/               → MediatR + CQRS + Pipeline Behaviors
│
├── Faz4-Performans/
│   ├── 01-Benchmarks/         → BenchmarkDotNet ölçümleri
│   ├── 02-Memory-Optimization/→ Span<T>, ArrayPool, allocation analizi
│   └── 03-EFCore-Tuning/      → N+1, compiled queries, batch ops
│
└── Faz5-Mikroservisler/
    ├── ApiGateway/            → YARP tabanlı gateway
    ├── CatalogService/        → Kitap kataloğu servisi
    ├── OrderService/          → Sipariş servisi (Saga pattern)
    ├── NotificationService/   → Bildirim servisi (RabbitMQ consumer)
    └── docker/                → docker-compose.yml
```

### Nasıl Çalışacağız?

1. **Konu anlatımı** — Faz başlamadan önce teorik bilgi aktarımı yapılır
2. **Kod yazımı** — Basit, konuyu doğrudan gösteren örnekler birlikte yazılır
3. **Soru-Cevap** — "Neden böyle?" soruları cevaplanır, alternatifler tartışılır
4. **Sonraki faza geçiş** — Önceki faz koduna referansla ilerlenir

> **Alan:** Faz1–3 boyunca "Kitabevi" domain'i kullanılır.
> Aynı problem önce basit → sonra MVC → sonra Onion → sonra mikroservis olarak evrilir.

---

## GENEL BAKIS — 5 FAZLI YOL HARITASI

| Faz | Hafta | Konu | Seviye |
|-----|-------|------|--------|
| 1 | 1–2 | C# & CLR Derinlikleri | Temel/Orta |
| 2 | 3–6 | ASP.NET Core, MVC, EF Core, **Test Yazımı** | Orta |
| 3 | 7–11 | Mimari, SOLID, CQRS, **Vertical Slice**, **Modüler Monolith** | Orta/İleri |
| 4 | 12–16 | Performans, Redis, SQL, **Niş Production Patterns**, **Production Diagnostics** | İleri |
| 5 | 17–28 | Mikroservisler, **.NET Aspire**, Kubernetes, CI/CD | Senior Hazırlık |

> Toplam süre: **22 → 28 hafta** (3 araştırma turu sonrası).
> Her ekleme araştırmayla doğrulandı — eksik konu değil, endüstri standardı olan konular.

---

# FAZ 1 — C# & CLR: Dilin Motoru

> Java/Go bilen biri için C# kolayı öğrenmek değil, C#'ın Java'dan farkını anlamak demektir. Bu faz teorik ağırlıklıdır.

### Bu Fazda Kodlayacaklarımız (`Faz1-CSharp-CLR/`)

| Klasör | Ne yapılır |
|--------|-----------|
| `01-GC-Memory/` | GC nesil demosu, IDisposable pattern, finalizer tehlikesi |
| `02-ValueRef-Types/` | Boxing maliyeti ölçümü, struct vs class karşılaştırma |
| `03-Async-Await/` | async/await state machine, deadlock demo, CancellationToken |
| `04-LINQ-Generics/` | Deferred execution tuzağı, kovaryans/kontravaryans örnekleri |
| `05-DI-Patterns/` | Manuel DI container, lifetime farkları (captive dependency) |

> Kod karmaşıklığı düşük tutulur — her dosya tek bir kavramı gösterir.

---

## Hafta 1 — CLR, Tip Sistemi ve Bellek Modeli

### Gün 1 — CLR Nedir? JVM ile Karşılaştırma

**Teorik:**
- Common Language Runtime (CLR) mimarisi
- JVM vs CLR farkları (bytecode → IL → JIT)
- Managed vs Unmanaged kod kavramı
- AppDomain → AssemblyLoadContext evrimi (.NET Core ile birlikte)
- Ahead-of-Time (AOT) compilation vs Just-in-Time (JIT) — .NET 7+ Native AOT

**Sorular:**
- Java'da class loading nasıl çalışır? CLR'de farkı ne?
- IL (Intermediate Language) nedir, neden platform bağımsızlık sağlar?
- `dotnet publish -r win-x64 --self-contained` ne yapar?

---

### Gün 2 — Stack, Heap ve Bellek Modeli

**Teorik:**
- Stack vs Heap — .NET perspektifinden
- Value Types (struct, int, bool, DateTime) → Stack'te mi yaşar? (Yanıltıcı soru — bağlama göre değişir)
- Reference Types (class, string, array) → Heap'te yaşar
- Boxing / Unboxing — performans kabusu neden?
- `Span<T>` ve `Memory<T>` — modern .NET'in stack-friendly yaklaşımı
- Pinned Objects ve LOH (Large Object Heap) neden sorunludur?

**Java ile karşılaştırma:**
- Java'da her şey Object (primitifler auto-box olur) — C#'ta struct ile kaçınılır
- C# `readonly struct` performans avantajı

**Oku:**
- CLR via C# (Jeffrey Richter) — Bölüm 4, 5 (Tip temelleri)

---

### Gün 3 — Garbage Collector: Derinlemesine

**Teorik:**
- .NET GC — Generational GC (Gen 0, Gen 1, Gen 2)
- Gen 0 neden küçük ve hızlı? Object lifetime hipotezi
- Large Object Heap (LOH) — 85KB üzeri nesneler, neden sorunlu?
- Pinned Object Heap (POH) — .NET 5+ ile gelen çözüm
- GC Modes: Workstation GC vs Server GC
- Background GC vs Concurrent GC
- GC.Collect() neden kötü pratik?
- `IDisposable` ve `using` — deterministic cleanup
- Finalizer (~Destructor) neden tehlikelidir? (GC sürecini uzatır)

**Java ile karşılaştırma:**
- Java G1GC vs .NET GC — hangi sorunları çözer?
- Java'da `finalize()` → deprecated, C#'ta da finalizer kaçınılır

**Kritik kavram:** Dispose Pattern — IDisposable doğru nasıl implement edilir?
```
Managed kaynak → Dispose() ile temizle
Unmanaged kaynak → Finalizer VE Dispose() ile temizle
GC.SuppressFinalize(this) → Dispose çağrıldıysa finalizer çalıştırma
```

---

### Gün 4 — Value Types vs Reference Types: Derin Analiz

**Teorik:**
- `struct` vs `class` — ne zaman hangisi?
- Record types (C# 9+) — immutable data modelleri
- `readonly struct` — defensive copying neden olur, nasıl önlenir?
- Ref returns ve ref locals
- `in` parametresi — readonly reference passing
- `ref struct` — stack-only tipler (Span<T> bunun örneği)

**Pratik kural:**
```
struct kullan: küçük (<16 byte), immutable, value semantics gereken yerde
class kullan: büyük, mutable, polimorfizm gereken yerde
record kullan: immutable DTO/Value Object için
```

---

### Gün 5 — String ve Koleksiyonlar: Performans Tuzakları

**Teorik:**
- String immutability — Java gibi, ama `string.Intern()` farkı
- String interning — CLR'nin string pool'u
- `StringBuilder` ne zaman kullanılır? (Concat loop = O(n²))
- `Span<char>` ve `StringSegment` — allocation-free string işlemleri
- Koleksiyonlar: `List<T>` vs `Array` vs `Span<T>` vs `IEnumerable<T>`
- `IEnumerable<T>` deferred execution — LINQ lazy evaluation
- `IQueryable<T>` vs `IEnumerable<T>` farkı (ORM'de kritik)

**Memory leak tuzağı:**
- Event handler subscription → memory leak nasıl olur?
- `WeakReference<T>` ne zaman kullanılır?

---

### Gün 6 — Async/Await ve Task Parallel Library (TPL)

**Teorik:**
- C# async/await — JavaScript/Go'dan farkı ne?
- `Task<T>` vs `ValueTask<T>` — allocation farkı
- ThreadPool nasıl çalışır? I/O Completion Ports (Windows) / io_uring (Linux)
- `ConfigureAwait(false)` — neden önemli? ASP.NET Core'da neden genellikle gereksiz?
- async void neden tehlikelidir?
- `CancellationToken` — cooperative cancellation modeli
- `SemaphoreSlim` vs `lock` — async context'te locking
- Deadlock senaryoları — `.Result` ve `.Wait()` neden async context'te yasak?

**Go ile karşılaştırma:**
- Go goroutine vs C# Task — hangisi daha lightweight?
- Go channel vs C# Channel<T> / BlockingCollection<T>

---

### Gün 7 — Hafta 1 Özet ve Derinleştirme

**Hafta 1 Genel Tekrar Soruları:**
1. Boxing neden performans sorunudur? Hangi durumlarda kaçınılmaz?
2. Gen 2 GC ne zaman tetiklenir? Bunu nasıl minimize ederiz?
3. `IDisposable` implement etmeyen bir class unmanaged kaynak tutabilir mi? Nasıl?
4. async/await state machine olarak nasıl derlenir?
5. `ValueTask<T>` ne zaman `Task<T>`'den daha iyi?

**Kaynak:** CLR via C# — Bölüm 7 (Constants and Fields), 8 (Methods)

---

## Hafta 2 — C# İleri Kavramlar: Generics, LINQ, Reflection

### Gün 8 — Generics: Kovaryans, Kontravaryans

**Teorik:**
- C# Generics vs Java Generics — Type Erasure YOK, reified generics
- CLR'de generics nasıl çalışır? (Value type için ayrı kod üretilir!)
- Kovaryans (`out`) ve Kontravaryans (`in`) — `IEnumerable<out T>`, `Action<in T>`
- Generic constraints: `where T : class`, `where T : struct`, `where T : new()`
- Generic interface'lerde default implementasyon (C# 8+)

**Java ile karşılaştırma:**
- Java wildcard (`? extends`, `? super`) vs C# kovaryans/kontravaryans
- Neden C# generics runtime'da daha verimli?

---

### Gün 9 — LINQ: Derinlemesine ve Performans

**Teorik:**
- LINQ'nun query expression vs method syntax — IL'de aynı şey
- Deferred execution — `IEnumerable<T>` pipeline'ı
- `yield return` — iterator state machine
- LINQ extension methods — C# extension method mekanizması
- `Where().Select().OrderBy()` zinciri — kaç pass?
- `ToList()` ne zaman erken çağırmak gerekir?

**Performans tuzakları:**
- `Count()` vs `Any()` — neden `Any()` daha iyi?
- `First()` vs `FirstOrDefault()` — exception vs null
- `Select()` içinde `ToList()` çağırmak
- Multiple enumeration warning — `IEnumerable<T>` iki kez iterate

---

### Gün 10 — Delegates, Events ve Functional Patterns

**Teorik:**
- Delegate nedir? Multicast delegate nasıl çalışır?
- `Action<T>`, `Func<T, TResult>`, `Predicate<T>` — built-in delegates
- Lambda expressions → compiler'ın ne ürettiği
- Closure — captured variables ve heap allocation
- Event keyword — delegate üzerine syntactic sugar
- Observer pattern'in C# natif implementasyonu
- `IObservable<T>` / `IObserver<T>` — Reactive Extensions temeli

**Bellek tuzağı:**
```
Event subscription memory leak:
publisher.SomethingHappened += subscriber.HandleIt;
// publisher, subscriber'ı hayatta tutar!
// publisher.SomethingHappened -= subscriber.HandleIt; unutulursa leak
```

---

### Gün 11 — Reflection, Attributes ve Source Generators

**Teorik:**
- Reflection — runtime type inspection maliyeti
- `Type.GetMethod()` vs cached MethodInfo vs compiled expression
- Attributes — metadata sistemi, AOP için temel
- Custom attribute yazmak
- `Activator.CreateInstance()` neden yavaş?
- Expression Trees — LINQ provider'ların temeli (EF Core bunları kullanır)
- Source Generators (C# 9+) — compile-time code generation, reflection alternatifi

**Performans kural:**
```
Reflection → sadece başlangıçta, cache'le
Source Generator → compile-time, zero runtime cost
Expression Trees → runtime'da ama JIT-friendly
```

---

### Gün 12 — Pattern Matching ve Modern C# Özellikleri

**Teorik:**
- Pattern Matching (C# 7-12) — switch expression, property patterns
- Nullable Reference Types (C# 8+) — null safety derleme zamanında
- Record types — positional records, `with` expression
- Init-only properties
- Top-level statements ve minimal code
- Global using directives
- Raw string literals (C# 11+)
- Required members (C# 11+)

**Mimari önemi:**
- Nullable reference types → domain model'i daha açık hale getirir
- Record types → Value Object pattern için ideal
- Pattern matching → switch'i functional yapar (discriminated union yaklaşımı)

---

### Gün 13 — Dependency Injection: C# Perspektifinden

**Teorik:**
- DI Container nasıl çalışır? Object graph inşası
- Lifetime yönetimi: Transient, Scoped, Singleton — farkları ve riskleri
- Captive Dependency Antipattern — Singleton içine Scoped inject etme
- `IServiceProvider` ve service locator antipattern
- `IServiceCollection` extension method pattern
- Pure DI vs Container — ne zaman hangisi?

**Spring ile karşılaştırma:**
- Spring IoC Container vs .NET DI — kavramsal farklar
- Spring'deki `@Autowired` = .NET Constructor Injection

---

### Gün 14 — Hafta 2 Özet ve C# Ekosistemi

**Hafta 2 Genel Tekrar Soruları:**
1. Java generics ile C# generics arasındaki temel fark ne ve neden önemli?
2. Closure neden heap allocation yaratır?
3. `IEnumerable<T>` deferred execution'ın tehlikeli olduğu bir senaryo?
4. Singleton içine Scoped inject etmenin sonucu ne?
5. Source Generator'ı Reflection'dan ayıran temel fark ne?

---

# FAZ 2 — ASP.NET Core & MVC

> Spring MVC bilen biri için ASP.NET Core MVC tanıdık gelir. Fark: middleware pipeline, minimal API ve DI sistemi.

### Bu Fazda Kodlayacaklarımız (`Faz2-ASPNET-MVC/KitabeviMVC/`)

Tek uygulama, kademeli olarak büyür:

| Aşama | Ne eklenir |
|-------|-----------|
| Başlangıç | MVC proje iskeleti, routing, controller, view |
| Orta | EF Core + SQLite, Repository pattern, model validation |
| İleri | JWT auth, action filters, middleware, hata yönetimi |
| Final | Health check, loglama (Serilog), caching |

> Kitabevi: Kitap listeleme, ekleme, güncelleme, silme + kullanıcı girişi.

---

## Hafta 3 — ASP.NET Core Temelleri

### Gün 15 — Middleware Pipeline: Derinlemesine

**Teorik:**
- `WebApplication` ve `WebApplicationBuilder` — host modeli
- Middleware nedir? Onion/chain yapısı
- `HttpContext` — her request için yaratılır, managed
- Middleware sıralaması neden kritik? (Auth → Routing → Endpoint)
- `Use()` vs `Run()` vs `Map()` farkı
- Short-circuit — response yazıldıktan sonra pipeline devam etmez
- Custom middleware yazmak — class-based vs inline

**Spring ile karşılaştırma:**
- Spring Filter/Interceptor vs ASP.NET Middleware — kavramsal örtüşme
- Servlet container'ın yerini Kestrel alıyor

---

### Gün 16 — Kestrel, IIS ve Hosting Modeli

**Teorik:**
- Kestrel — cross-platform, yüksek performanslı HTTP server
- In-process vs Out-of-process hosting (IIS ile)
- `Program.cs` evrimi — Generic Host → WebApplication
- Environment: Development, Staging, Production
- `appsettings.json` ve environment-specific override
- Configuration providers hiyerarşisi (JSON < Env < Command line < Secret)
- `IOptions<T>`, `IOptionsMonitor<T>`, `IOptionsSnapshot<T>` farkları

---

### Gün 17 — Routing: Convention vs Attribute

**Teorik:**
- Endpoint routing (ASP.NET Core 3+) — middleware'den bağımsız routing
- Convention-based routing: `{controller}/{action}/{id?}`
- Attribute routing: `[Route]`, `[HttpGet]`, `[HttpPost]`
- Route constraints: `{id:int}`, `{slug:regex(...)}`
- Route value ambiguity ve order
- Minimal API routing — .NET 6+ lambda tabanlı
- `LinkGenerator` — URL üretimi controller bağımsız

---

### Gün 18 — MVC Pattern: Controller, Action, View

**Teorik:**
- MVC'nin .NET implementasyonu
- Controller lifecycle — her request'te yeni instance
- Action method return types: `IActionResult`, `ActionResult<T>`, `OkObjectResult`
- Model Binding — request → C# object dönüşümü
- `[FromBody]`, `[FromRoute]`, `[FromQuery]`, `[FromHeader]`, `[FromForm]`
- Model Validation — DataAnnotations vs FluentValidation
- `ModelState.IsValid` — validation pipeline
- View ve Razor syntax — `@model`, `@Html`, Tag Helpers

---

### Gün 19 — Filters: Cross-Cutting Concerns

**Teorik:**
- Filter pipeline: Authorization → Resource → Action → Result → Exception
- `IActionFilter`, `IResultFilter`, `IExceptionFilter`, `IAuthorizationFilter`
- Sync vs Async filter interface'leri
- Global, Controller, Action seviyesinde filter
- `[TypeFilter]` vs `[ServiceFilter]` — DI ile filter
- Exception handling: `IExceptionFilter` vs `UseExceptionHandler` middleware
- `ProblemDetails` — RFC 7807 standart hata yanıtı

**Spring ile karşılaştırma:**
- Spring AOP `@Around` vs ASP.NET Action Filter — benzer ama farklı

---

### Gün 20 — Authentication & Authorization

**Teorik:**
- Authentication vs Authorization ayrımı
- Cookie authentication vs JWT Bearer
- JWT anatomisi — Header.Payload.Signature
- `ClaimsPrincipal` ve `ClaimsIdentity` modeli
- Policy-based authorization — `[Authorize(Policy = "AdminOnly")]`
- Resource-based authorization — `IAuthorizationHandler`
- OAuth 2.0 / OpenID Connect kavramları
- IdentityServer / Keycloak ile entegrasyon mantığı

---

### Gün 21 — Hafta 3 Özet

**Tekrar soruları:**
1. Middleware sırasında response yazılırsa ne olur?
2. `IOptions<T>` vs `IOptionsMonitor<T>` — runtime reload hangisi?
3. Model Binding sırası neden önemli? Aynı isimde query ve body parametresi olursa ne olur?
4. Action Filter vs Middleware — ne zaman hangisi?
5. JWT'de imza doğrulaması nasıl çalışır?

---

## Hafta 4 — ASP.NET Core API: Modern Yaklaşımlar

### Gün 22 — REST API Tasarım Prensipleri

**Teorik:**
- REST kısıtlamaları — uniform interface, stateless, cacheable
- Resource modeling — entity değil kaynak
- HTTP method semantikleri — GET idempotent, POST değil
- Status code seçimi — 200 vs 201 vs 204 vs 400 vs 404 vs 409 vs 422
- HATEOAS — ne zaman gerekli, ne zaman overkill?
- API versioning stratejileri — URL path vs header vs query string
- `Asp.Versioning` paketi

---

### Gün 23 — Minimal API vs Controller-Based API

**Teorik:**
- Minimal API — .NET 6+ lambda based endpoints
- Controller vs Minimal API — trade-off'lar
- `IEndpointRouteBuilder` extension pattern
- `TypedResults` — compile-time OpenAPI desteği
- Minimal API'de Filters
- `EndpointFilter` vs Action Filter
- Ne zaman Minimal API, ne zaman Controller?

**Kural:**
```
Minimal API → microservice, basit CRUD, az bağımlılık
Controller → karmaşık domain, büyük ekip, filter-heavy logic
```

---

### Gün 24 — OpenAPI / Swagger ve API Dokümantasyonu

**Teorik:**
- OpenAPI spec 3.x
- Swashbuckle vs NSwag vs built-in .NET 9 OpenAPI
- `[ProducesResponseType]` ve XML comment'ler
- API consumer'lar için SDK generation
- Scalar (modern Swagger UI alternatifi)

---

### Gün 25 — Background Services ve Job Scheduling: Derin Karşılaştırma

**Teorik:**
- `IHostedService` ve `BackgroundService` — native .NET, hafif, library-free
- Hosted service lifetime — host ile başlar, biter
- `IHostApplicationLifetime` — graceful shutdown, `StoppingToken`
- `Channel<T>` — producer/consumer pattern, back-pressure
- `PeriodicTimer` (.NET 6+) — drift'e dayanıklı periyodik iş
- **Hangfire** — persistence (SQL/Redis), retry, dashboard, job zinciri
  - Fire-and-forget, Delayed, Recurring, Continuations
  - Dashboard — ops ekibinin job yönetimi
  - `[AutomaticRetry(Attempts = 3)]`
  - Hangfire ne zaman: kullanıcı tetiklemeli, retry gerekli, dashboard lazım
- **Quartz.NET** — cron expression, `[DisallowConcurrentExecution]`, clustering
  - Quartz ne zaman: kompleks zamanlama, paralel çalışmama garantisi, enterprise
- **Worker Service** — standalone process, Docker container, queue consumer
  - Worker Service ne zaman: uzun süreli loop, mesaj tüketici, library-free
- **Seçim kılavuzu:**

```
Email gönder, rapor üret (kullanıcı tetikler) → Hangfire
Nightly cron, complex schedule                 → Quartz.NET
Message broker consumer, sürekli loop         → Worker Service / BackgroundService
Kritik iş akışı, orchestration               → Temporal (gelecek)
```

---

### Gün 26 — Caching Stratejileri

**Teorik:**
- In-memory cache — `IMemoryCache`, eviction policies
- Distributed cache — `IDistributedCache`, Redis
- Output cache — ASP.NET Core 7+ built-in
- Cache-aside pattern
- Write-through vs Write-behind
- Cache invalidation — "2 hard problems" biri
- Stampede problem (cache thundering herd) ve çözümleri

---

### Gün 27 — Loglama, Tracing ve Observability

**Teorik:**
- Structured logging — Serilog vs Microsoft.Extensions.Logging
- Log levels — Trace, Debug, Information, Warning, Error, Critical
- Correlation ID — distributed trace başlangıcı
- OpenTelemetry — traces, metrics, logs trinity
- Trace vs Span kavramları
- `Activity` API — .NET'in native tracing sistemi
- Jaeger / Zipkin / Grafana Tempo entegrasyon mantığı

---

### Gün 28 — IHttpClientFactory ve Typed HttpClient

**Teorik:**
- `HttpClient` doğrudan `new` ile neden oluşturulmamalı? (socket exhaustion, DNS stale)
- `IHttpClientFactory` — pooled HttpMessageHandler, DNS refresh
- Named client vs Typed client vs Basic client — ne zaman hangisi?
- Typed client: service class'ına `HttpClient` inject et
- `DelegatingHandler` — request pipeline (logging, auth header ekleme, retry)
- `AddHttpClient<TClient>().AddPolicyHandler(Polly...)` — Polly entegrasyonu
- `BaseAddress`, default headers, timeout konfigürasyonu
- Resilience: .NET 8+ `AddStandardResilienceHandler()`

**Java ile karşılaştırma:**
- Spring `RestTemplate` / `WebClient` → .NET `HttpClient` + `IHttpClientFactory`

---

---

## Hafta 5 — Entity Framework Core: Doğru Kullanım

### Gün 29 — EF Core Mimarisi: DbContext ve Change Tracker

**Teorik:**
- DbContext — Unit of Work + Repository implementasyonu
- Change Tracker — object state management
- Entity States: Detached, Unchanged, Modified, Added, Deleted
- `SaveChanges()` — transaction boundary
- DbContext lifetime — neden Scoped olmalı?
- `AsNoTracking()` — read-only query için performans kazancı

---

### Gün 30 — IQueryable ve LINQ-to-SQL

**Teorik:**
- `IQueryable<T>` — expression tree → SQL translation
- `IEnumerable<T>` vs `IQueryable<T>` — client vs server evaluation
- Client-side evaluation tuzağı — tüm veriyi çekme
- `Include()` ve eager loading — N+1 problemi
- `ThenInclude()` — nested navigation
- Split queries — `.AsSplitQuery()`
- Projection — `Select(x => new DTO {...})` SQL'i küçültür

---

### Gün 31 — N+1 Problemi ve Çözümleri

**Teorik:**
- N+1 query problemi nedir? (Java Hibernate'deki gibi)
- Eager loading (`Include`) vs Lazy loading vs Explicit loading
- Lazy loading neden üretimde tehlikelidir?
- `AsSplitQuery()` ne zaman yardımcı olur, ne zaman olmaz?
- Compiled queries — `EF.CompileQuery()` ile tekrar kullanılan sorgular
- Raw SQL — `FromSqlRaw()` ve `ExecuteSqlRaw()`

---

### Gün 32 — Migration Stratejileri ve Schema Management

**Teorik:**
- Code-first migration sistemi
- Migration idempotency — tekrar uygulanabilirlik
- Production'da migration — `Database.Migrate()` riski
- Migration bundle — executable migration
- Flyway/Liquibase ile karşılaştırma
- Seed data — `HasData()` vs özel seed servisi

---

### Gün 33 — EF Core Performance Tuning

**Teorik:**
- `AsNoTracking()` ne kadar kazandırır?
- Batch operations — `ExecuteUpdate()`, `ExecuteDelete()` (EF Core 7+)
- Bulk operations için `EFCore.BulkExtensions`
- Connection pooling — Npgsql/SQL Server pool konfigürasyonu
- Query tagging — `TagWith()` ile SQL log'unda etiketleme
- Indexes — `HasIndex()`, filtered index, composite index
- Database concurrency — optimistic concurrency token

---

### Gün 34 — Repository Pattern & Unit of Work

**Teorik:**
- Repository Pattern — veri erişimini soyutla, controller'ı ORM'den koru
- `IRepository<T>` — generic CRUD arayüzü
- Entity'ye özel repository — `IKitapRepository : IRepository<Kitap>`
- Unit of Work — birden fazla repository tek DbContext üzerinden; `SaveAsync()` tek transaction
- DI kaydı: Scoped (Transient veya Singleton değil — neden?)
- Test değeri: `FakeRepository` ile DB'siz unit test

---

### Gün 35 — CQRS ve MediatR

**Teorik:**
- CQRS — okuma (Query) ve yazma (Command) modellerini ayır
- `IRequest<T>` — mesaj tanımı; `IRequestHandler<,>` — mesajı işleyen sınıf
- Query → `AsNoTracking`, DTO döndür; Command → validation, `SaveChanges`, Id döndür
- `_mediator.Send()` — handler'ı bul ve çalıştır (controller, handler'ı bilmez)
- `IPipelineBehavior` — cross-cutting concerns (logging, validation tüm handler'larda otomatik)
- Feature klasör yapısı — `Features/Kitaplar/KitapListeQuery.cs` vb.

---

### Gün 36 — Serilog ile Yapılandırılmış Loglama

**Teorik:**
- Structured logging — `{PropertyName}` → JSON'da filtrelenebilir alan
- Sink: Console (geliştirme), File (prod), Seq/Elasticsearch (ileri)
- `MinimumLevel.Override` — Microsoft/EF Core namespace'ini sustur
- `appsettings.json` + `ReadFrom.Configuration` — binary değiştirmeden log seviyesi ayarla
- `UseSerilogRequestLogging` — her HTTP isteği otomatik loglanır
- `{@Nesne}` vs `{Nesne}` farkı — serialize et vs ToString()

---

### Gün 37 — Docker ile Containerization

**Teorik:**
- Image vs Container — şablon vs çalışan örnek
- Multi-stage Dockerfile: build (SDK ~800MB) → runtime (aspnet ~200MB)
- Layer cache — `.csproj` önce kopyala + restore → her küçük değişiklikte restore yok
- Non-root user — güvenlik: `adduser appuser`
- `docker-compose` — SQL Server + web: `depends_on + healthcheck` ile sıra garantisi
- Named volume — container silinse de veri kalır
- Bağlantı dizesi environment variable ile — `ConnectionStrings__Default`

---

## Hafta 6 — Test Yazımı: Unit, Integration, Architecture

> Mid+ seviyenin en net göstergesi test yazabilmektir.
> Java'da JUnit/Mockito bilgisi burada doğrudan transfer olur.

### Bu Haftada Kodlayacaklarımız
Faz2'deki KitabeviMVC uygulamasına test projesi eklenir. Gerçek uygulama kodu üzerinde çalışılır.

---

### Gün 38 — Test Temelleri: xUnit v3 ve AAA Pattern

**Teorik:**
- Unit Test nedir? Neyi test eder, neyi test etmez?
- Test pyramid — Unit → Integration → E2E oranı neden önemli?
- AAA Pattern: Arrange → Act → Assert
- xUnit v3 — .NET'in standart test framework'ü (JUnit eşdeğeri)
- `[Fact]` vs `[Theory]` + `[InlineData]` — parametrize test
- Test class isolation — her test bağımsız çalışmalı
- `ITestOutputHelper` — test log'u

**Java ile karşılaştırma:**
- JUnit 5 `@Test` → xUnit `[Fact]`
- JUnit `@ParameterizedTest` → xUnit `[Theory]`
- xUnit'te `[SetUp]` yok — constructor kullan, `IDisposable` ile teardown

---

### Gün 39 — Test Doubles: Mock, Stub, Fake, Spy

**Teorik:**
- Test Double türleri — Gerard Meszaros terminolojisi
  - **Dummy** — parametre doldurmak için, hiç kullanılmaz
  - **Stub** — önceden belirlenmiş yanıt döner
  - **Mock** — çağrı doğrulaması yapılır (`Verify`)
  - **Fake** — gerçek implementasyon ama basitleştirilmiş (in-memory DB)
  - **Spy** — gerçek nesneyi sarmalar, çağrıları kaydeder
- Moq — .NET'in standart mocking kütüphanesi
- `Mock<T>`, `Setup()`, `Returns()`, `Verify()`
- `It.IsAny<T>()`, `It.Is<T>(predicate)`
- Mock vs Stub ne zaman hangisi?

**Java ile karşılaştırma:**
- Mockito `when().thenReturn()` → Moq `Setup().Returns()`
- Mockito `verify()` → Moq `Verify()`

---

### Gün 40 — FluentAssertions ve Test Okunabilirliği

**Teorik:**
- FluentAssertions — assertion kütüphanesi, okunabilir hata mesajları
- `result.Should().Be(expected)`
- `list.Should().HaveCount(3).And.Contain(x => x.Id == 1)`
- `action.Should().Throw<DomainException>().WithMessage("...")`
- `result.Should().BeEquivalentTo(expected)` — deep equality
- Test isimlendirme — `MethodName_Scenario_ExpectedBehavior`
- Test coverage: ne kadar yeterli? (100% hedef değil)

---

### Gün 41 — Integration Testing: WebApplicationFactory

**Teorik:**
- Unit test vs Integration test farkı — gerçek HTTP pipeline testi
- `WebApplicationFactory<TProgram>` — in-memory test server
- `HttpClient` ile endpoint testi
- Test için DI servislerini override etme
- In-memory database — `UseInMemoryDatabase` (integration test için yeterli mi?)
- `appsettings.Testing.json` — test konfigürasyonu
- Test verisi yönetimi — seed data

---

### Gün 42 — TestContainers: Gerçek DB ile Test

**Teorik:**
- TestContainers — Docker container'ı test içinde ayağa kaldırır
- `Testcontainers.PostgreSql`, `Testcontainers.MsSql`
- Gerçek DB ile integration test — in-memory'nin eksiklerini kapatır
  (in-memory: SQL fonksiyonları yok, transaction davranışı farklı)
- `IAsyncLifetime` — container lifecycle yönetimi
- `CollectionFixture` — aynı container'ı birden fazla test class'ı paylaşır
- TestContainers ne zaman, in-memory ne zaman?

---

### Gün 43 — Architecture Testing: NetArchTest

**Teorik:**
- Architecture Test nedir? — Katman kurallarını otomatik doğrula
- NetArchTest kütüphanesi
- "Domain katmanı Infrastructure'a bağımlı olamaz" kuralını test yaz
- Naming convention testleri — `Controller` ile biten class'lar Controllers namespace'inde olmalı
- Dependency direction testleri
- CI pipeline'da architecture test çalıştırma

**Örnek kural:**
```
Types.InAssembly(domainAssembly)
  .ShouldNot()
  .HaveDependencyOn("Infrastructure")
  .GetResult().IsSuccessful
```

---

### Gün 44 — TDD ve Test Stratejisi

**Teorik:**
- TDD (Test-Driven Development) — Red → Green → Refactor döngüsü
- TDD her yerde uygulanır mı? Tartışma
- Test stratejisi seçimi:
  - Domain logic → Unit test (Moq ile izole)
  - Application layer (command/query) → Unit test (repository mock)
  - Controller/API endpoint → Integration test (WebApplicationFactory)
  - DB sorguları → TestContainers ile integration test
- Brittle test antipattern — implementasyon detayına bağımlı test
- Test piramidi vs test elması (Google'ın yaklaşımı)

---

### Gün 45 — Hafta 6 Özet ve Faz 2 Sonu

**Tekrar soruları:**
1. Stub ile Mock arasındaki fark nedir? Ne zaman hangisi kullanılır?
2. `WebApplicationFactory` nasıl çalışır? In-process mi çalışır?
3. In-memory database ile TestContainers arasında neden TestContainers daha güvenilir?
4. Architecture test neden CI'da çalıştırılmalı?
5. Repository Pattern olmadan CQRS handler'ı unit test edebilir misin?

---

# FAZ 3 — MİMARİ VE DESIGN PATTERNS

> Bu faz müfredatın kalbi. Kod az, düşünce çok.

### Bu Fazda Kodlayacaklarımız (`Faz3-Mimari/`)

| Klasör | Ne yapılır |
|--------|-----------|
| `01-SOLID/` | Her prensip için ihlal → düzeltme karşılaştırması |
| `02-DesignPatterns/` | Strategy, Decorator, Observer, Command, Mediator örnekleri |
| `03-OnionArchitecture/` | Faz2 KitabeviMVC → Onion katmanlarına refactor |
| `04-CQRS/` | MediatR + Command/Query + Pipeline Behavior (logging, validation) |
| `05-Tests/` | Onion uygulamasına Unit + Integration + Architecture testleri |

> Faz3'ün sonunda elimizde Onion + CQRS + Test Coverage olan tam bir Kitabevi API'si olacak.

---

## Hafta 7 — SOLID Prensipleri: Gerçek Uygulama

### Gün 46 — Single Responsibility Principle (SRP)

**Teorik:**
- SRP'nin gerçek tanımı: "A class should have only one reason to change"
- Robert Martin'in orijinal tanımı: actor/stakeholder tabanlı
- "One class, one job" yanlış basitleştirmedir — neden?
- God class antipattern — nasıl tespit edilir?
- SRP ihlali örnekleri: `UserService` hem auth hem email hem DB
- Ayırma stratejileri: domain service, application service, infrastructure service

---

### Gün 47 — Open/Closed Principle (OCP)

**Teorik:**
- "Open for extension, closed for modification"
- Strategy pattern ile OCP
- Switch/if-else zinciri OCP ihlali — neden?
- Plugin mimarisi — yeni davranış = yeni class, eski kod dokunulmaz
- `IEnumerable<IValidator<T>>` pattern — yeni validator = yeni class

---

### Gün 48 — Liskov Substitution ve Interface Segregation

**Teorik (LSP):**
- "Subtype must be substitutable for base type"
- Rectangle/Square örneği — neden inheritance hatalı?
- Precondition güçlendirme ve postcondition zayıflatma yasağı
- LSP ihlali nasıl bulunur?

**Teorik (ISP):**
- "Many specific interfaces are better than one general-purpose interface"
- Fat interface antipattern
- Role interface pattern
- `IReadableRepository<T>` + `IWritableRepository<T>` ayrımı

---

### Gün 49 — Dependency Inversion Principle (DIP)

**Teorik:**
- "High-level modules should not depend on low-level modules"
- Dependency Inversion ≠ Dependency Injection (DI, DIP'i uygulamanın bir yolu)
- Abstraction nerede yaşamalı? Yüksek seviyeli modülde!
- Infrastructure kodun domain'e bağımlı olması, tersinin değil
- Hexagonal Architecture bu prensipten türer

---

### Gün 50 — SOLID Sentez: Mimari Etkileri

**Teorik:**
- SOLID ihlalleri teknik borca nasıl dönüşür?
- Test edilebilirlik SOLID'in ödülü
- SOLID ve design patterns ilişkisi
- "SOLID overengineering" — ne zaman prensipleri ihlal etmek makul?

---

### Gün 51 — GoF Design Patterns: Creational

**Teorik:**
- Factory Method — nesne oluşturma kararını alt sınıfa bırak
- Abstract Factory — birbirine bağlı nesne ailesi
- Builder — karmaşık nesne inşası (Fluent Builder yaygın)
- Singleton — .NET DI ile nasıl gereksizleşir?
- Prototype — `ICloneable` ve deep copy
- **Revealing Constructor** (Node.js kökenli, .NET karşılığı) — nesneyi sadece constructor sırasında değiştirilebilir yap, sonra immutable sun. C#'ta `record` + `init` + internal ctor ile aynı fikir. `ImmutableBuilder` pattern.
- **Async Initialization Pattern** — constructor async olamaz; çözümler:
  - Static async factory: `public static async Task<Foo> CreateAsync()`
  - `IAsyncDisposable` + `InitializeAsync()` (IHostedService)
  - `Lazy<Task<T>>` — lazy async singleton
  - .NET DI ile: `IHostedService.StartAsync()` + servis hazırlanana kadar readiness probe fail

**.NET örnekleri:**
- `IHttpClientFactory` → Factory Method
- `StringBuilder` → Builder
- ASP.NET Core'da Singleton lifetime → DI Singleton
- `DbContext` ile async initialization → `EnsureCreatedAsync()` startup'ta

**Sorular:**
1. Constructor neden async olamaz? Async factory ile nasıl çözersin?
2. `Lazy<Task<T>>` kullanımında thread safety nasıl sağlanır?
3. Revealing Constructor neden immutability ile ilişkili? .NET'te `record init` ile nasıl bağdaşır?

---

### Gün 52 — Hafta 7 Özet

**Mimari soru:**
- Bir ödeme sistemi tasarlıyorsun. Kredi kartı, PayPal, crypto desteklenecek. SOLID'i nasıl uygularsın?
- `UserService` içinde 500 satır kod var. SRP'ye göre nasıl bölersin?

---

## Hafta 8 — GoF Design Patterns: Structural & Behavioral

### Gün 53 — Structural Patterns

**Teorik:**
- **Decorator** — davranış ekle, sınıfı değiştirme. ASP.NET Middleware bunun örneği
- **Adapter** — uyumsuz interface'leri uyumlu hale getir
- **Facade** — karmaşık sistemi basit interface arkasına gizle
- **Proxy** — lazy loading, caching, access control (EF Core lazy loading proxy kullanır)
- **Composite** — ağaç yapısı, leaf ve composite aynı interface
- **Flyweight** — paylaşılan state ile bellek optimizasyonu (string interning benzer)

**.NET örnekleri:**
- `ILogger` decorator chain — Serilog enrichers
- `HttpClient` DelegatingHandler → Decorator
- EF Core lazy loading proxy → Proxy pattern

---

### Gün 54 — Behavioral Patterns 1

**Teorik:**
- **Strategy** — algoritma ailesi, runtime seçimi
- **Observer** — event-driven, C# event keyword bunun implementasyonu
- **Command** — isteği nesne olarak temsil et, undo/redo, queue
- **Chain of Responsibility** — ASP.NET Middleware pipeline bunun örneği
- **Template Method** — algoritma iskeleti, adımları override

**.NET örnekleri:**
- MediatR `IRequestHandler<T>` → Command pattern
- Middleware pipeline → Chain of Responsibility
- `IValidator<T>` → Strategy pattern

---

### Gün 55 — Behavioral Patterns 2

**Teorik:**
- **Mediator** — nesneler arası iletişimi merkezi nesne üzerinden
- **State** — durum değişimini nesne değişimine dönüştür
- **Iterator** — `IEnumerable<T>` / `yield return` bunun uygulaması
- **Visitor** — operasyonu veri yapısından ayır
- **Memento** — snapshot, undo

**.NET örnekleri:**
- MediatR → Mediator pattern
- `IAsyncEnumerable<T>` → Iterator pattern

---

### Gün 56 — Anti-Patterns: Kaçınılması Gerekenler

**Teorik:**
- Anemic Domain Model — sadece getter/setter, logic yok
- God Class / God Object — her şeyi bilen tek nesne
- Service Locator — hidden dependency, test edilemezlik
- Shotgun Surgery — bir değişiklik birçok yerde
- Feature Envy — başka sınıfın verisini çok kullanan method
- Primitive Obsession — domain kavramı yerine int/string
- Magic Numbers/Strings
- Spaghetti Code vs Big Ball of Mud

---

### Gün 57 — Domain-Driven Design: Taktiksel Kavramlar

**Teorik:**
- Entity vs Value Object farkı
- Aggregate ve Aggregate Root — transaction boundary
- Repository — aggregate erişim kapısı
- Domain Service — birden fazla aggregate'i ilgilendiren logic
- Domain Event — aggregate içinde ne olduğu
- Ubiquitous Language — domain uzmanı ile aynı dil

**Kritik:**
```
Aggregate Root → tek giriş noktası
Aggregate içi tutarlılık → her zaman korunur (invariants)
Aggregate'ler arası tutarlılık → eventual consistency
```

---

### Gün 58 — Hafta 8 Özet

**Mimari soru:**
- Sipariş durumu (Pending, Confirmed, Shipped, Delivered, Cancelled) State pattern ile nasıl modellenir?
- MediatR neden Service Locator antipattern'i değildir?
- Anemic Domain Model ile Rich Domain Model arasında ne zaman hangisi tercih edilir?

---

## Hafta 9 — Onion / Clean Architecture

### Gün 59 — Layered Architecture'dan Onion'a Geçiş

**Teorik:**
- Traditional N-Layer: Presentation → Business → Data — sorun nedir?
- Bağımlılık yönü: UI → Business → DB. Değişim: DB değişirse business değişir
- Onion Architecture — Jeffrey Palermo
- Clean Architecture — Robert Martin (Uncle Bob)
- Hexagonal Architecture (Ports & Adapter) — Alistair Cockburn
- Üçü aynı fikrin farklı ifadesi: Domain merkez, dış dünya detay

---

### Gün 60 — Onion Katmanları: Detaylı Analiz

**Teorik:**

**Katman 1: Domain (en içte)**
- Entity, Value Object, Aggregate, Domain Event
- Domain Service
- Repository Interface (implementation değil!)
- Hiçbir dış bağımlılık yok. Sadece C# standart kütüphanesi

**Katman 2: Application**
- Use Case / Command / Query (CQRS)
- Application Service
- DTO'lar
- Bağımlılık: Sadece Domain katmanına
- Infrastructure interface'leri burada tanımlanır (IEmailService, IFileStorage)

**Katman 3: Infrastructure**
- EF Core implementasyonu (Repository)
- Email servisi implementasyonu
- File storage implementasyonu
- External API client'ları
- Bağımlılık: Application ve Domain'e

**Katman 4: Presentation (en dışta)**
- Controller / Minimal API
- Request/Response modelleri
- DI composition root

---

### Gün 61 — Bağımlılık Kuralı: Dependency Rule

**Teorik:**
- "Bağımlılık daima içe doğru akar"
- Infrastructure → Application → Domain (tersine asla)
- Domain'i test etmek için Infrastructure gerekmez
- Interface Segregation ile domain-infrastructure ayrımı
- `IRepository<T>` neden domain'de tanımlanır?

**Pratik mimari kararlar:**
- DTO nerede yaşamalı? (Application katmanı)
- Mapper nerede çalışmalı? (Application katmanı)
- Validation nerede yapılmalı? (Application + Domain)

---

### Gün 62 — CQRS: Command Query Responsibility Segregation

**Teorik:**
- CQRS nedir? Okuma ve yazma modellerini ayırma
- Greg Young'ın CQRS tanımı vs Martin Fowler'ın yaklaşımı
- Simple CQRS — tek DB, farklı model
- Full CQRS — farklı DB (write: SQL, read: MongoDB/Redis)
- MediatR ile CQRS implementasyonu
- Command → side effect yaratır, değer döndürmez (veya ID döndürür)
- Query → side effect yok, değer döndürür

**MediatR pipeline:**
```
Request → Pipeline Behavior → Handler → Response
Logging Behavior → Validation Behavior → Handler
```

---

### Gün 63 — MediatR: Pipeline Behaviors

**Teorik:**
- `IPipelineBehavior<TRequest, TResponse>` — AOP benzeri
- Logging Behavior
- Validation Behavior — FluentValidation entegrasyonu
- Caching Behavior — query sonucu cache'le
- Transaction Behavior — command'i transaction'a sar
- Exception Handling Behavior — domain exception → HTTP result

**Dikkat:** MediatR her şeye cevap değil. Handler başka handler'ı çağırmamalı.

---

### Gün 64 — Result Pattern ve Hata Yönetimi

**Teorik:**
- Exception vs Result pattern
- Domain exception'lar ne zaman throw edilmeli?
- `Result<T>` — başarı ve başarısızlığı explicit temsil et
- `OneOf<T1, T2>` — discriminated union
- `FluentResults` paketi
- Railway Oriented Programming kavramı
- ASP.NET Core'da Result → HTTP response mapping

---

### Gün 65 — Hafta 9 Özet

**Mimari soru:**
- E-ticaret uygulamasında sipariş oluşturma use case'ini Onion Architecture'da nasıl tasarlarsın?
- CQRS olmadan ne kaybedersin?
- MediatR Pipeline Behavior'ı Decorator pattern ile karşılaştır.

---

## Hafta 10 — İleri Mimari Kavramlar

### Gün 66 — Event-Driven Architecture Temelleri

**Teorik:**
- Domain Event vs Integration Event farkı
- Domain Event — aynı bounded context içinde (senkron veya async)
- Integration Event — bounded context'ler arası (her zaman async)
- Outbox Pattern — "at-least-once delivery" garantisi
- Event sourcing nedir? CRUD'un alternatifi mi?
- Eventual Consistency — ne zaman kabul edilebilir?

---

### Gün 67 — Specification Pattern

**Teorik:**
- Business rule'u nesne olarak temsil et
- Combine: `And()`, `Or()`, `Not()`
- EF Core ile `IQueryable` üzerine specification
- Ardalis.Specification paketi
- Repository'deki "FindAll(spec)" pattern'i

---

### Gün 68 — Unit of Work Pattern

**Teorik:**
- Unit of Work — transaction yönetimi
- EF Core `DbContext` zaten Unit of Work
- Neden explicit Unit of Work interface yazılır?
- Multiple aggregate, tek transaction — ne zaman mümkün?
- Distributed transaction neden kaçınılır?

---

### Gün 69 — Validation Stratejileri

**Teorik:**
- DataAnnotations vs FluentValidation
- Application layer validation vs Domain layer validation farkı
- Guard clauses — domain invariant'ları koruma
- `Ardalis.GuardClauses`
- Validation sonucu nasıl döndürülür? (Exception vs Result)
- ASP.NET validation pipeline ile MediatR validation davranışı karşılaştırması

---

### Gün 70 — Vertical Slice Architecture

**Teorik:**
- Vertical Slice nedir? Katman bazlı değil, özellik bazlı organizasyon
- Her slice kendi içinde tam: Request → Handler → Validation → DB → Response
- Onion/Clean Architecture ile farkı:
  - Clean Architecture → horizontal katmanlar (Domain, App, Infra, UI)
  - Vertical Slice → vertical kesitler (CreateOrder, GetOrders, CancelOrder)
- `Features/` klasör yapısı: her özellik kendi klasöründe
- MediatR + Vertical Slice — doğal birliktelik
- Paylaşılan kod nereye? — `Shared/` veya `Common/`
- Ne zaman Vertical Slice, ne zaman Onion?
  - Domain karmaşıksa, domain logic paylaşılıyorsa → Onion
  - CRUD ağırlıklı, özellikler bağımsızsa → Vertical Slice
  - İkisi birlikte: Modüler Monolith içinde her modül Vertical Slice

---

### Gün 71 — Modüler Monolith Mimarisi

**Teorik:**
- Modüler Monolith nedir? Tek deployment, modül izolasyonu
- Mikroservis ile farkı: aynı process, network yok, distributed transaction yok
- Modül sınırları — Bounded Context = Modül
- Modüller arası iletişim: in-process public API (interface üzerinden)
- Modüller arası DB izolasyonu: ayrı schema veya ayrı DbContext
- Modülden modüle doğrudan tablo erişimi yasak — neden?
- **Modüler Monolith → Mikroservis yolculuğu:**
  1. Modülleri izole et (DB schema ayrı, interface üzerinden iletişim)
  2. Modüller arası çağrıları event-based yap
  3. Hazır olunca modülü ayrı servise taşı (Strangler Fig)
- .NET Aspire ile modüler monolith geliştirme
- Ne zaman modüler monolith, ne zaman mikroservis?

```
Modüler Monolith tercih et:
- Ekip küçük (< 15 kişi)
- Domain henüz netleşmemiş
- Operasyon karmaşıklığı istemiyorsun
- Mikroservise geçiş planı var ama hazır değilsin
```

---

### Gün 72 — Hafta 10 Özet ve Faz 3 Kapanış

**Büyük mimari soru:**
Bir e-ticaret platformu tasarla (kavramsal):
- Onion Architecture katmanlarını belirle
- CQRS ile hangi use case'ler command, hangisi query?
- Domain event'leri nerede neden kullanırsın?
- Hangi aggregate'ler olur? Sınırları neden orada çizersin?
- Repository interface'leri hangi katmanda?
- Vertical Slice bu domain için uygun olur muydu? Neden?
- Modüler Monolith olarak tasarlasaydın ne değişirdi?

---

# FAZ 4 — PERFORMANS VE İLERİ KONULAR

> Senior geliştirici olmak isteyenler için bu faz kritik.

### Bu Fazda Kodlayacaklarımız (`Faz4-Performans/`)

| Klasör | Ne yapılır |
|--------|-----------|
| `01-Benchmarks/` | BenchmarkDotNet: string concat, LINQ, Span<T> karşılaştırmaları |
| `02-Memory-Optimization/` | ArrayPool, Span, allocation-free parser demoları |
| `03-EFCore-Tuning/` | N+1 tespiti, compiled query, AsNoTracking, batch update |

> Her benchmark "öncesi" ve "sonrası" karşılaştırması olarak sunulur.

---

## Hafta 11 — .NET Performans: Bellek ve Allocation

### Gün 73 — Allocation Analizi: BenchmarkDotNet ve dotMemory

**Teorik:**
- Allocation neden kötü? GC baskısı
- BenchmarkDotNet ile micro-benchmark
- dotMemory ile memory profiling
- `dotnet-trace` ve `dotnet-counters` CLI araçları
- ETW (Event Tracing for Windows) ve EventPipe

---

### Gün 74 — Span<T>, Memory<T>, ArrayPool<T>

**Teorik:**
- `Span<T>` — stack-allocated, allocation-free slice
- `Memory<T>` — async context'te Span yerine
- `ArrayPool<T>.Shared` — array kiralama, allocation sıfır
- `MemoryPool<T>` — büyük buffer'lar için
- `stackalloc` — stack'te dizi tahsisi
- Neden string split yerine `MemoryExtensions.Split()` kullanmalı?

---

### Gün 75 — Struct, readonly struct, ref struct

**Teorik:**
- Defensive copying nedir? readonly struct neden önler?
- `in` parametresi ile readonly struct — copy yok
- `ref struct` — sadece stack, async context'te kullanamaz
- `Span<T>` neden ref struct? (Heap'e kaçamaması için)
- Struct array vs class array — cache locality etkisi

---

### Gün 76 — String Optimizasyonları

**Teorik:**
- `string.Concat` vs `StringBuilder` vs `string.Create()`
- Interpolated string handler (C# 10+) — allocation-free interpolation
- `SearchValues<T>` — hızlı karakter arama
- `Regex.IsMatch()` vs compiled Regex vs `GeneratedRegex` (source gen)
- `CompositeFormat` — format string caching

---

### Gün 77 — Async Performans: ValueTask ve IAsyncEnumerable

**Teorik:**
- `ValueTask<T>` — synchronous path'te allocation sıfır
- `IAsyncEnumerable<T>` — streaming, tüm koleksiyonu belleğe almadan
- `await foreach` — async stream tüketimi
- `ConfigureAwait(false)` — library kodu için
- Thread-safe collection'lar — `ConcurrentDictionary`, `Channel<T>`

---

### Gün 78 — Memory Leak Tespiti ve Çözümü

**Teorik:**
- .NET'te memory leak senaryoları:
  1. Static koleksiyona ekleme, çıkarma yok
  2. Event handler unsubscribe edilmemiş
  3. `IDisposable` Dispose edilmemiş (DB connection vb.)
  4. Closure uzun yaşayan nesneyi yakalar
  5. Cache sınırsız büyür
- `WeakReference<T>` ve `ConditionalWeakTable<TKey, TValue>`
- dotMemory ile leak bulma adımları
- `GC.GetTotalMemory()` ve `GC.GetGCMemoryInfo()`

---

### Gün 79 — Production Diagnostics: dotnet CLI Araçları

**Teorik:**
- Production'da sorun var ama debugger bağlayamazsın — ne yaparsın?
- **dotnet-counters** — canlı runtime metrik izleme (CPU, GC, thread pool)
  - `dotnet-counters monitor --process-id <PID>`
  - GC baskısı var mı? Thread pool exhausted mi? Hızlı yanıt verir
- **dotnet-trace** — EventPipe ile CPU profil ve event trace
  - `dotnet-trace collect --process-id <PID>`
  - PerfView veya SpeedScope ile analiz
  - CPU'yu ne yiyor? Method-level profil
- **dotnet-gcdump** — GC heap snapshot, hafif (full dump değil)
  - `dotnet-gcdump collect --process-id <PID>`
  - Heap'teki nesne sayısını ve kök referansları gösterir
  - Collecting sırasında Gen2 GC tetikler — production dikkatli kullan
  - Visual Studio veya PerfView ile analiz
- **dotnet-dump** — full memory dump (Windows/Linux)
  - `dotnet-dump collect --process-id <PID>`
  - `dotnet-dump analyze` — SOS komutları
  - `dumpheap -stat` — memory leak tespiti
  - `gcroot <address>` — nesneyi canlı tutan referans zinciri
- **Linux container'da diagnostics:**
  - Kubernetes pod'una araç kurma (init container veya ephemeral container)
  - `kubectl exec -it <pod> -- sh` + `dotnet-dump install`
- Araç seçim kılavuzu:
  ```
  CPU yüksek           → dotnet-trace
  Memory artıyor       → dotnet-gcdump → dotnet-dump
  Thread pool dolu     → dotnet-counters
  Crash / exception    → dotnet-dump (post-mortem)
  ```

---

### Gün 80 — System.IO Streams, Pipelines ve Networking

> Node.js'teki Stream kavramlarının .NET karşılığı — aynı zihinsel model, farklı API.

**Teorik — Stream Temelleri:**
- `System.IO.Stream` hiyerarşisi: `FileStream`, `MemoryStream`, `NetworkStream`, `BufferedStream`, `CryptoStream`, `GZipStream`
- Buffer vs Stream farkı — tüm veriyi bellekte tutmak vs parça parça işlemek
- Sync vs Async okuma — `Read()` vs `ReadAsync()` (Node.js: non-flowing vs flowing mode karşılığı)
- Chunk okuma — `buffer[]` ile `Read(buffer, offset, count)` — her çağrı bir chunk
- Writable Stream karşılığı — `StreamWriter`, `BinaryWriter`, `Stream.WriteAsync()`
- Stream composition (transform stream karşılığı):
  - `GZipStream(fileStream, CompressionMode.Compress)` → dosya yazarken sıkıştır
  - `CryptoStream(networkStream, encryptor, CryptoStreamMode.Write)` → yazarken şifrele
  - Katmanlı sarma: `GZipStream → CryptoStream → FileStream` = compose edilmiş pipeline
- Duplex stream karşılığı — `NetworkStream` (hem okunabilir hem yazılabilir)
- `Stream.CopyToAsync()` — pipe method karşılığı

**Teorik — System.IO.Pipelines (Backpressure & High Performance):**
- `PipeReader` / `PipeWriter` — backpressure'ı built-in olarak yöneten modern IO API
- Backpressure nedir? Consumer yazardan daha yavaşsa ne olur? Pipe otomatik yavaşlatır
- `pipe()` method karşılığı — `PipeReader.CopyToAsync(pipeWriter)`
- `pipeline()` karşılığı — birden fazla `PipeReader/PipeWriter` zinciri
- `ReadResult.IsCompleted` — stream bitti mi kontrolü
- Neden `MemoryStream` yerine `Pipe`? Allocation sıfır, buffer yeniden kullanım
- ASP.NET Core'un içi zaten `System.IO.Pipelines` kullanır (Kestrel)

**Teorik — TCP Socket ve Network:**
- `TcpListener` / `TcpClient` — `net.createServer()` karşılığı
- `Socket` — alt seviye, UDP dahil
- `NetworkStream` — TCP socket'in stream arayüzü
- Async kabul döngüsü: `await listener.AcceptTcpClientAsync()`
- `SocketAsyncEventArgs` — zero-copy, yüksek performans pattern

**Teorik — HTTP Range Requests:**
- `Range` header — `bytes=0-1023` ile kısmi içerik talebi
- `206 Partial Content` + `Content-Range` header
- `PhysicalFileResult` ile ASP.NET Core otomatik range desteği
- Büyük dosya indirme, video streaming, resume-download senaryoları
- `EnableRangeProcessing = true`

**Teorik — WebStreams (.NET 6+):**
- `System.IO.Pipelines` ile ASP.NET Core response streaming
- `HttpResponse.Body` → `PipeWriter`
- Fetch API Streams karşılığı: `ReadableStream` → `IAsyncEnumerable<T>` + `await foreach`
- SSE (Server-Sent Events) — `text/event-stream` ile push streaming
- `HttpClient` ile streaming response: `GetStreamAsync()` → allocation-free okuma

**Pratik:**
1. `FileStream + GZipStream + CryptoStream` zinciri — dosyayı okurken sıkıştır ve şifrele
2. `System.IO.Pipelines` ile büyük CSV oku — `PipeReader` + `SequenceReader<byte>`
3. `TcpListener` ile basit echo server — async kabul döngüsü
4. ASP.NET Core'da range request destekli dosya endpoint'i
5. `IAsyncEnumerable<T>` ile SSE endpoint — Kitabevi stok güncellemeleri push

**Sorular:**
1. `Stream.CopyToAsync()` ile `System.IO.Pipelines` arasındaki fark nedir?
2. Backpressure olmadan producer-consumer'da ne olur? Pipelines bunu nasıl çözer?
3. `NetworkStream` duplex mi? Aynı anda hem oku hem yaz güvenli mi?
4. Video streaming endpoint'inde neden `FileStreamResult` yerine range request gerekir?
5. `GZipStream(fileStream)` ile `fileStream` doğrudan okuma arasındaki allocation farkı nedir?

---

### Gün 81 — Hafta 11 Özet

**Pratik görev:**
- Bir CSV dosyasını satır satır oku, `Span<char>` ile parse et, nesne oluştur
- `ArrayPool<T>` kullanarak buffer allocation'ı minimize et
- BenchmarkDotNet ile `string.Split()` vs `MemoryExtensions.Split()` karşılaştır

---

## Hafta 12 — Parallelism, Concurrency ve Thread Safety

### Gün 82 — Thread Safety: Lock, Monitor, Interlocked

**Teorik:**
- Race condition — ne zaman olur?
- `lock` → `Monitor.Enter/Exit` sugar
- `Interlocked` — atomic operations, lock-free
- `ReaderWriterLockSlim` — okuma çok, yazma az
- `volatile` keyword — memory visibility, C#'ta sınırlı kullanım
- Memory model — happens-before, acquire-release semantics

---

### Gün 83 — Task Parallel Library (TPL) Derinlemesine

**Teorik:**
- `Parallel.For`, `Parallel.ForEach` — data parallelism
- `PLINQ` — `AsParallel()` ne zaman kullanılır?
- `Task.WhenAll` vs `Task.WhenAny`
- `SemaphoreSlim` — async-friendly limiting
- `TaskCompletionSource<T>` — event-to-task dönüşümü
- `CancellationTokenSource.CreateLinkedTokenSource()`

---

### Gün 84 — Channel<T> ve Producer-Consumer

**Teorik:**
- `Channel<T>` — modern async queue
- Bounded vs Unbounded channel
- `ChannelWriter<T>` ve `ChannelReader<T>`
- Back-pressure — bounded channel ile
- `IAsyncEnumerable<T>` ile channel tüketimi
- Dataflow — `TransformBlock`, `ActionBlock`

---

### Gün 85 — Thread Pool ve Task Scheduling

**Teorik:**
- ThreadPool neden `new Thread()` yerine tercih edilir?
- ThreadPool starvation — async over sync anti-pattern
- `Task.Run()` vs `Task.Factory.StartNew()`
- Custom `TaskScheduler` — ne zaman gerekli?
- Synchronization context — UI thread, ASP.NET Classic farkı
- ASP.NET Core'da SynchronizationContext yok — neden?

---

### Gün 86 — Immutability ve Functional Patterns

**Teorik:**
- Immutable data neden thread-safe?
- `ImmutableList<T>`, `ImmutableDictionary<T,V>` — System.Collections.Immutable
- Record types + `with` expression — structural copying
- Functional core, imperative shell pattern
- Pure function — test edilebilirlik ve parallelism kolaylığı

---

### Gün 87 — Hafta 12 Özet

**Senaryo sorusu:**
- 10.000 URL'yi paralel fetch edeceksin. ThreadPool starvation olmadan nasıl yaparsın?
- `SemaphoreSlim` ile parallelism'i nasıl sınırlarsın?
- Neden `Task.Run()` her yerde kullanmak yanlış?

---

## Hafta 13 — ASP.NET Core Performans Tuning

### Gün 88 — Response Compression, Caching, CDN

**Teorik:**
- Response compression — Brotli vs Gzip
- HTTP caching headers — `Cache-Control`, `ETag`, `Last-Modified`
- Output caching (ASP.NET Core 7+) — vary by route, query, header
- CDN — edge caching, ne cache edilmeli?
- `ResponseCache` attribute vs Output Cache middleware

---

### Gün 89 — Rate Limiting

**Teorik:**
- Rate limiting neden gerekli?
- ASP.NET Core 7+ built-in rate limiting
- Fixed window vs Sliding window vs Token bucket vs Concurrency limiter
- `RateLimiter` policy yapılandırması
- Rate limit exceeded → 429 Too Many Requests

---

### Gün 90 — Minimal API Performance

**Teorik:**
- Minimal API neden controller'dan daha hızlı?
- `TypedResults` — reflection yerine compile-time
- Endpoint filter — minimal API için action filter
- `IEndpointFilter` pipeline
- Short-circuit ve early return

---

### Gün 91 — SQL ve Index Stratejisi: .NET Geliştiricisi Perspektifinden

**Teorik:**
- Index türleri: Clustered vs Non-Clustered index
- Covering index — `INCLUDE` ile extra kolon, index-only scan
- Filtered index — partial index, belirli satırlar için
- Composite index — kolon sırası neden önemli? (SARGability)
- Query plan okuma — `EXPLAIN ANALYZE` (PostgreSQL) / Execution Plan (SQL Server)
- Implicit conversion — index'i kör eden tuzak
- Pagination: `OFFSET/FETCH` (Skip/Take) vs Keyset (cursor-based) — büyük tablolarda fark
- `EF.CompileQuery()` — tekrar eden sorgu için compiled query
- Connection pooling — `Min Pool Size`, `Max Pool Size`
- Batch insert/update — `ExecuteUpdate` / `BulkExtensions`

---

### Gün 92 — Redis: Distributed Cache Derinlemesine

**Teorik:**
- Redis veri yapıları: String, List, Set, Sorted Set, Hash, Stream
- `IDistributedCache` — ASP.NET Core soyutlaması
- `StackExchange.Redis` — doğrudan Redis client
- Cache-aside pattern implementasyonu
- TTL (Time-To-Live) ve eviction policy — LRU, LFU
- Redis Pub/Sub — basit event bus olarak kullanım
- Redis'te atomic operasyonlar — `SETNX`, `GETSET`, Lua script
- Distributed lock — `RedLock` pattern (birden fazla Redis node)
- `HybridCache` (.NET 9) — L1 (in-memory) + L2 (Redis) otomatik yönetimi
- Redis Cluster vs Sentinel — high availability farkı

**Ne zaman Redis, ne zaman in-memory cache?**
```
In-memory  → tek instance, restart'ta kayıp kabul edilebilir
Redis      → multi-instance, restart'ta kalıcılık, session paylaşımı
```

---

### Gün 93 — Health Checks ve Readiness/Liveness

**Teorik:**
- Kubernetes liveness vs readiness probe
- `IHealthCheck` interface
- `AddDbContextCheck`, custom health check
- Health check UI
- Circuit breaker ile health check entegrasyonu

---

### Gün 94 — Hafta 13 Özet

**Performans checklist:**
- [ ] `AsNoTracking()` kullanıldı mı?
- [ ] N+1 var mı? (`Include` veya split query?)
- [ ] String concat loop'ta mı?
- [ ] `IDisposable` her yerde dispose ediliyor mu?
- [ ] Büyük koleksiyon memory'de mi tutulmuş?
- [ ] Synchronous I/O var mı? (Thread pool starvation riski)

---

## Hafta 15 — Niş Production Patterns: EF Core İleri

> Bu haftada öğreneceğin konular "kod çalışıyor" ile "production'a hazır" arasındaki farkı oluşturur.
> Her biri gerçek projelerde kaçınılmaz karşılaşacağın senaryolardır.

---

### Gün 95 — Soft Delete ve Global Query Filters

**Teorik:**
- Soft delete nedir? `IsDeleted` flag ile fiziksel silme yerine mantıksal silme
- `IsDeleted = true` olan kayıtları her sorguda elle filtrelemek neden tehlikeli?
- EF Core Global Query Filter — `HasQueryFilter(x => !x.IsDeleted)` — otomatik uygulama
- `IgnoreQueryFilters()` — admin ekranı, silinen kaydı göster
- Soft delete + cascade: ilişkili kayıtlar da soft-delete olmalı mı?
- `SaveChangesInterceptor` — `DeleteBehavior` override, `IsDeleted = true` set et
- Soft delete ve unique index çakışması — `[Index(IsUnique=true, Filter="IsDeleted=0")]`
- Fiziksel temizlik (purge) stratejisi — periyodik hard delete

**EF Core 10 yeniliği:**
- Named query filters — birden fazla filter tanımlama, isimle toggle

---

### Gün 96 — Audit Trail: Kim, Ne Zaman, Ne Yaptı?

**Teorik:**
- Audit trail neden gerekli? GDPR, finansal sistemler, debug
- `IAuditableEntity` interface: `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`
- `SaveChangesInterceptor` ile otomatik audit field doldurma
- `ICurrentUserService` — kim yapmış bilgisini DI ile inject etme
- Shadow properties — entity'de alan olmadan EF Core'da audit bilgisi sakla
- Değişiklik loglama — `ChangeTracker.Entries()` ile property öncesi/sonrası değer
- Audit log tablosu tasarımı — JSON olarak `OldValues` / `NewValues`
- Event Sourcing ile Audit Trail farkı — hangisi ne zaman?

---

### Gün 97 — Optimistic Concurrency ve Conflict Resolution

**Teorik:**
- Optimistic concurrency nedir? "Son yazan kazanır" sorununu çözer
- `[Timestamp]` / `RowVersion` — EF Core otomatik concurrency token
- `[ConcurrencyCheck]` — belirli kolon(lar) üzerinde concurrency
- `DbUpdateConcurrencyException` — conflict yakalandığında ne yapılır?
- Conflict resolution stratejileri:
  - Database wins — gelen değişikliği reddet
  - Client wins — DB'yi override et
  - Merge — kolonları birleştir, özel logic
- Pessimistic locking — `UPDLOCK` / `SELECT FOR UPDATE` — ne zaman?
- Distributed lock ile uygulama seviyesi serialization (Redis RedLock)

---

### Gün 98 — Multi-Tenancy Mimarisi

**Teorik:**
- Multi-tenancy nedir? Tek uygulama, N farklı müşteri (tenant)
- İzolasyon stratejileri karşılaştırması:
  - **Database-per-Tenant** — tam izolasyon, yüksek maliyet
  - **Schema-per-Tenant** — orta izolasyon, PostgreSQL schema
  - **Row-Level (Shared DB)** — düşük maliyet, global query filter ile izolasyon
- Tenant resolving — subdomain, header, claim, route'dan tenant ID al
- `ITenantService` → `ICurrentTenantService` — DI ile tenant context
- EF Core global query filter ile tenant isolation: `HasQueryFilter(x => x.TenantId == _tenantService.TenantId)`
- PostgreSQL Row Level Security — DB seviyesinde izolasyon
- Finbuckle.MultiTenant kütüphanesi
- Cross-tenant data erişimi — admin tenant konsepti
- Migration: her tenant için ayrı migration nasıl yönetilir?

---

### Gün 99 — EF Core Interceptors ve SaveChanges Pipeline

**Teorik:**
- EF Core Interceptor türleri:
  - `ISaveChangesInterceptor` — save öncesi/sonrası
  - `IDbCommandInterceptor` — SQL komut öncesi/sonrası (loglama, modifiye)
  - `IDbConnectionInterceptor` — connection open/close
  - `IMaterializationInterceptor` — entity oluşturulurken
- `SavingChangesAsync` → audit trail, soft delete dönüşümü
- `SavedChangesAsync` → domain event dispatch
- Shadow properties — entity class'ına koymak istemediğin audit field'lar
- Owned entities vs Value Objects — EF Core'da Value Object mapping
- Complex types (EF Core 8+) — owned entity'nin hafif versiyonu

---

## Hafta 16 — Niş Production Patterns: API ve Loglama

---

### Gün 100 — Idempotency: API ve Message Consumer

**Teorik:**
- Idempotency nedir? Aynı işlemi N kez yapmak = 1 kez yapmak
- HTTP method idempotency tablosu:
  - GET, PUT, DELETE → doğası gereği idempotent
  - POST → idempotent **değil**, uygulama katmanında sağlanmalı
- **Idempotency Key Pattern:**
  - Client `Idempotency-Key: <uuid>` header'ı gönderir
  - Server key'i cache'e kaydeder (Redis), response'u saklar
  - Aynı key gelirse cache'den dön, işlemi tekrar yapma
  - TTL: kaç saat/gün saklanmalı?
- ASP.NET Core'da `IIdempotencyFilter` implementasyonu
- `IdempotentAPI` NuGet paketi ile attribute-based
- Başarısız response cache'lenebilir mi? (Kritik karar: 400 saklanır, 500 saklanmaz)
- **Consumer tarafında idempotency (Inbox Pattern):**
  - Her mesajın unique message ID'si var
  - `ProcessedMessages` tablosuna kaydet (idempotency table)
  - Aynı ID gelirse işlemi atla
  - DB unique constraint ile garantile

---

### Gün 101 — Structured Logging: Production-Grade

**Teorik:**
- Structured logging vs düz string logging — neden fark kritik?
  - `"User {UserId} logged in"` → aranabilir, filtrelenebilir
  - `$"User {userId} logged in"` → sadece string, analiz edilemez
- Serilog enricher'ları:
  - `Enrich.FromLogContext()` — `LogContext.PushProperty()` ile dinamik property
  - `Enrich.WithMachineName()`, `WithThreadId()`, `WithEnvironmentName()`
  - Custom enricher yazımı
- **Correlation ID vs TraceId vs Activity.Id farkı:**
  - `HttpContext.TraceIdentifier` — eski yaklaşım, sadece bu uygulama
  - `Activity.Current?.Id` — W3C TraceContext, distributed trace ID
  - `X-Correlation-ID` header — business correlation, manuel yayılım
  - Hangisi ne zaman kullanılır?
- W3C TraceContext propagation — `traceparent` header, servisler arası
- **Hassas veri maskeleme:**
  - Password, token, kredi kartı log'a yazılmamalı
  - Serilog destructuring policy ile alan maskeleme
  - `[LogMasked]` attribute pattern
- Log seviyesi dinamik değiştirme — production'da debug açma (`IConfigurationRoot.Reload()`)
- Log aggregation: Seq (local dev), Elastic Stack, Grafana Loki
- Ne loglanmalı, ne loglanmamalı? — log noise vs log gap

---

### Gün 102 — Problem Details RFC 9457 ve API Hata Standartları

**Teorik:**
- RFC 7807 → RFC 9457 evrimi (Temmuz 2023) — `ProblemDetails` için yeni standart
- `ProblemDetails` anatomy: `type`, `title`, `status`, `detail`, `instance`
- ASP.NET Core built-in `ProblemDetails` desteği — `AddProblemDetails()`
- Custom `IProblemDetailsWriter`
- Extension fields — domain'e özgü hata bilgisi ekleme
- Validation hatalarını `ValidationProblemDetails` ile standartlaştırma
- `IExceptionHandler` (ASP.NET Core 8+) — exception → ProblemDetails mapping
- Client'ın hata tipine göre davranması — `type` URI'si ile hata kataloğu
- API deprecation:
  - `Deprecation` header (RFC 8594) — bu endpoint kullanımdan kalkacak
  - `Sunset` header — ne zaman kalkacak?
  - ASP.NET API Versioning ile deprecated version işaretleme

---

### Gün 103 — Data Protection API ve Şifreleme Temelleri

**Teorik:**
- .NET Data Protection API — ASP.NET Core'un built-in şifreleme sistemi
- `IDataProtector` — purpose-based key isolation
- `IDataProtectionProvider.CreateProtector("purpose")`
- `Protect()` / `Unprotect()` — application restart'ta kalıcılık
- Key ring yönetimi — key rotasyonu, key storage (file, Redis, Azure Key Vault)
- Token şifreleme — email confirmation, password reset token güvenliği
- `TimeLimitedDataProtector` — süreli token
- Şifreleme ≠ hashing — ne zaman hangisi?
- Password hashing: `IPasswordHasher<T>` — PBKDF2 (varsayılan), BCrypt, Argon2
- Genel kriptografi: `AesGcm`, `RSA` — ne zaman low-level API kullanılır?

---

### Gün 104 — Domain Event Dispatch: Doğru Mimari

**Teorik:**
- Domain event ne zaman dispatch edilmeli? `SaveChanges` öncesi mi, sonrası mı?
- **Seçenek 1:** `SaveChanges` öncesi dispatch → event işlenirse ama save fail olursa?
- **Seçenek 2:** `SaveChanges` sonrası dispatch → save başarılı, event dispatch fail olursa?
- **Seçenek 3:** Outbox pattern → en güvenli, transactional
- `SaveChangesInterceptor` ile `SavedChangesAsync`'te event dispatch
- `IMediator.Publish()` ile in-process domain event dispatch
- Domain event handler'ların transaction scope'u: aynı transaction mı, yeni mi?
- `IDomainEventHandler<T>` vs `INotificationHandler<T>` (MediatR)
- Domain event vs Integration event ayrımı — ne zaman hangisine dönüşür?

---

### Gün 105 — Feature Flags ve Dark Launching

**Teorik:**
- Feature flag nedir? Deploy ≠ Release ayrımı
- Feature flag türleri:
  - Release flag — yeni özelliği kapat, hazır olunca aç
  - Experiment flag — A/B test
  - Ops flag — circuit breaker benzeri kill switch
  - Permission flag — belirli kullanıcıya aç
- Microsoft.FeatureManagement — ASP.NET Core native
- `IFeatureManager.IsEnabledAsync("FeatureName")`
- `[FeatureGate("FeatureName")]` — action filter
- Configuration-based flags vs database-based flags
- Feature flag temizliği — flag borcu (flag debt) nedir?
- LaunchDarkly, Azure App Configuration integration

---

## Haftalar 15–16 — Advanced API: Security, gRPC, SignalR

### Gün 106–110 — API Security Derinlemesine

**Teorik:**
- OAuth 2.0 grant types — Authorization Code, Client Credentials, PKCE
- JWT refresh token rotation
- API key yönetimi
- CORS — same-origin policy, pre-flight request
- CSRF — SPA'da neden daha az sorun?
- SQL Injection koruması — parametrized query, EF Core varsayılan korur
- XSS koruması — Content-Security-Policy header
- OWASP API Security Top 10 (2023)
- `HybridCache` (.NET 9) — L1+L2 cache pattern

---

### Gün 111 — Identity Server, Keycloak ve OpenID Connect (Mikroservis Auth)

**Teorik:**
- OpenID Connect nedir? OAuth 2.0 üzerine kimlik katmanı — `id_token` vs `access_token`
- Identity Provider (IdP) rolü — mikroservislerde merkezi auth
- IdentityServer (Duende) — .NET native IdP, `IProfileService`, client credentials flow
- Keycloak — open-source enterprise IdP, realm/client/role kavramları
- Client Credentials flow — servisler arası auth (M2M)
- Authorization Code + PKCE flow — kullanıcı girişi (SPA/mobile)
- JWT Bearer validation — mikroserviste token doğrulama, `AddJwtBearer`
- Token introspection vs local JWT validation — trade-off
- Scope ve audience (`aud`) doğrulaması — servis bazlı erişim kısıtlama
- Refresh token rotation — güvenli token yenileme
- API Gateway'de auth — merkezi doğrulama vs dağıtık doğrulama

**Pratik:**
1. Keycloak Docker'da ayağa kaldır — realm, client, kullanıcı oluştur
2. `CatalogService` için Keycloak'tan JWT Bearer token al (Postman)
3. ASP.NET Core'da `AddJwtBearer` ile Keycloak token doğrulama
4. Client Credentials ile `OrderService → CatalogService` çağrısı (M2M auth)
5. IdentityServer (Duende) minimal kurulum — `AddIdentityServer`, `AddInMemoryClients`
6. Policy-based authorization — `RequireScope("catalog.read")`
7. API Gateway (YARP) üzerinde merkezi JWT doğrulama

**Sorular:**
1. IdentityServer ile Keycloak arasındaki temel fark nedir? Ne zaman hangisi?
2. `id_token` ile `access_token` farkı nedir, mikroserviste hangisini kullanırsın?
3. API Gateway'de auth mi yapmalısın yoksa her serviste mi? Trade-off nedir?
4. Client Credentials flow'da `scope` nasıl servis bazlı erişim kontrolü sağlar?
5. JWT'nin lokal doğrulanması ile introspection endpoint'in farkı nedir?

---

### Gün 112–114 — gRPC ve Protocol Buffers

**Teorik:**
- gRPC neden REST'ten farklı? Binary protocol, HTTP/2
- Protocol Buffers — IDL, code generation
- gRPC service types: Unary, Server streaming, Client streaming, Bidirectional streaming
- ASP.NET Core gRPC — `Grpc.AspNetCore`
- gRPC-JSON transcoding — REST gateway
- gRPC vs REST ne zaman hangisi?

---

### Gün 115–118 — SignalR: Real-Time İletişim

**Teorik:**
- SignalR — WebSocket abstraction
- WebSocket vs Server-Sent Events vs Long Polling — SignalR fallback
- Hub pattern
- Groups ve Connections
- Scale-out — Redis backplane
- SignalR vs gRPC bidirectional streaming — ne zaman hangisi?

---

# FAZ 5 — MİKROSERVİSLER VE PRODUCTION

### Bu Fazda Kodlayacaklarımız (`Faz5-Mikroservisler/`)

| Servis | Ne yapılır |
|--------|-----------|
| `ApiGateway/` | YARP ile reverse proxy, rate limiting |
| `CatalogService/` | Kitap kataloğu — Onion + CQRS + REST API |
| `OrderService/` | Sipariş — Saga pattern, Outbox, RabbitMQ publish |
| `NotificationService/` | RabbitMQ consumer, email bildirim |
| `docker/` | docker-compose ile tüm servisleri ayağa kaldırma |

> Faz3'te yazdığımız Onion kodunu alıp CatalogService'e taşıyacağız — sıfırdan yazmıyoruz.

---

## Hafta 17 — Mikroservis Temelleri

### Gün 119 — Monolith → Microservice: Ne Zaman?

**Teorik:**
- Monolith'in avantajları — basitlik, transaction, debugging
- Microservice'in avantajları — bağımsız deploy, ölçekleme, fault isolation
- "Distributed monolith" antipattern — en kötü dünya
- Martin Fowler'ın Microservice Prerequisites
- Strangler Fig pattern — monolith'i parçalama stratejisi
- Domain-Driven Design → Bounded Context → Microservice sınırı

---

### Gün 120 — Bounded Context ve Servis Sınırları

**Teorik:**
- Bounded Context nedir? DDD strategic design
- Context Map — bounded context'ler arası ilişki
- Shared Kernel, Customer-Supplier, Conformist, Anticorruption Layer
- Servis sınırı nasıl çizilir? — "yalnız deploy edilebilir" testi
- Veri sahipliği — her servis kendi DB'si (Database per Service pattern)
- Shared database antipattern

---

### Gün 121 — Servisler Arası İletişim

**Teorik:**
- Synchronous: REST, gRPC
- Asynchronous: Message broker (RabbitMQ, Kafka, Azure Service Bus)
- Command vs Event — mesaj türleri
- Request-Reply pattern — async ama yanıt bekleniyor
- Fire and Forget — yanıt beklenmiyor
- Choreography vs Orchestration — event zinciri vs merkezi koordinatör

---

### Gün 122 — API Gateway

**Teorik:**
- API Gateway pattern — tek giriş noktası
- Reverse proxy görevleri: routing, auth, rate limiting, SSL termination
- YARP (Yet Another Reverse Proxy) — .NET native
- Kong, Nginx, Ocelot karşılaştırması
- BFF (Backend for Frontend) pattern — frontend türüne göre ayrı API gateway
- API Gateway antipattern — iş mantığı gateway'de olmamalı

---

### Gün 123 — Service Discovery ve Load Balancing

**Teorik:**
- Service Discovery — DNS-based vs client-side vs server-side
- Consul, Kubernetes DNS, Eureka karşılaştırması
- Load balancing stratejileri — round-robin, least connections, consistent hashing
- Health check entegrasyonu
- Service mesh nedir? Istio/Linkerd kavramı

---

### Gün 124 — Hafta 17 Özet

**Mimari soru:**
- 3 bounded context belirle, servis sınırlarını neden orada çizdiğini açıkla
- Hangi iletişim: Sipariş oluşturuldu → stok düşürüldü → email gönderildi

---

## Hafta 18 — Message Broker'lar

### Gün 125 — RabbitMQ: Kavramsal Derinlik

**Teorik:**
- AMQP protokolü
- Exchange types: Direct, Topic, Fanout, Headers
- Queue ve binding
- Message durability — durable queue + persistent message
- Dead Letter Queue (DLQ) — başarısız mesajlar nereye?
- Consumer acknowledgment — manual ack neden önemli?
- Prefetch count — consumer throughput ayarı
- MassTransit ile RabbitMQ entegrasyonu

---

### Gün 126 — Apache Kafka: Log-Based Messaging

**Teorik:**
- Kafka vs RabbitMQ — temel fark: log vs queue
- Topic, partition, consumer group
- Offset management — consumer'ın nerede olduğu
- Retention policy — mesajlar silinmez, retention süresi var
- Event sourcing için Kafka neden uygun?
- Exactly-once semantics — transaction producer + idempotent consumer
- Confluent .NET client vs MassTransit Kafka transport

---

### Gün 127 — Outbox Pattern: At-Least-Once Delivery

**Teorik:**
- Problem: DB kaydet + mesaj gönder — ikisi atomik değil
- Outbox pattern çözümü: DB'ye outbox tablosuna kaydet, worker gönder
- Polling publisher vs transaction log tailing (Debezium)
- MassTransit Outbox entegrasyonu
- Idempotency — aynı mesaj iki kez gelirse?
- Idempotency key pattern

---

### Gün 128 — Saga Pattern: Distributed Transaction

**Teorik:**
- 2-Phase Commit neden mikroserviste çalışmaz?
- Saga pattern — distributed transaction alternatifi
- Choreography-based Saga — event zinciri
- Orchestration-based Saga — merkezi state machine
- Compensating transaction — geri alma işlemi
- MassTransit Saga (State Machine) ile implementasyon
- Saga dayanıklılık — state persistence

---

### Gün 129 — Hafta 18 Özet

**Senaryo:**
- Sipariş oluştur → ödeme al → kargo hazırla → bildirim gönder
- Bu akışı Saga pattern ile tasarla
- Her adımda hata olursa compensating transaction nedir?

---

## Hafta 19 — Resilience ve Fault Tolerance

### Gün 130 — Circuit Breaker Pattern

**Teorik:**
- Cascade failure — bir servisin düşmesi zincirleme etkisi
- Circuit Breaker — Closed → Open → Half-Open state machine
- Polly kütüphanesi — .NET resilience standardı
- Microsoft.Extensions.Resilience — .NET 8+ standardı
- Circuit breaker thresholds — failure rate, duration
- `HttpClient` + Polly entegrasyonu

---

### Gün 131 — Retry ve Timeout Stratejileri

**Teorik:**
- Retry — idempotent endpoint'lerde güvenli
- Exponential backoff + jitter — thundering herd önleme
- Retry storm antipattern — retry + circuit breaker birlikte
- Timeout — ne kadar beklemeli?
- Deadline propagation — upstream timeout'u downstream'e aktar
- Polly Resilience Pipeline — retry + circuit breaker + timeout zinciri

---

### Gün 132 — Bulkhead Pattern

**Teorik:**
- Bulkhead — bir servisin tüm thread'i tüketmesini önle
- Thread isolation vs semaphore isolation
- `SemaphoreSlim` ile bulkhead
- Polly bulkhead policy
- Resource isolation stratejisi

---

### Gün 133 — Fallback ve Graceful Degradation

**Teorik:**
- Fallback — servis düştüğünde ne sunulur?
- Cache-based fallback — stale data vs no data
- Default response fallback
- Graceful degradation — azaltılmış işlevsellik, tam çöküş değil
- Feature flags — devre kesici olmadan özellik kapatma

---

### Gün 134 — Hafta 19 Özet

**Resilience tasarımı:**
- `HttpClient` için Polly pipeline tasarla (retry 3x, circuit breaker, timeout 5s)
- Ödeme servisine yapılan çağrı düştüğünde graceful degradation stratejin ne?

---

## Hafta 20 — Distributed Systems: İleri Kavramlar

### Gün 135 — CAP Teoremi ve Consistency Modelleri

**Teorik:**
- CAP teoremi — Consistency, Availability, Partition Tolerance
- CP vs AP — hangisi ne zaman?
- PACELC — CAP'ın daha gerçekçi versiyonu
- Eventual Consistency — ne zaman yeterli?
- Strong Consistency — ne zaman zorunlu?
- CRDT — conflict-free replicated data types kavramı

---

### Gün 136 — Distributed Tracing ve Observability: Derinlemesine

**Teorik:**
- Three pillars of observability: Logs, Metrics, Traces
- OpenTelemetry — vendor-neutral standart, CNCF projesi
- Trace context propagation — W3C TraceContext (`traceparent`, `tracestate` header)
- `ActivitySource` ve `Activity` — .NET native tracing (OTel bunun üzerine kurulu)
- `ActivitySource.StartActivity("OperationName")` — span oluşturma
- Activity tags, baggage, events
- Sampling stratejileri — head-based vs tail-based
- **Custom Metrics ile uygulama sağlığı ölçme:**
  - `Meter` ve `Counter<T>`, `Histogram<T>`, `ObservableGauge<T>`
  - `System.Diagnostics.Metrics` API — .NET 6+ native
  - `builder.Services.AddMetrics()` + `AddMeter("MyApp.Orders")`
  - Örnek custom metrikler:
    - `orders_created_total` — Counter
    - `order_processing_duration_ms` — Histogram
    - `active_connections` — ObservableGauge
  - OpenTelemetry'ye custom meter kaydetme
  - Prometheus scrape endpoint — `UseOpenTelemetryPrometheusScrapingEndpoint()`
  - Grafana dashboard tasarımı — RED (Rate, Errors, Duration) metrik seti
- Grafana stack: Loki (logs) + Tempo (traces) + Mimir/Prometheus (metrics)
- **Correlation: log + trace birleştirme**
  - `TraceId` ve `SpanId`'yi log'a otomatik ekleme
  - Serilog + OTel entegrasyonu
- .NET Aspire built-in OTel dashboard — local dev için

**Pratik kural:**
```
Loglama → ne oldu (olay)
Tracing → nerede, ne kadar sürdü (akış)
Metrik  → ne sıklıkta, ne kadar (ölçüm)
```

---

### Gün 137 — Event Sourcing

**Teorik:**
- Event Sourcing nedir? State yerine event'leri sakla
- Aggregate state'i event replay ile yeniden inşa et
- Snapshot pattern — replay performansı için
- Event store — Marten (PostgreSQL), EventStoreDB
- CQRS + Event Sourcing kombinasyonu
- Event versioning — event şeması değişirse?
- Upcasting — eski event'leri yeni schema'ya çevir

---

### Gün 138 — Idempotency ve Message Deduplication

**Teorik:**
- At-most-once vs At-least-once vs Exactly-once
- Idempotency key — duplicate request detection
- Inbox pattern — consumer tarafında deduplication
- Database unique constraint ile idempotency
- Distributed lock — Redis SETNX ile leader election

---

### Gün 139 — Data Consistency Patterns

**Teorik:**
- Database per Service — data isolation
- API Composition — query side aggregation
- CQRS read model — denormalized, hızlı okuma
- Change Data Capture (CDC) — Debezium ile event üretme
- Materialized View pattern — precomputed read model

---

### Gün 140 — Hafta 20 Özet

**Büyük mimari soru:**
- Event Sourcing mi yoksa CRUD + Domain Events mi? Ne zaman hangisi?
- Distributed tracing olmadan mikroserviste debug nasıl yaparsın?
- Idempotency key neden sadece HTTP POST için değil, mesaj tüketiminde de gerekli?

---

## Hafta 21 — .NET Aspire: Cloud-Native Geliştirme

> .NET Aspire, 2024-2025'te ortaya çıkan ve 2026'da mikroservis geliştirmenin standart yolu haline gelen framework. Mikroservisler bölümünün son halkası.

---

### Gün 141 — .NET Aspire Nedir? Neden Önemli?

**Teorik:**
- .NET Aspire — opinionated, cloud-native, distributed app framework
- Çözdüğü problem: "10 servisi nasıl local'de aynı anda ayağa kaldırırım?"
- AppHost projesi — orkestrasyon merkezi (kod ile, YAML değil)
- ServiceDefaults projesi — ortak config: OTel, health check, resilience
- Aspire ne değildir? Kubernetes değil, servis mesh değil, runtime değil
- **Aspire'ın getirdikleri:**
  - Tek komutla tüm stack: API + DB + Redis + RabbitMQ + Frontend
  - Built-in OpenTelemetry dashboard (local dev)
  - Service discovery otomatik — `http://catalog-service` çalışır
  - Resource konfigürasyonu C# ile — tip güvenli
  - Deployment: Azure Container Apps, Kubernetes manifest üretimi

---

### Gün 142 — Aspire AppHost ve Komponent Modeli

**Teorik:**
- `IDistributedApplicationBuilder` — kaynak (resource) tanımlama
- `.AddProject<T>()` — .NET projesi ekleme
- `.AddPostgres()`, `.AddRedis()`, `.AddRabbitMQ()` — infrastructure kaynakları
- `.WithReference()` — bağımlılık ve connection string aktarımı
- Environment variable injection — Aspire otomatik ayarlar
- Health check entegrasyonu — dashboard'da görünür
- **ServiceDefaults:**
  - `AddServiceDefaults()` — tek satırda OTel + health + resilience
  - Tüm servislerde standart konfigürasyon

---

### Gün 143 — Aspire ile Mikroservis Geliştirme Akışı

**Teorik:**
- Aspire olmadan vs Aspire ile geliştirme deneyimi karşılaştırması
- Local dev → Aspire dashboard (trace, log, metric hepsi bir arada)
- Aspire manifest üretimi — `dotnet run --publisher manifest`
- Azure Container Apps deployment — `azd up`
- Aspire ve Kubernetes — manifest generate, sonra kubectl apply
- Aspire 13 (2025+) yenilikleri — Python/Node.js servisleri de orkestre
- Ne zaman Aspire, ne zaman düz docker-compose?

```
docker-compose → basit, Aspire bilgisi gerekmez, CI/CD standard
Aspire         → .NET ekosistemi, OTel dashboard, tip-güvenli config, Azure-first
```

---

## Haftalar 20–21 — Containerization ve Kubernetes

### Gün 144–147 — Docker: .NET Servisleri

**Teorik:**
- Container vs VM — namespace ve cgroup izolasyonu
- Dockerfile best practices — layer caching, multi-stage build
- .NET multi-stage Dockerfile — build stage + runtime stage
- Alpine vs Debian base image — boyut vs uyumluluk
- `ENTRYPOINT` vs `CMD`
- Health check in Dockerfile
- Non-root user — güvenlik
- Docker Compose — local mikroservis ortamı

---

### Gün 148–152 — Kubernetes: .NET Servisleri için

**Teorik:**
- Pod, ReplicaSet, Deployment
- Service — ClusterIP, NodePort, LoadBalancer
- Ingress — HTTP routing, TLS termination
- ConfigMap ve Secret — konfigürasyon yönetimi
- Liveness ve Readiness probe — ASP.NET Core health check entegrasyonu
- Resource requests ve limits — OOM kill önleme
- Horizontal Pod Autoscaler (HPA) — CPU/memory'e göre ölçekleme
- Rolling update stratejisi — zero-downtime deploy

---

## Haftalar 21–22 — CI/CD, Güvenlik ve Final Proje

### Gün 153–157 — CI/CD Pipeline

**Teorik:**
- GitHub Actions ile .NET CI/CD
- Build → Test → Lint → Docker Build → Push → Deploy
- Branch stratejisi — trunk-based vs gitflow
- Feature flags — deployment ≠ release
- Blue/Green deployment
- Canary release

---

### Gün 158–162 — Güvenlik: Production Checklist

**Teorik:**
- Secret management — Azure Key Vault, HashiCorp Vault
- TLS everywhere — servisler arası de
- mTLS — mutual authentication (service mesh)
- Network policy — Kubernetes'te pod-to-pod iletişim kısıtlama
- SAST/DAST — statik ve dinamik güvenlik analizi
- Dependency scanning — `dotnet list package --vulnerable`
- OWASP ZAP, Snyk

---

### Gün 163–170 — Final Proje: Mini E-Ticaret Mikroservis Mimarisi

**Kavramsal tasarım görevi (kod değil, mimari):**

1. **Bounded Context belirleme:** Catalog, Order, Payment, Notification, User/Identity
2. **Servis sınırları:** Her servisin sorumluluğu ve sahip olduğu data
3. **İletişim haritası:** Hangi servisler sync (gRPC), hangileri async (RabbitMQ)?
4. **Saga tasarımı:** Sipariş oluşturma Saga'sını state machine olarak çiz
5. **Resilience stratejisi:** Her servis çağrısı için Polly pipeline
6. **Observability planı:** Hangi metrik, hangi trace, hangi log?
7. **Deployment:** Her servis için Kubernetes manifest yapısı
8. **Security:** JWT issuer, servisler arası auth stratejisi

---

# KAYNAKLAR

## Zorunlu Kitaplar

| Kitap | Konu | Öncelik |
|-------|------|---------|
| CLR via C# — Jeffrey Richter | .NET internals, GC, threading | Yüksek |
| C# in Depth — Jon Skeet | Dil derinliği | Yüksek |
| Designing Data-Intensive Applications — Kleppmann | Distributed systems | Yüksek |
| Domain-Driven Design — Eric Evans | DDD | Orta |
| Clean Architecture — Robert C. Martin | Mimari | Orta |
| Building Microservices — Sam Newman | Microservices | Orta |
| Patterns of Enterprise Application Architecture — Fowler | Design patterns | Referans |

## Faydalı Kaynaklar

- Microsoft Learn — ASP.NET Core, EF Core resmi dokümantasyon
- Andrew Lock — andrewlock.net (ASP.NET Core derinlemeleri)
- Nick Chapsas — YouTube (.NET performance, modern patterns)
- Milan Jovanović — .NET architecture blog/YouTube
- dotnet/runtime GitHub — gerçek implementasyon okuma

## Araçlar

| Araç | Amaç |
|------|------|
| BenchmarkDotNet | Micro-benchmark |
| dotMemory | Memory profiling |
| dotTrace | CPU profiling |
| dotnet-counters | Runtime metrics |
| dotnet-trace | Event tracing |
| Postman / httpie | API testing |
| Jaeger / Grafana Tempo | Distributed tracing |

---

# ÖZET: MID+ SEVİYE GELİŞTİRİCİ OLMANİN KRİTERLERİ

Müfredatın sonunda şunları yapabilir olmalısın:

**C# & .NET Temelleri**
- [ ] CLR'nin bellek modelini ve GC davranışını açıklayabilmek
- [ ] Memory leak'i kod incelemesiyle tespit edebilmek
- [ ] async/await deadlock senaryosunu açıklayabilmek
- [ ] `IDisposable` Dispose Pattern'i doğru implement edebilmek
- [ ] `IHttpClientFactory` ile doğru HttpClient kullanımı

**Test Yazımı**
- [ ] xUnit + Moq + FluentAssertions ile unit test yazabilmek
- [ ] Mock vs Stub farkını bilerek doğru test double seçmek
- [ ] `WebApplicationFactory` ile integration test kurabilmek
- [ ] TestContainers ile gerçek DB'ye karşı test çalıştırabilmek
- [ ] NetArchTest ile architecture kurallarını otomatik doğrulayabilmek

**Mimari**
- [ ] Onion Architecture'da katman sorumluluklarını doğru dağıtmak
- [ ] CQRS + MediatR pipeline tasarlayabilmek
- [ ] Domain Event ve Integration Event ayrımını uygulamak
- [ ] Result pattern ile exception-free hata yönetimi yapabilmek

**Veri Erişimi & Cache**
- [ ] EF Core'daki N+1 problemini ve çözümlerini bilmek
- [ ] SQL index stratejisini (covering, filtered, composite) açıklayabilmek
- [ ] Redis veri yapılarını ve Cache-aside pattern'i uygulayabilmek
- [ ] Keyset pagination ile offset pagination farkını bilmek

**Mikroservisler & Production**
- [ ] Mikroservis sınırlarını Bounded Context'e göre çizmek
- [ ] Saga pattern ile distributed transaction yönetmek
- [ ] Outbox pattern ile mesaj güvenilirliği sağlamak
- [ ] Polly ile resilience pipeline kurmak
- [ ] OpenTelemetry ile distributed tracing yapılandırmak
- [ ] Docker + Kubernetes ile .NET servisi deploy etmek

---

## MÜFREDATTAKİ ARAŞTIRMA BULGULARI — NE EKSIK BULUNDU?

Araştırma sonrası müfredata eklenen konular:

| Eklenen Konu | Neden Kritik | Nereye Eklendi |
|-------------|-------------|----------------|
| xUnit + Moq + FluentAssertions | Mid+ seviyenin net göstergesi, interview'larda sorulur | Hafta 6 |
| WebApplicationFactory + TestContainers | Production-grade integration test | Hafta 6 |
| NetArchTest | Onion kurallarını otomatik koruma | Hafta 6 + Faz3 |
| IHttpClientFactory & Typed Client | Socket exhaustion production bug'ı | Faz2 Hafta 4 |
| Redis derinlemesine | Distributed cache olmadan mid+ olmaz | Faz4 |
| SQL Index stratejisi | Query plan okumak senior bilgisi ama mid+ bilmeli | Faz4 |
| TDD yaklaşımı | Test önce mi, kod önce mi tartışması | Hafta 6 |
| **Soft Delete + Global Query Filter** | Veri silme yanlışlıkla prodda felakete yol açar | Hafta 13 |
| **Audit Trail (SaveChangesInterceptor)** | GDPR, debug, "kim ne yaptı?" sorusu | Hafta 13 |
| **Optimistic Concurrency (RowVersion)** | Eş zamanlı yazma çakışması, gerçek prodda çok görülür | Hafta 13 |
| **Multi-tenancy (row-level + Global Filter)** | SaaS ürünlerde kaçınılmaz, yanlış yapılırsa veri sızıntısı | Hafta 13 |
| **EF Core Interceptor pipeline** | Soft delete, audit, domain event dispatch için temel | Hafta 13 |
| **Idempotency Key (API + Inbox Pattern)** | Retry storm + double payment en kritik production bug | Hafta 14 |
| **Structured Logging derinliği** | Correlation ID, hassas veri maskeleme, W3C TraceContext | Hafta 14 |
| **Problem Details RFC 9457** | RFC 7807'nin güncel hali, API hata standardı | Hafta 14 |
| **API Deprecation (Sunset/Deprecation header)** | Versiyonlu API yönetimi için zorunlu | Hafta 14 |
| **Data Protection API + PBKDF2** | Token şifreleme, password hashing — security temeli | Hafta 14 |
| **Domain Event Dispatch mimarisi** | SaveChanges öncesi/sonrası dispatch tuzakları | Hafta 14 |
| **Feature Flags (Microsoft.FeatureManagement)** | Deploy ≠ Release, kill switch, A/B test | Hafta 14 |
| **Background Jobs derinlik (Hangfire/Quartz/Worker)** | "Hangisi ne zaman?" sorusu her projede sorulur | Faz2 Gün 25 |
| **Vertical Slice Architecture** | CQRS'in doğal eşi, 2025'te yaygın yaklaşım | Faz3 |
| **Modüler Monolith** | Mikroservis öncesi hazırlık, ekip büyüyene kadar ideal | Faz3 |
| **Production Diagnostics CLI** | dotnet-dump/gcdump/trace/counters — production'da debugger yok | Faz4 Gün 66b |
| **OTel Custom Metrics** | Counter/Histogram/Gauge — RED metrik seti | Faz5 Gün 109 |
| **.NET Aspire** | 2025/2026 cloud-native standart, mikroservis orkestrasyon | Faz5 Hafta 18b |

---

*Bu müfredat teorik ağırlıklıdır. Her gün sonunda "neden böyle tasarlandı?" sorusunu sor.*
*Kod yazmak değil, mimariyi anlamak ve savunabilmek hedef.*
