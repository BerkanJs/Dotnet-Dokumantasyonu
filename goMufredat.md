# Go Mid+ Seviye Geliştirici Müfredatı
## C# / .NET Tecrübeli Geliştirici İçin

> **Hedef:** 23 haftada güçlü temeller üzerine inşa edilmiş, PostgreSQL ile konuşan production-grade REST API yazabilen, goroutine/channel ile concurrency kurabilen, mikroservis mimarisi anlayan Mid+ Go geliştirici.
>
> **Felsefe:** Go'yu "küçük C#" gibi değil, farklı bir düşünce biçimi olarak öğren. Kompozisyon > kalıtım, açık hata > exception, basitlik > soyutlama. Her konuda "Go neden böyle tasarlandı, C#'tan farkı ne?" sorusu sorulacak.
>
> **Çalışma Yöntemi:** Her faz öncesinde konu anlatımı yapılır, ardından birlikte kod yazılır. Kod örneklerinde her satırın yanında "ne yapar" + "bunu yazmasaydık ne olurdu" yorumu bulunur.
>
> **Domain:** C# müfredatındaki Kitabevi. Aynı problem — kitap listeleme, sipariş, kullanıcı yönetimi — ama Go idiomlarıyla sıfırdan inşa edilir.

---

## PROJE YAPISI

```
GoKitabevi/
│
├── Faz1-Go-Runtime/
│   ├── 01-Runtime-Tipler/       → Go runtime, tip sistemi, bellek
│   ├── 02-Concurrency/          → Goroutine, channel, select, context
│   ├── 03-Interface-Generics/   → Duck typing, generic constraints
│   └── 04-Error-Tooling/        → Error handling, paket sistemi, test
│
├── Faz2-REST-API/
│   └── kitabevi-api/
│       ├── cmd/api/             → main.go — uygulama giriş noktası
│       ├── internal/
│       │   ├── domain/          → iş kuralları, entity'ler, interface'ler
│       │   ├── handler/         → HTTP handler'ları
│       │   ├── repository/      → DB erişim katmanı
│       │   ├── service/         → iş mantığı
│       │   └── middleware/      → auth, logging, recovery, validation
│       ├── db/
│       │   ├── migrations/      → golang-migrate SQL dosyaları
│       │   └── queries/         → sqlc .sql dosyaları
│       └── docker-compose.yml
│
├── Faz3-Mimari/
│   ├── 01-SOLID/                → Go'da SOLID prensipleri
│   ├── 02-Patterns/             → GoF pattern'lerin Go versiyonları
│   └── 03-Clean-Architecture/   → Hexagonal / Clean mimari + CQRS
│
├── Faz4-Performans/
│   ├── 01-Profiling/            → pprof, trace, go tool
│   ├── 02-Memory/               → escape analysis, sync.Pool, allocations
│   ├── 03-DB-Tuning/            → pgx batch, prepared statements
│   └── 04-Observability/        → OpenTelemetry, Prometheus, Grafana
│
└── Faz5-Mikroservisler/
    ├── gateway/                 → reverse proxy
    ├── catalog-service/         → kitap kataloğu
    ├── order-service/           → sipariş (Saga pattern)
    └── docker/                  → docker-compose.yml
```

---

## GENEL BAKIŞ — 5 FAZLI YOL HARİTASI

| Faz | Hafta | Konu | Seviye |
|-----|-------|------|--------|
| 1 | 1–2 | Go Runtime, Tip Sistemi, Concurrency | Temel/Orta |
| 2 | 3–11 | REST API, PostgreSQL, sqlc, JWT, Redis, OAuth2, CI/CD, Validation, Test | Orta |
| 3 | 12–14 | Mimari, SOLID, Design Patterns, Clean Arch, CQRS | Orta/İleri |
| 4 | 15–18 | Performans, pprof, Memory, DB Tuning, Observability | İleri |
| 5 | 19–23 | Mikroservisler, gRPC, NATS, Saga Pattern | Senior Hazırlık |

> **C# ile fark:** Faz 2'de MVC yok. Direkt `net/http` + PostgreSQL'den başlanır. Go'da "framework" değil "standart kütüphane + minimal bağımlılık" felsefesi var.

---

# FAZ 1 — Go Runtime & Temel Kavramlar

> C#/Java bilen biri için Go'nun sözdizimi 1 haftada öğrenilir. Asıl mesele: Go'nun neden class yok, neden inheritance yok, neden exception yok diye tasarlandığını anlamak. Bu faz o "neden"lere odaklanır.

### Bu Fazda Kodlayacaklarımız (`Faz1-Go-Runtime/`)

| Klasör | Ne yapılır |
|--------|-----------|
| `01-Runtime-Tipler/` | GC analizi, escape analysis, struct vs pointer, value semantics |
| `02-Concurrency/` | goroutine, channel, select, WaitGroup, Mutex, context |
| `03-Interface-Generics/` | duck typing, interface composition, generic constraints |
| `04-Error-Tooling/` | error wrapping, sentinel errors, testify, mockery, benchmark |

---

## Hafta 1 — Go Runtime, Tip Sistemi ve Bellek Modeli

### Gün 1 — Go Runtime Nedir? CLR/JVM ile Karşılaştırma

**Teorik:**
- Go runtime mimarisi — garbage collector, goroutine scheduler, stack management
- CLR'deki JIT → Go'da yok: Go doğrudan makine koduna derlenir (AOT benzeri)
- Go binary — tek dosya, runtime gömülü, dependency yok
- `GOMAXPROCS` — kaç OS thread kullanılacağı
- Go GC — concurrent, tri-color mark-and-sweep, stop-the-world minimumu
- Go GC'de nesil kavramı yok (non-generational) — CLR Gen0/1/2 ile karşılaştırma
- Stack boyutu: C# thread ~1MB sabit → Go goroutine ~2KB başlar, dinamik büyür

**C# ile karşılaştırma:** CLR vs Go Runtime — derleme modeli, GC türü, thread modeli, binary boyutu

**Kontrol Soruları:**
1. Go binary neden "bağımsız"? CLR ile farkı ne?
2. Goroutine neden OS thread'den daha ucuz?
3. JIT warmup neden Go'da sorun değil, C#'ta neden sorun olabiliyor?

---

### Gün 2 — Tip Sistemi: Struct, Interface, No Class

**Teorik:**
- Go'da `class` yok — sadece `struct`
- Kalıtım (inheritance) yok — kompozisyon var (embedding)
- Interface: implicit implementation — C#'taki `implements` yok
- Pointer vs value receiver — ne zaman hangisi?
- Zero value — Go'da her tip sıfır değere sahiptir, null yoktur
- `nil` — sadece pointer, interface, slice, map, channel, func için geçerli
- Struct embedding ile kompozisyon — C#'ta `class Kitap : Yazar` vs Go embedding
- `iota` — Go'da sabit gruplama, C#'ta `enum` yerine
- `stringer` tool — `iota` değerlerine otomatik `String()` metodu üret
- Typed constant pattern: `type Durum int` + `iota` → Ardalis.SmartEnum'un Go karşılığı

**C# ile karşılaştırma:** explicit interface vs duck typing, class hiyerarşisi vs composition, `enum` vs `iota` + typed constant

**Kontrol Soruları:**
1. Go'da kalıtım neden yok? Kompozisyon ne sağlar?
2. Value receiver vs pointer receiver — ne zaman hangisi?
3. Zero value neden Go'da güvenli, C#'ta null reference neden tehlikeli?

---

### Gün 3 — Bellek Modeli: Escape Analysis, Stack ve Heap

**Teorik:**
- Go'da stack vs heap kararını sen vermezsin — compiler verir (escape analysis)
- `new(T)` → illa heap değil; compiler stack'te tutabilir
- `&T{}` → illa heap değil; escape etmiyorsa stack'te kalır
- Escape analysis: değişken fonksiyon dışına "kaçıyor" mu?
- `go build -gcflags="-m"` → escape analysis raporunu gösterir
- GC baskısını azaltmak: heap allocation'ı minimize et
- Ne zaman pointer kullan: büyük struct, mutate, nil kontrolü, interface assign

**C# ile karşılaştırma:** value type → stack (genelde), reference type → heap garantisi vs Go'da compiler kararı

**Kontrol Soruları:**
1. `new(Kitap)` yazdığında Go neden illa heap kullanmaz?
2. Escape analysis neden önemli? GC ile bağlantısı ne?
3. C#'ta `struct` neden stack'te, Go'da struct neden her zaman stack'te değil?

---

### Gün 4 — Goroutine ve Go Scheduler

**Teorik:**
- Goroutine: Go'nun hafif thread'i — başlangıçta ~2KB stack
- OS Thread: Linux'ta ~8MB stack → 10K thread = 80GB RAM → imkânsız
- M:N threading model — M goroutine, N OS thread'e map edilir
- Go Scheduler GMP modeli: G (goroutine), M (machine/OS thread), P (processor/context)
- `GOMAXPROCS`: kaç P çalışır = kaç goroutine aynı anda paralel çalışır
- Goroutine switch: cooperative + preemptive (Go 1.14+ async preemption)
- `go` anahtar kelimesi — goroutine başlatır
- `sync.WaitGroup` — goroutine'lerin bitmesini bekle
- Closure tuzağı — loop variable capture

**C# ile karşılaştırma:** `Task.Run()` vs `go func()`, thread pool vs GMP scheduler

**Kontrol Soruları:**
1. 10.000 goroutine neden mümkün, 10.000 OS thread neden değil?
2. GMP modelinde P (processor) neden var? M ile farkı ne?
3. Closure tuzağı neden oluşur? C#'taki `Task.Run` içinde de aynı sorun var mı?

---

### Gün 5 — Channel ve Select: CSP Concurrency

**Teorik:**
- Go'nun concurrency felsefesi: "Don't communicate by sharing memory; share memory by communicating."
- Channel: goroutine'ler arası güvenli iletişim kanalı (type-safe pipe)
- Unbuffered channel: gönderen bekler alıcı hazır olana kadar — senkron
- Buffered channel: kapasite dolana kadar gönderen beklemez — asenkron
- `select`: birden fazla channel'ı aynı anda bekle
- `close(ch)`: channel'ı kapat — range ile iterasyon için gerekli
- Channel direction: `chan<- T` (sadece gönder), `<-chan T` (sadece al) — type safety
- Pipeline pattern: producer → transform → consumer

**C# ile karşılaştırma:** `Channel<T>` (System.Threading.Channels) vs Go channel, `Task.WhenAny()` vs `select`

**Kontrol Soruları:**
1. Unbuffered channel neden göndereni "bloklar"? Ne zaman kullanılır?
2. `select` ile `switch` arasındaki fark ne?
3. `close(ch)` neden önemli? Kapatılmamış channel'dan `range` ne yapar?

---

### Gün 6 — Error Handling: Go'nun Felsefesi

**Teorik:**
- Go'da exception yok — error, return değeridir
- `error` interface'i: `Error() string` metoduna sahip herhangi bir tip
- Multiple return values: `(T, error)` paterni
- `errors.New()`, `fmt.Errorf()`, `%w` wrap operatörü
- `errors.Is()` — sentinel error karşılaştırması (wrap zincirinde de arar)
- `errors.As()` — belirli error tipini bul
- Sentinel error: `var ErrNotFound = errors.New("not found")` — neden `var`, neden `const` değil
- Custom error tipi: `Error() string` implement eden struct — ekstra context taşımak için
- `panic` ve `recover` — ne zaman kullanılır, ne zaman kullanılmaz
- Result Pattern: `type Result[T any] struct{ Value T; Err error }` — Go'da TS.Result benzeri yaklaşım, ne zaman tercih edilir ne zaman gereksiz

**C# ile karşılaştırma:** exception (görünmez kontrol akışı) vs error return value (görünür), `try/catch` vs `if err != nil`, TS.Result vs Go `(T, error)` tuple

**Kontrol Soruları:**
1. `%w` ile `%v` farkı ne? Neden wrap önemli?
2. Sentinel error neden `var` ile tanımlanır, `const` ile neden tanımlanamaz?
3. C#'ta exception'ı yakalamayı unutabilirsin. Go'da error'ı görmezden gelmek mümkün mü?

---

### Gün 7 — Hafta 1 Özet

**Tekrar soruları:**
1. Go binary neden CLR'den farklı çalışır? Docker'da avantajı ne?
2. Goroutine'in OS thread'den 3 temel farkı nedir?
3. Unbuffered channel deadlock'a nasıl yol açar? Örnek ver.
4. `errors.Is()` vs `errors.As()` — ne zaman hangisi?
5. Escape analysis neden önemli? `go build -gcflags="-m"` ne gösterir?

---

## Hafta 2 — Interface, Generics, Tooling ve Testing

### Gün 8 — Interface: Duck Typing ve Kompozisyon

**Teorik:**
- Go interface: implicit — struct interface'i "implement eder" demesi gerekmez
- Interface satisfaction derleme zamanında kontrol edilir
- Derleme zamanı doğrulama: `var _ Hayvan = (*Kedi)(nil)`
- Interface kompozisyon: küçük interface'leri birleştir (Interface Segregation'ın Go hali)
- Empty interface: `interface{}` veya `any` (Go 1.18+) — ne zaman kullanılır, ne zaman kaçınılır
- Type assertion: `v, ok := i.(Kitap)` — güvenli cast
- Type switch: birden fazla tipe göre dallara ayrılma
- Küçük interface felsefesi: `io.Reader`, `io.Writer`, `io.Closer` — standart kütüphaneden örnekler

**C# ile karşılaştırma:** explicit `implements` vs implicit satisfaction, büyük interface antipattern

**Kontrol Soruları:**
1. Implicit interface neden test yazımında avantajlıdır?
2. `var _ KitapDeposu = (*PostgresKitapRepo)(nil)` satırı ne iş yapar?
3. C#'ta `IEnumerable<T>` Go'daki hangi interface'e benzer?

---

### Gün 9 — Generics (Go 1.18+): Kısıtlamalar ve Type Parameters

**Teorik:**
- Go 1.18 ile gelen generics — C# generics'ten farklı kısıtlama modeli
- Type parameter: `[T any]`, `[T comparable]`, `[T constraints.Ordered]`
- Type constraint: interface ile tanımlanır
- `comparable`: `==` operatörünü destekleyen tipler
- Union constraint: `~int | ~string` — underlying type dahil (`~` operatörü)
- Generic function vs generic type
- `slices`, `maps` paketleri (Go 1.21+) — generic utility'ler
- Ne zaman generic, ne zaman interface — karar kuralı

**C# ile karşılaştırma:** `where T : class` vs Go constraint, type erasure yok — Go runtime'da type korunur (C# reified generics gibi)

**Kontrol Soruları:**
1. `comparable` constraint neden gerekli? `==` neden her tipte çalışmaz?
2. `~int` ile `int` arasındaki fark ne? (underlying type)
3. C# `where T : class` Go'da neye karşılık gelir?

---

### Gün 10 — Context Paketi: Cancellation, Timeout, Values

**Teorik:**
- `context.Context` — Go'da her I/O çağrısına geçirilir, iptal sinyali taşır
- `context.Background()` — kök context, main/test'te kullanılır
- `context.WithCancel()` — iptal edilebilir context, `cancel()` fonksiyonu döner
- `context.WithTimeout()` — süreli context
- `context.WithDeadline()` — kesin zamanlı timeout
- `context.WithValue()` — context'e değer göm (request-scoped data)
- `defer cancel()` — her zaman zorunlu, goroutine leak önler
- Context propagation: her fonksiyona ilk parametre olarak geçir (convention)
- Goroutine leak: context iptal edilince goroutine'ler temizlenmeli

**C# ile karşılaştırma:** `CancellationToken` vs `context.Context` — context daha zengin (value + cancel + deadline)

**Kontrol Soruları:**
1. `defer cancel()` neden her zaman gerekli?
2. `context.WithValue()` ne için kullanılır? Neden tip-güvenli değil?
3. Context neden fonksiyona struct field olarak değil, parametre olarak geçirilir?

---

### Gün 11 — Module Sistemi ve Paket Yapısı

**Teorik:**
- `go.mod` — proje root'unda, module path tanımlar
- `go.sum` — checksum, güvenlik
- `go get`, `go mod tidy`, `go mod vendor` — ne zaman hangisi
- Package: `package main` vs `package kitabevi`
- Exported vs unexported: büyük harf → public, küçük harf → package-private
- `internal/` paketi — sadece üst dizinden erişilebilir
- `cmd/` pattern — binary'ler burada, `internal/` uygulama kodu burada
- Circular import: Go'da yasak — mimariyi doğal olarak zorlar
- Semantic versioning: v0, v1, v2+ — major version değişimi module path etkiler

**C# ile karşılaştırma:** NuGet + .csproj vs go.mod, `internal` modifier vs `internal/` klasörü

**Kontrol Soruları:**
1. `internal/` klasörü ne sağlar? C#'taki `internal` modifier ile farkı?
2. Circular import neden derleme zamanında yakalanıyor, runtime'da değil?
3. `go mod tidy` ne yapar? `go.sum` neden versiyon kontrolünde olmalı?

---

### Gün 12 — Testing: testify, mockery ve Table-Driven Tests

**Teorik:**
- Go'da testing paketi built-in — `testing.T`, `testing.B`
- Test dosyası: `*_test.go` — ayrı dosya zorunluluğu
- Test fonksiyonu: `func TestXxx(t *testing.T)`
- Table-driven test: Go idiomu — test case'lerini anonymous struct slice'ta tut
- `t.Run()` — sub-test, paralel çalışma
- `t.Helper()` — hata satırını doğru göster
- `t.Parallel()` — testleri paralel çalıştır
- Benchmark: `func BenchmarkXxx(b *testing.B)` — `b.N`, `b.ResetTimer()`, `-benchmem`
- **testify/assert**: `assert.Equal`, `assert.NoError`, `assert.ErrorIs` — okunabilir assertion'lar
- **testify/require**: `require.NoError` — hata varsa testi hemen durdur (assert devam eder)
- **testify/mock**: struct mock, `On()`, `Return()`, `AssertExpectations()`
- **mockery**: interface'den otomatik mock kodu üretir — elle yazmak yerine generate et
- `mockery --name=KitapDeposu` — interface mock'u oluştur
- Test coverage: `go test -cover ./...` — coverage yüzdesi
- `go tool cover -html=coverage.out` — HTML coverage raporu
- `go test -coverprofile=coverage.out -covermode=atomic` — CI için coverage dosyası üret
- **golangci-lint**: Go'nun SonarQube / linting kombinasyonu — `golangci-lint run`
- `staticcheck`: ileri seviye statik analiz, golangci-lint içinde
- `go vet`: built-in basit statik analiz — her CI'da çalışmalı
- `.golangci.yml`: linter konfigürasyon dosyası — hangi linter'lar aktif, threshold'lar

**C# ile karşılaştırma:** xUnit + NSubstitute vs testing + testify + mockery, coverlet vs `go test -cover`, SonarQube vs golangci-lint + staticcheck

**Kontrol Soruları:**
1. `assert` vs `require` farkı ne? Ne zaman hangisi?
2. mockery neden elle mock yazmaktan daha iyi?
3. `go vet` ile `golangci-lint` arasındaki fark ne?

---

### Gün 13 — Functional Patterns ve Closure

**Teorik:**
- First-class function: Go'da fonksiyonlar değer olarak geçirilebilir
- Closure: dış scope'taki değişkeni capture eder — heap allocation sonucu
- Higher-order function: fonksiyon alan veya döndüren fonksiyon
- Method value vs method expression
- Functional options pattern — config için yaygın Go idiomu, C#'ta builder pattern yerine
- `defer` — fonksiyon çıkışında çalışır, LIFO sırasında — resource cleanup için

**C# ile karşılaştırma:** `Func<T>`, `Action<T>`, lambda vs Go function value, builder pattern vs functional options

**Kontrol Soruları:**
1. Functional options neden `new Server { Port = 9090 }` syntax'ından daha esnek?
2. `defer` LIFO sırasında çalışır — bunun pratik kullanımı ne?
3. Closure hangi senaryoda goroutine leak'e yol açar?

---

### Gün 14 — Hafta 2 Özet ve Go Ekosistemi

**Tekrar soruları:**
1. Implicit interface neden test için avantajlı?
2. `context.WithTimeout()` kullanılmış ama `defer cancel()` yazılmamış — ne olur?
3. Generic `[T comparable]` ile `[T any]` farkı?
4. `internal/` klasörü olmadan ne tür bir güvenlik sorunu çıkabilir?
5. mockery ile elle yazılmış mock arasındaki temel fark ne?

---

# FAZ 2 — REST API + PostgreSQL

> C# müfredatında Faz 2 MVC ile başlıyordu. Go'da MVC yok, framework seçimi zorunlu değil. Direkt `net/http` + PostgreSQL'den başlıyoruz. Bu Go'nun felsefesi: standart kütüphane yeterliyse onu kullan.

### Bu Fazda İnşa Edeceğimiz (`Faz2-REST-API/kitabevi-api/`)

| Aşama | Ne eklenir |
|-------|-----------|
| Başlangıç | `net/http`, chi router, JSON encode/decode |
| Validation | go-playground/validator, request doğrulama |
| DB Katmanı | PostgreSQL + pgx, sqlc, golang-migrate |
| Mimari | Repository, Service, Handler katman ayrımı |
| Güvenlik | JWT auth, middleware zinciri |
| Gözlemlenebilirlik | slog, health check |
| Test | httptest, testify/mock, testcontainers, integration test |
| Deploy | Docker, docker-compose |

---

## Hafta 3 — HTTP Temelleri ve Router

### Gün 15 — net/http: Go'nun Standart HTTP Kütüphanesi

**Teorik:**
- `http.Handler` interface: `ServeHTTP(ResponseWriter, *Request)` — tek metod, her şey burada
- `http.HandlerFunc` — fonksiyonu handler'a dönüştürür
- `http.ServeMux` — standart router, Go 1.22'de method+path pattern matching geldi
- `http.ResponseWriter` — response yaz, header set et
- `*http.Request` — URL, header, body, context
- Server konfigürasyonu: `ReadTimeout`, `WriteTimeout`, `IdleTimeout` — production'da zorunlu, neden?
- `http.ListenAndServe` vs `http.Server{}` — fark ne, hangisi kullanılmalı

**C# ile karşılaştırma:** Kestrel vs Go'nun built-in HTTP server, `WebApplication.Run()` vs `server.ListenAndServe()`

**Kontrol Soruları:**
1. `ReadTimeout` ve `WriteTimeout` neden production'da zorunlu?
2. `w.Header().Set()` neden `w.Write()`'dan önce çağrılmalı?
3. Go 1.22 öncesi `net/http`'nin method+path routing eksikliği nasıl çözülürdü?

---

### Gün 16 — Chi Router: Middleware Zinciri

**Teorik:**
- `chi` — lightweight router, Go standart `http.Handler` interface'ine uyar
- Middleware: handler'ı saran fonksiyon — `func(http.Handler) http.Handler`
- Middleware zinciri: `Use()` ile eklenir, sıra önemli
- Route grouping: prefix ile grupla, ayrı middleware zinciri
- URL parametresi: `chi.URLParam(r, "id")`
- Built-in middleware: `middleware.RequestID`, `middleware.RealIP`, `middleware.Logger`, `middleware.Recoverer`
- Custom middleware anatomy — `next.ServeHTTP()` nerede çağrılır, neden önemli
- Neden chi? Gin/Echo alternatiflerine kıyasla trade-off'lar

**C# ile karşılaştırma:** `app.Use()` middleware pipeline vs chi middleware chain

**Kontrol Soruları:**
1. `middleware.Recoverer` neden her production uygulamasında olmalı?
2. Middleware sırası neden önemli? Logger ve Auth yer değiştirirse ne olur?
3. Chi neden `net/http` ile uyumlu? Bunu sağlayan nedir?

---

### Gün 17 — JSON ve Input Validation

**Teorik:**
- `encoding/json` — standart kütüphane
- `json.NewEncoder(w).Encode(v)` — response'a yaz (stream, buffer'sız)
- `json.NewDecoder(r.Body).Decode(&v)` — request'ten oku (stream)
- Struct tag'ları: `` `json:"baslik,omitempty"` `` — `omitempty` ne zaman kullanılır
- `json.Decoder.DisallowUnknownFields()` — ekstra field'ları reddet, neden güvenlik açısından önemli
- **go-playground/validator/v10**: tag-based struct validation
- `validate:"required,min=1,max=200"` — built-in validator kuralları
- `validate:"gt=0,lte=10000"` — sayısal validation
- Custom validator fonksiyonu — `validate.RegisterValidation()`
- Cross-field validation — bir field'ın değeri başka field'a bağlı
- Validation hatasını API response'a dönüştürme — field bazlı hata mesajları

**C# ile karşılaştırma:** `DataAnnotations` + `ModelState.IsValid` vs go-playground/validator, FluentValidation vs custom validator

**Kontrol Soruları:**
1. `json.NewEncoder(w).Encode(v)` vs `json.Marshal(v)` + `w.Write()` — farkı ne?
2. `DisallowUnknownFields()` neden güvenlik açısından önemli?
3. Validation hatalarını tek field mi, tüm field'lar birden mi dönmeliyiz? Neden?

---

### Gün 18 — Handler Organizasyonu: Katmanlı Mimari Temeli

**Teorik:**
- Handler: sadece HTTP concerns — request parse, response yaz, hata çevir
- Handler iş mantığı içermemeli — service'e delege et
- Dependency injection: constructor ile — Go'da DI container genellikle gerekmez
- Domain hatasını HTTP status code'a çevirme — merkezi `domainHataYaz()` fonksiyonu
- RFC 7807 Problem Details — standart hata yanıtı formatı
- Handler test edilebilirliği: interface inject et → test'te mock geç
- `http.Handler` interface ile endpoint gruplandırma
- Object mapping: DB row / sqlc struct → domain model → response DTO — her dönüşüm nerede yapılır
- `mapstructure` paketi — map/struct arası dönüşüm, AutoMapper benzeri ama hafif
- Manuel mapping fonksiyonu tercih edilmeli: `toKitapResponse(k domain.Kitap) KitapResponse` — açık, tip-güvenli, sıfır bağımlılık

**C# ile karşılaştırma:** AutoMapper profil vs Go manuel mapping fonksiyonu — Go'da reflection tabanlı mapper neden tercih edilmez

**Kontrol Soruları:**
1. Handler neden iş mantığı içermemeli?
2. `domainHataYaz()` merkezi fonksiyonu neden önemli?
3. Interface inject etmek neden test için avantajlı?

---

## Hafta 4 — PostgreSQL: database/sql ve sqlc

### Gün 19 — database/sql ve pgx: Bağlantı Havuzu

**Teorik:**
- `database/sql` — standart DB interface, driver agnostik
- `pgx/v5` — PostgreSQL driver + stdlib adapter
- Side-effect import: `_ "github.com/jackc/pgx/v5/stdlib"` — driver kaydı
- Connection pool: `sql.DB` zaten pool — `MaxOpenConns`, `MaxIdleConns`, `ConnMaxLifetime`, `ConnMaxIdleTime`
- `sql.ErrNoRows` — kayıt bulunamadı sentinel, domain hatasına çevirme
- Parametreli sorgu: `$1`, `$2` — SQL injection önleme
- `QueryRowContext`, `QueryContext`, `ExecContext` — farkları ve ne zaman hangisi
- `Scan()` — satırı Go değişkenlerine çek, kolon sırası önemli
- `pgxpool` — pgx'in native pool'u, daha iyi performans, ne zaman tercih edilir

**C# ile karşılaştırma:** EF Core ORM soyutlaması vs raw SQL + type-safe wrapper, implicit N+1 riski karşılaştırması

**Kontrol Soruları:**
1. `MaxOpenConns` ve `MaxIdleConns` neden eşit ayarlanmalı?
2. `_ "github.com/jackc/pgx/v5/stdlib"` satırındaki `_` ne anlama geliyor?
3. EF Core'un implicit N+1 riski Go'da neden daha az?

---

### Gün 20 — sqlc: Type-Safe Query Generation

**Teorik:**
- sqlc: SQL yaz → Go kodu üretir — runtime reflection yok, compile-time type-safe
- `sqlc.yaml` — proje konfigürasyonu: engine, queries path, schema path, output package
- Sorgu annotation'ları: `-- name: GetKitap :one`, `:many`, `:exec`, `:execresult`
- Üretilen kod: `Queries` struct + `DBTX` interface
- `DBTX` interface: `*sql.DB` ve `*sql.Tx` her ikisini de kabul eder → transaction şeffaf
- sqlc ile JOIN: flat struct veya nullable field mapping
- `sqlc generate` — SQL değişince kodu yeniden üret, derleme hatası ile senkron kalırsın

**C# ile karşılaştırma:** EF Core LINQ → SQL (runtime) vs sqlc SQL → Go (compile-time), Dapper ile benzerlik

**Kontrol Soruları:**
1. sqlc neden EF Core'dan daha öngörülebilir?
2. `DBTX` interface transaction'ı neden şeffaf yapar?
3. `:one` vs `:many` vs `:exec` — hangisi ne zaman?

---

### Gün 21 — golang-migrate: Migration Yönetimi

**Teorik:**
- `golang-migrate` — CLI + Go kütüphanesi, DB migration
- Migration dosyaları: `000001_olustur_kitaplar.up.sql` + `000001_olustur_kitaplar.down.sql`
- Up: değişikliği uygula — Down: geri al
- Migration version: sıralı numara, boşluk bırakma
- `//go:embed` ile migration dosyalarını binary'e göm
- Programatik migration — startup'ta mı, CI/CD'de mi? Trade-off'lar
- `migrate.ErrNoChange` — migration yok, hata değil

**C# ile karşılaştırma:** `dotnet ef migrations add` (code-first) vs golang-migrate (SQL-first) — avantaj/dezavantaj

**Kontrol Soruları:**
1. Her migration dosyasının `.down.sql` versiyonu neden gerekli?
2. EF Core'da `dotnet ef migrations add` ile Go'nun farkı?
3. Migration startup'ta programatik mı çalıştırılmalı, CI/CD'de mi?

---

## Hafta 5 — Mimari Katmanlar

### Gün 22 — Repository Pattern: Interface Tabanlı

**Teorik:**
- Repository: veri erişimini soyutlar — service katmanı DB'yi bilmez
- Interface domain katmanında tanımlanır, implementasyon repository katmanında
- Küçük interface: `KitapOkuyucu` + `KitapYazici` ayrımı
- Mock repository: test için interface'i implement et — framework gerekmez
- Elle yazılmış mock vs mockery ile generate edilmiş mock
- `testify/mock` ile mock beklentileri — `On()`, `Return()`, `AssertExpectations()`

**C# ile karşılaştırma:** `IRepository<T>` generic repository antipattern vs Go'da domain-specific interface

**Kontrol Soruları:**
1. Generic repository (`IRepository<T>`) neden antipattern sayılır?
2. Interface domain katmanında neden tanımlanır, repository katmanında değil?
3. `AssertExpectations()` neden her test sonunda çağrılmalı?

---

### Gün 23 — Transaction Yönetimi

**Teorik:**
- `db.BeginTx()` — transaction başlat
- `tx.Commit()` ve `tx.Rollback()` — commit veya geri al
- `defer tx.Rollback()` pattern — hata durumunda otomatik rollback, Commit sonrası no-op
- sqlc ile transaction: `queries.WithTx(tx)` — DBTX interface sayesinde şeffaf
- Transaction'ı hangi katmanda yönet: service katmanı, repository değil — neden
- İç içe transaction: savepoint kavramı, Go'da nasıl yaklaşılır

**C# ile karşılaştırma:** EF Core `_context.Database.BeginTransactionAsync()` vs Go manuel transaction

**Kontrol Soruları:**
1. `defer tx.Rollback()` neden `tx.Commit()` başarılıysa da güvenli?
2. `DBTX` interface transaction'ı neden şeffaf yapar?
3. Transaction service katmanında mı, repository'de mi yönetilmeli? Neden?

---

## Hafta 6 — N+1, Auth ve Configuration

### Gün 24 — N+1 Problemi ve Query Optimizasyonu

**Teorik:**
- N+1: her kitap için ayrı yazar sorgusu — EF Core'da implicit (lazy loading), Go'da explicit
- Çözüm 1: JOIN sorgusu — tek sorguda birleştir, sqlc ile flat struct mapping
- Çözüm 2: IN query — `= ANY($1)` ile pgx array desteği, batch yükleme
- Çözüm 3: DataLoader pattern — batch + cache, GraphQL'de yaygın
- EXPLAIN ANALYZE: sorgu planını oku, index kullanımını kontrol et

**C# ile karşılaştırma:** EF Core `Include()` eager loading vs Go explicit JOIN, lazy loading tuzağı

**Kontrol Soruları:**
1. Neden EF Core `Include()` Go'da karşılık bulamaz?
2. `= ANY($1)` vs `IN (...)` — PostgreSQL'de farkı ne?
3. DataLoader ne zaman JOIN'den daha iyi?

---

### Gün 25 — JWT Authentication Middleware

**Teorik:**
- JWT: Header.Payload.Signature — stateless auth, neden stateless tercih edilir
- `golang-jwt/jwt` paketi — token üretme ve doğrulama
- `jwt.NewWithClaims()` + `token.SignedString(secret)` — token imzalama
- `jwt.ParseWithClaims()` — token doğrulama, imza algoritması kontrolü
- Custom claims struct: `jwt.RegisteredClaims` embed et
- Middleware: Authorization header parse → context'e kullanıcı koy → handler okur
- Access token (kısa TTL) + refresh token (uzun TTL) stratejisi
- Token refresh endpoint'i — güvenli rotasyon

**C# ile karşılaştırma:** `AddJwtBearer()` middleware vs Go manuel JWT middleware, `ClaimsPrincipal` vs custom claims struct

**Kontrol Soruları:**
1. Algoritma kontrolü (`jwt.SigningMethodHMAC`) neden yapılmalı?
2. Access token neden kısa ömürlü olmalı?
3. Context'e kullanıcı koymak neden cookie/global değişkenden daha iyi?

---

### Gün 26 — Configuration ve Environment

**Teorik:**
- 12-Factor App: config environment variable'lardan gelir
- `os.Getenv()` — basit, validation yok
- `kelseyhightower/envconfig` — struct tag ile env mapping, required/default desteği
- `spf13/viper` — YAML/JSON/env/flag hepsi bir arada, büyük projeler için
- `godotenv` — `.env` dosyasını yükle, sadece development
- Config struct: tüm ayarları tek yerde topla, alt struct'larla grupla
- Secret yönetimi: env var vs HashiCorp Vault vs AWS Secrets Manager — ne zaman hangisi

**C# ile karşılaştırma:** `appsettings.json` + `IOptions<T>` vs Go envconfig/viper, configuration providers hiyerarşisi

**Kontrol Soruları:**
1. Neden config struct'ta required tag önemli? Runtime'da mı, startup'ta mı hata vermeli?
2. viper ne zaman envconfig'den daha uygun?
3. Secret'ları env var olarak yönetmenin sınırı nerede?

---

### Gün 27 — Structured Logging: slog

**Teorik:**
- Go 1.21+ ile gelen `log/slog` — standart structured logging
- JSON handler vs text handler — ne zaman hangisi
- Log level: Debug, Info, Warn, Error — level filtreleme
- Attribute'lar: key-value çiftleri — `slog.String()`, `slog.Int()`, `slog.Any()`
- `slog.With()` — context'e attribute ekle, tüm log'lara yansır
- Logger'ı context'e göm — request ID, user ID ile zenginleştirme
- `zerolog` alternatifi — allocation-free, benchmark karşılaştırması
- Production'da log level dinamik değiştirme — `slog.SetLogLoggerLevel()`

**C# ile karşılaştırma:** Serilog vs slog — enrichment, sink kavramları, structured logging felsefesi

**Kontrol Soruları:**
1. Neden `fmt.Println` yerine structured logging?
2. `slog.With()` ne zaman kullanılır? Request middleware'de nasıl uygulanır?
3. zerolog neden daha performanslı? Bu fark ne zaman önemli?

---

## Hafta 7 — Testing

### Gün 28 — HTTP Testing: httptest ve testify/mock

**Teorik:**
- `net/http/httptest` — HTTP handler'ı gerçek server olmadan test et
- `httptest.NewRecorder()` — response'u yakala: status code, body, header
- `httptest.NewRequest()` — sahte request oluştur
- chi router ile handler test: `chi.URLParam` çalışması için router kurulumu
- `testify/mock` ile service mock'u: `On()`, `Return()`, `Maybe()`, `Times()`
- mockery ile generate edilmiş mock'u kullanmak
- `AssertExpectations(t)` — beklenmeyen çağrı var mı, beklenen çağrı yapılmadı mı
- JSON body parse: `json.NewDecoder(w.Body).Decode(&result)`

**C# ile karşılaştırma:** `WebApplicationFactory` + `HttpClient` vs `httptest.NewRecorder()`, Moq vs testify/mock

**Kontrol Soruları:**
1. `httptest.NewRecorder()` neden gerçek server'dan daha iyi unit test için?
2. `AssertExpectations()` ne yakalar? Ne zaman false positive verebilir?
3. Chi router kurulumu neden test'te de gerekli?

---

### Gün 29 — Integration Testing: testcontainers-go

**Teorik:**
- testcontainers-go: test içinde Docker container başlat — gerçek PostgreSQL
- Gerçek DB ile test: mock DB'nin saklamadığı hataları yakala (constraint, index, transaction)
- `TestMain` — test suite başlamadan container ayağa kaldır, `m.Run()` sonra temizle
- `wait.ForListeningPort()` — container hazır olana kadar bekle
- Paralel test: her test aynı DB'yi kullanabilir mi? Schema isolation stratejisi
- Build tag: `//go:build integration` — unit test ile integration test ayırımı
- `go test -tags=integration ./...`

**C# ile karşılaştırma:** `Testcontainers` NuGet paketi — aynı konsept, Go versiyonu

**Kontrol Soruları:**
1. Mock repository yerine gerçek DB ile test — trade-off nedir?
2. `//go:build integration` neden gerekli? CI'da nasıl yönetilir?
3. Test veritabanı isolation için ne yapılabilir?

---

## Hafta 8 — Background Workers ve Docker

### Gün 30 — Graceful Shutdown ve Background Workers

**Teorik:**
- OS signal yakalama: `signal.NotifyContext()` — SIGINT/SIGTERM
- `server.Shutdown(ctx)` — aktif request'leri bitir, yenileri reddet
- Background goroutine'leri kapatmak — context propagation ile
- Worker pool: sabit sayıda goroutine, iş kuyruğu channel
- `sync.WaitGroup` ile worker'ların bitmesini bekle
- `PeriodicTimer` alternatifi — `time.Ticker` ile periyodik iş
- Goroutine leak detection — goroutine sayısını monitor et

**C# ile karşılaştırma:** `IHostedService` + `StoppingToken` vs Go goroutine + signal context

**Kontrol Soruları:**
1. Graceful shutdown olmadan container restart'ta ne olur?
2. Worker pool neden sabit sayıda goroutine kullanır, her iş için yeni goroutine değil?
3. `signal.NotifyContext()` neden `signal.Notify()` + channel'dan daha iyi?

---

### Gün 31 — Docker: Go Binary Containerization

**Teorik:**
- Go'nun en büyük avantajı: tek statik binary → minimal Docker image
- Multi-stage build: builder stage + runtime stage
- `CGO_ENABLED=0` — statik binary, neden gerekli
- `-ldflags="-s -w"` — binary küçültme: sembol tablosu ve debug bilgisi sil
- `FROM scratch` vs `FROM alpine` — trade-off: boyut vs debugging
- Non-root user: güvenlik için `USER` directive
- `COPY --from=builder` — sadece binary ve gerekli dosyalar
- docker-compose.yml: service, depends_on, healthcheck, volume
- Health check endpoint: `/health` — liveness + readiness ayrımı

**C# ile karşılaştırma:** .NET SDK image (büyük) + runtime image vs Go scratch image

**Kontrol Soruları:**
1. `CGO_ENABLED=0` neden statik binary için gerekli?
2. `FROM scratch` vs `FROM alpine` trade-off nedir?
3. Multi-stage build olmadan Dockerfile boyutu ne kadar büyür?

---

### Gün 32 — OpenAPI: Dokümantasyon

**Teorik:**
- `swaggo/swag` — comment'lerden OpenAPI spec üretir
- Handler comment annotation'ları: `// @Summary`, `// @Param`, `// @Success`, `// @Failure`
- `swag init` — spec üretir, `docs/` klasörüne yazar
- `swaggo/http-swagger` — Swagger UI serve eder
- `@Security BearerAuth` — JWT korumalı endpoint'leri işaretle
- Alternatif: `huma` framework — code-first OpenAPI, tip-güvenli
- Manuel `openapi.yaml` yazma — ne zaman code-gen'den daha iyi

**C# ile karşılaştırma:** Swashbuckle + XML comment vs swaggo/swag annotation

**Kontrol Soruları:**
1. `swag init` ne zaman çalıştırılmalı? CI'da otomatik mi?
2. Swagger UI production'da açık olmalı mı?
3. Code-first vs spec-first API dokümantasyon — avantaj/dezavantaj?

---

### Gün 33 — Redis: Distributed Cache

**Teorik:**
- Redis kullanım senaryoları: cache, session store, rate limit counter, pub/sub, distributed lock
- `go-redis/redis/v9` — Go Redis client, en yaygın
- `Set()`, `Get()`, `Del()`, `Expire()` — temel operasyonlar
- Cache-aside pattern: önce cache bak, miss varsa DB'den al, cache'e yaz
- TTL stratejisi: ne kadar süre? Stale data riski
- Cache invalidation: ne zaman, nasıl? En zor problem
- `SET ... NX` — distributed lock temeli (SETNX)
- `StackExchange.Redis` (C#) vs `go-redis` karşılaştırması
- In-memory map vs Redis: ne zaman Redis gerekli (multi-instance, restart)
- `redisearch`, pipeline, pipelining — performans için batch komutları
- `redis.Nil` — cache miss sentinel, `sql.ErrNoRows` benzeri

**C# ile karşılaştırma:** `IDistributedCache` + `StackExchange.Redis` vs `go-redis`, Redis serialization farkları

**Kontrol Soruları:**
1. Cache-aside ile write-through farkı ne? Ne zaman hangisi?
2. Cache invalidation ne zaman yapılır? Proaktif mi, reactive mi?
3. Redis olmadan distributed lock nasıl yapılır? Neden zordur?

---

### Gün 34 — Rate Limiting ve Health Check

**Teorik:**
- **Rate Limiting:**
  - `golang.org/x/time/rate` — token bucket algoritması, built-in
  - `Limiter.Allow()`, `Limiter.Wait()`, `Limiter.Reserve()` — farkları
  - IP başına rate limit: middleware'de client IP'ye göre ayrı `Limiter`
  - `sync.Map` ile per-IP limiter store
  - Distributed rate limiting: Redis ile — tek instance yeterli değil
  - Throttle vs Rate limit farkı
  - `429 Too Many Requests` + `Retry-After` header
- **Health Check:**
  - Liveness vs Readiness — farkı ve neden ikisi gerekli
  - `/health` (liveness): uygulama ayakta mı? Basit `200 OK`
  - `/ready` (readiness): DB, Redis bağlantısı hazır mı? Detaylı kontrol
  - `alexliesenfeld/health` paketi — health check framework
  - Kubernetes probe'ları ile entegrasyon: `livenessProbe`, `readinessProbe`
  - DB ping health check: `db.PingContext(ctx)` ile timeout

**C# ile karşılaştırma:** `AspNetCore.HealthChecks.*` vs `alexliesenfeld/health`, ASP.NET Core Rate Limiting built-in vs `golang.org/x/time/rate`

**Kontrol Soruları:**
1. Liveness ve readiness probe arasındaki fark ne? Kubernetes'te ne işe yarar?
2. Per-IP rate limiting neden `sync.Map` gerektirir?
3. Distributed rate limiting neden tek instance Redis gerektirir?

---

### Gün 35 — OAuth2 ve OpenID Connect

**Teorik:**
- OAuth2: yetkilendirme protokolü — "X uygulamasına Y kaynağına erişim ver"
- OpenID Connect (OIDC): OAuth2 üzerine kimlik doğrulama katmanı
- JWT vs opaque token — OIDC'de ID token JWT'dir
- Authorization Code Flow: web app için standart — PKCE ile
- Client Credentials Flow: servis-servis auth için, kullanıcı yok
- `golang.org/x/oauth2` — OAuth2 client, token yönetimi
- `coreos/go-oidc/v3` — OIDC provider discovery, token doğrulama
- Keycloak / Auth0 entegrasyonu: provider olarak Go app
- Resource Server: Go API, token doğrular ama üretmez
- JWT doğrulama: OIDC provider JWKS endpoint'inden public key al, imzayı doğrula
- Refresh token rotasyonu: access token süresi dolunca
- Go'da custom OAuth2 server: `OpenIddict` benzeri (`fosite` paketi)

**C# ile karşılaştırma:** ASP.NET Core Identity + OpenIddict vs Go `golang.org/x/oauth2` + `go-oidc`, `AddOpenIdConnect()` vs manuel OIDC flow

**Kontrol Soruları:**
1. JWT auth (Gün 25) ile OIDC farkı ne? Ne zaman OIDC gerekli?
2. Client Credentials Flow ne zaman kullanılır? Authorization Code Flow'dan farkı?
3. JWKS endpoint neden token doğrulamada önemli?

---

### Gün 36 — Multi-Tenant Mimari

**Teorik:**
- Multi-tenancy: tek uygulama, birden fazla müşteri (tenant) — veriler birbirinden izole
- Tenant isolation stratejileri:
  - Database per tenant: tam izolasyon, yönetimi zor
  - Schema per tenant: PostgreSQL schema ayrımı
  - Row-level (shared DB): `tenant_id` kolonu — en yaygın, en ucuz
- Tenant tespiti: header (`X-Tenant-ID`), subdomain, JWT claim — ne zaman hangisi
- Middleware ile tenant context'e alma: `context.WithValue(ctx, ctxKeyTenant, tenantID)`
- Row-level: her sorguda `WHERE tenant_id = $1` — unutursan veri sızıntısı
- `SET app.current_tenant = $1` + PostgreSQL Row Level Security (RLS): DB seviyesinde güvenlik
- sqlc ile multi-tenant query: her sorguya tenant_id parametresi
- Tenant registration ve provisioning akışı

**C# ile karşılaştırma:** Header bazlı tenant çözümleme — Go middleware ile C# middleware aynı konsept, `IHttpContextAccessor` vs context.Context

**Kontrol Soruları:**
1. Row-level multi-tenancy neden `WHERE tenant_id` unutulursa tehlikeli? RLS nasıl önler?
2. Subdomain vs header ile tenant tespiti — avantaj/dezavantaj?
3. Database per tenant ne zaman row-level'dan daha iyi?

---

### Gün 37 — CI/CD: GitHub Actions, Kod Kalitesi ve Yük Testi

**Teorik:**
- **GitHub Actions ile Go CI/CD:**
  - Workflow: `on: [push, pull_request]` → trigger
  - Job: `go test`, `golangci-lint`, `go build`, Docker build + push
  - Go setup: `actions/setup-go@v5` — Go versiyonu pin et
  - Cache: `actions/cache` — Go module cache, lint cache
  - Matrix build: birden fazla Go versiyonu test
  - Secrets: `${{ secrets.REGISTRY_TOKEN }}` — credential yönetimi
  - `codecov/codecov-action` — coverage raporu upload
- **Kod Kalitesi:**
  - `golangci-lint` CI'da: `golangci-lint run --timeout=5m`
  - `go vet ./...` — her CI'da zorunlu
  - `staticcheck ./...` — golangci-lint dışında da çalıştırılabilir
  - Pre-commit hook: `golangci-lint` commit öncesi çalıştır
  - SonarQube entegrasyonu: `sonar-scanner` ile Go coverage + lint raporu gönder
- **Yük Testi (k6):**
  - k6: JavaScript ile yazılan yük testi aracı — Go API'yi test eder
  - `vus`: virtual user sayısı, `duration`: test süresi
  - Threshold: `http_req_duration{p95}<200ms` — SLA kontrolü
  - Smoke test, load test, stress test, spike test — farkları
  - k6 Cloud vs local — CI'da entegrasyon
  - Grafana k6 dashboard — sonuçları görselleştir

**C# ile karşılaştırma:** GitHub Actions workflow aynı, k6 language-agnostic — C# veya Go API'sini aynı şekilde test eder

**Kontrol Soruları:**
1. CI'da `golangci-lint` neden `go vet`'ten daha kapsamlı?
2. k6 threshold neden `p95` kullanır? `average` neden yanıltıcı?
3. Coverage %80 hedefi ne anlama gelir? Hangi threshold mantıklı?

---

# FAZ 3 — Mimari

> Faz 2'de çalışan bir Kitabevi API'si var. Faz 3'te bunu ölçeklenebilir mimariyle yeniden düşünüyoruz. Aynı C# müfredatındaki SOLID → Design Patterns → Clean Architecture yolculuğu, ama Go idiomlarıyla.

---

## Hafta 10 — SOLID: Go Perspektifi

### Gün 38 — SRP ve OCP: Tek Sorumluluk ve Açık/Kapalı

**Teorik:**
- SRP: Her struct/fonksiyon tek şeyi değişme nedeni olan — package level'da da geçerli
- SRP ihlali: handler'da DB çağrısı + validation + email gönderme
- OCP: Yeni davranış eklerken var olanı değiştirme — interface ile
- `Bildirimci` interface: `EmailBildirimci`, `SMSBildirimci` — yeni tip, mevcut kod değişmez
- Composite pattern ile çoklu bildirim — `CokluBildirimci`
- Go'da OCP: yeni tip ekle, mevcut kodu değiştirme — switch/case antipattern

**C# ile karşılaştırma:** Faz 2 KitabeviMVC'de SRP ihlalleri vs Faz 3 ayrıştırması

**Kontrol Soruları:**
1. Bir fonksiyon kaç satırdan uzun olunca SRP ihlali başlar? (Subjektif ama tartış)
2. `switch` ile OCP'yi nasıl ihlal edersin? Interface ile nasıl düzelirsin?
3. Composite bildirimci neden OCP'yi sağlar?

---

### Gün 39 — LSP, ISP, DIP: Interface Disiplini

**Teorik:**
- LSP: Liskov Substitution — Go'da implicit interface sayesinde kolayca ihlal edilir, test ile yakala
- ISP: Interface Segregation — Go'nun doğasında var, büyük interface antipattern
- `io.Reader`, `io.Writer` — standart kütüphanenin ISP örneği
- DIP: Dependency Inversion — domain interface tanımlar, infrastructure implement eder
- Manuel DI wiring — `cmd/api/main.go`'da tüm bağımlılıkları kurma (composition root)
- Uber Fx / Wire — ne zaman gerekli, ne zaman overengineering

**C# ile karşılaştırma:** `IServiceCollection` + DI container vs Go manuel wiring, `[Inject]` vs constructor

**Kontrol Soruları:**
1. Go'da LSP ihlali nasıl ortaya çıkar? Interface satisfaction nasıl yanıltır?
2. Büyük interface neden ISP ihlalidir? Bunu ikiye bölünce ne kazanırsın?
3. Manuel DI ne zaman yeterli, DI framework ne zaman gerekli?

---

## Hafta 11 — Design Patterns: Go Versiyonları

### Gün 40 — Creational ve Structural Patterns

**Teorik:**
- Factory function: Go'da `New*()` convention — `NewKitapHandler()`, `NewKitapRepo()`
- Functional options pattern — Creational'ın Go idiomu (Gün 13'te gördük, mimari bağlamda)
- Decorator pattern: middleware olarak — `LoggingRepo`, `CachingRepo`
- Adapter pattern: dış servis interface'ini domain interface'ine çevir
- Proxy pattern: rate limiting, circuit breaker wrapper olarak
- Singleton: package-level `var` ile — neden dikkatli kullanılmalı, test zorluğu

**C# ile karşılaştırma:** GoF pattern'ler kalıtım olmadan nasıl uygulanır

**Kontrol Soruları:**
1. Decorator pattern middleware olarak nasıl çalışır? `LoggingRepo` örneği.
2. Singleton neden test edilmesi zor? Alternatifi ne?
3. Adapter pattern ne zaman gerekli? Dış API entegrasyonunda nasıl kullanılır?

---

### Gün 41 — Behavioral Patterns: Observer ve Strategy

**Teorik:**
- Observer pattern: channel ile — `OlayYayincisi`, goroutine'ler dinler
- Non-blocking publish: `select` + `default` — yavaş abone sistemi tıkamaz
- Strategy pattern: interface ile — `SiralamaStratejisi`, `FiyatHesaplayici`
- Command pattern: CQRS'in temeli — `KitapOlusturKomutu`, handler
- Pipeline pattern: channel zinciri — her stage bir goroutine
- Go'da hangi GoF pattern'ler gereksizdir? (kalıtım olmadığı için)

**C# ile karşılaştırma:** `event` keyword vs channel observer, delegate vs interface strategy

**Kontrol Soruları:**
1. Channel-based observer neden thread-safe? Mutex'e gerek var mı?
2. Strategy pattern ne zaman `if-else` zincirine tercih edilir?
3. Hangi GoF pattern'ler Go'da daha az anlam taşır, neden?

---

## Hafta 12 — Clean Architecture ve CQRS

### Gün 42 — Clean/Hexagonal Architecture

**Teorik:**
- Dependency rule: içten dışa bağımlılık — domain hiçbir şeyi import etmez
- Katmanlar: domain → application/service → infrastructure → interface (handler)
- Ports (interface'ler) — domain içinde tanımlanır
- Adapters (implementasyonlar) — dışarıda, infrastructure katmanında
- Go'da circular import bu kuralı doğal olarak zorlar
- Package yapısı: `internal/domain`, `internal/service`, `internal/handler`, `internal/repository`
- Anti-corruption layer: dış servis modelini domain modeline çevir

**C# ile karşılaştırma:** Onion Architecture (Faz 3 C# müfredatı) vs Go Clean Arch — aynı konsept, farklı araçlar

**Kontrol Soruları:**
1. Domain katmanı neden hiçbir dış paketi import edemez?
2. Port ve Adapter ne anlama gelir? Somut örnek ver.
3. Go circular import kuralı Clean Arch'ı nasıl zorlar?

---

### Gün 43 — CQRS: Mediator Olmadan Explicit Handler

**Teorik:**
- CQRS: Command (yaz) ve Query (oku) ayrı modeller
- C#'ta MediatR ile yapılır — Go'da explicit handler, framework yok
- Command struct + CommandHandler: tek sorumluluk
- Query struct + QueryHandler: okuma modeli, domain modelinden farklı olabilir
- Read model: query için optimize edilmiş, flat DTO — JOIN sorgusu
- Write model: domain entity, iş kurallarını taşır
- Event: command başarıyla tamamlanınca — async bildirim
- Neden MediatR gibi mediator Go'da gerekmez?

**C# ile karşılaştırma:** MediatR + `IRequest<T>` vs Go explicit handler çağrısı — trade-off

**Kontrol Soruları:**
1. CQRS neden her projede uygulanmamalı? Ne zaman gerekli?
2. Read model ile write model neden farklı olabilir?
3. Go'da mediator olmadan CQRS'in dezavantajı ne?

---

# FAZ 4 — Performans

---

## Hafta 14 — Profiling

### Gün 44 — pprof: Go Performans Analizi

**Teorik:**
- `net/http/pprof` — HTTP üzerinden profil endpoint'i, side-effect import
- CPU profili: hangi fonksiyon ne kadar CPU kullanıyor
- Heap profili: allocation'lar nerede, ne kadar bellek tutuluyor
- Goroutine profili: kaç goroutine var, nerede takılmış — leak tespiti
- Block/mutex profili: nerede bekleniyor
- `go tool pprof` — interaktif analiz: `top`, `list`, `web` komutları
- Flame graph: görsel CPU/heap analizi
- `go tool trace` — scheduler, GC, goroutine hayat döngüsü detaylı görünüm
- Benchmark ile profil: `go test -bench=. -cpuprofile=cpu.prof -memprofile=mem.prof`
- Production'da pprof: güvenlik riski, ayrı port, sadece internal erişim

**C# ile karşılaştırma:** dotTrace / PerfView vs pprof, ETW vs Go trace

**Kontrol Soruları:**
1. CPU profili ile heap profili arasındaki fark ne? Ne zaman hangisi?
2. Goroutine profili nasıl leak tespitinde kullanılır?
3. Production'da pprof neden dikkatli kullanılmalı?

---

## Hafta 15 — Memory ve DB Optimizasyonu

### Gün 45 — Memory Optimizasyonu: sync.Pool ve Allocation Azaltma

**Teorik:**
- GC baskısı: çok allocation → sık GC → latency spike
- `sync.Pool`: nesne yeniden kullan, allocation azalt — `bytes.Buffer`, JSON encoder
- `Pool.Get()` + `Pool.Put()` + `Pool.New` — doğru kullanım
- Slice pre-allocation: `make([]T, 0, kapasite)` — neden reallocation maliyetli
- Map pre-allocation: `make(map[K]V, kapasite)`
- `strings.Builder` vs string concatenation — neden Builder daha iyi
- `go build -gcflags="-m"` ile allocation'ları bul
- `-benchmem` ile benchmark'ta allocation ölç: `allocs/op`, `B/op`

**C# ile karşılaştırma:** `ArrayPool<T>`, `MemoryPool<T>` vs `sync.Pool`, `Span<T>` vs slice operations

**Kontrol Soruları:**
1. `sync.Pool` neden GC baskısını azaltır? Nesne garantili geri gelir mi?
2. Slice'a kapasite vermemek neden N reallocation'a yol açar?
3. `-benchmem` çıktısında `allocs/op` neden önemli?

---

### Gün 46 — DB Performance Tuning

**Teorik:**
- pgx batch: `pgx.Batch` — tek round-trip'te birden fazla sorgu
- Prepared statement: `db.PrepareContext()` — query parsing overhead'ini azalt
- `EXPLAIN ANALYZE` okuma: Seq Scan vs Index Scan, cost tahmini vs gerçek
- Index stratejisi: composite index sırası, partial index, covering index
- `pgxpool` ile connection reuse — her request'te yeni bağlantı kurma
- `COPY` protokolü: bulk insert için en hızlı yöntem
- Query timeout: context ile per-query timeout

**C# ile karşılaştırma:** EF Core compiled queries vs pgx prepared statement, EF Core batch vs pgx batch

**Kontrol Soruları:**
1. Prepared statement neden tekrarlı sorgularda daha hızlı?
2. `EXPLAIN ANALYZE` çıktısında neye bakmalısın?
3. Bulk insert için neden normal INSERT yerine COPY?

---

## Hafta 16 — Observability

### Gün 47 — OpenTelemetry ve Prometheus: Gözlemlenebilirlik

**Teorik:**
- Observability üçlüsü: Logs + Metrics + Traces — her birinin rolü
- **OpenTelemetry (OTel)**: vendor-neutral, traces ve metrics için standart
  - `go.opentelemetry.io/otel` — Go SDK
  - Trace: istek hayat döngüsü, span'lar arası ilişki
  - `tracer.Start(ctx, "span-adı")` — span oluşturma
  - Propagation: `traceparent` header ile servisler arası trace
  - Exporter: Jaeger, Zipkin, OTLP — nereye gönderilir
- **Prometheus**: pull-based metrics collection
  - `prometheus/client_golang` — Go client
  - Counter, Gauge, Histogram, Summary — ne zaman hangisi
  - HTTP request duration histogram — latency percentile (p50, p95, p99)
  - `/metrics` endpoint — Prometheus scrape eder
  - Grafana ile dashboard — alert kuralları
- Slog ile trace ID: her log satırına trace ID ekle — log-trace korelasyonu
- Middleware'de OTel: her HTTP request için otomatik span + metric

**C# ile karşılaştırma:** OpenTelemetry .NET SDK vs Go SDK, Application Insights vs Prometheus + Grafana

**Kontrol Soruları:**
1. Trace, metric ve log'un farkı ne? Hangisi hangi soruyu cevaplar?
2. Histogram neden latency ölçümü için Counter'dan daha iyi?
3. `traceparent` header neden servisler arası trace için gerekli?

---

# FAZ 5 — Mikroservisler

---

## Hafta 18 — gRPC ve Servisler Arası İletişim

### Gün 48 — gRPC: Protocol Buffers ve Go

**Teorik:**
- gRPC: Google'ın RPC framework'ü — HTTP/2, Protocol Buffers binary serialization
- REST vs gRPC: JSON vs binary, HTTP/1.1 vs HTTP/2 — ne zaman hangisi
- `.proto` dosyası — schema tanımı, `protoc` + `protoc-gen-go` ile Go kodu üret
- Streaming türleri: unary, server-streaming, client-streaming, bidirectional
- `UnimplementedXxxServer` embed: forward compatibility için zorunlu
- gRPC status codes: `codes.NotFound`, `codes.Internal` — HTTP status kod eşlemesi
- Interceptor: gRPC'de middleware — logging, auth, recovery
- `grpc-gateway`: gRPC endpoint'i REST olarak da sun

**C# ile karşılaştırma:** `Grpc.AspNetCore` vs Go gRPC, protobuf code generation farkları

**Kontrol Soruları:**
1. gRPC ne zaman REST'ten daha uygun? Kullanıcıya açık API için neden genellikle REST?
2. Streaming ne zaman unary'den daha iyi?
3. Interceptor ile middleware aynı şey mi? Farkı ne?

---

### Gün 49 — NATS: Async Mesajlaşma

**Teorik:**
- NATS: hafif, hızlı, Go ile yazılmış message broker
- Publish/Subscribe: yayıncı ve abone — loose coupling
- JetStream: kalıcı mesajlar, at-least-once delivery, consumer group
- `Ack()` ve `Nak()` — mesaj onaylama ve reddetme, retry mekanizması
- Durable consumer: restart sonrası kaldığı yerden devam et
- NATS vs RabbitMQ vs Kafka — trade-off ve kullanım senaryoları
- Dead letter queue: başarısız mesajları ayrı subject'e yönlendir
- Outbox pattern: DB transaction + mesaj yayınlama atomik olarak nasıl yapılır

**C# ile karşılaştırma:** RabbitMQ (MassTransit) vs NATS JetStream, `IHostedService` consumer vs goroutine subscriber

**Kontrol Soruları:**
1. At-least-once delivery neden exactly-once'tan daha yaygın? Idempotency nasıl sağlanır?
2. Outbox pattern neden gerekli? Olmadan ne olur?
3. NATS JetStream ne zaman Kafka'ya tercih edilir?

---

### Gün 50 — Saga Pattern: Distributed Transaction

**Teorik:**
- Saga: mikroservislerde distributed transaction — iki faz commit (2PC) yerine
- Choreography Saga: her servis event yayar, diğerleri dinler — merkezi koordinatör yok
- Orchestration Saga: merkezi koordinatör (saga orchestrator) her adımı yönetir
- Compensating transaction: başarısız adımda önceki adımları geri al
- Kitabevi örneği: Sipariş → Stok Düş → Ödeme Al → Kargo Oluştur
  - Ödeme başarısız → Stok geri ekle (compensate) → Sipariş iptal et
- Go'da Choreography: NATS JetStream + event handler'lar (Gün 49 üzerine)
- Go'da Orchestration: state machine pattern — `type SagaState int` + `iota`
- Idempotency key: aynı event iki kez gelirse iki kez işleme
- Saga log: her adımı kaydet — restart sonrası kaldığı yerden devam
- `MassTransit State Machine` (C#) vs Go manuel state machine

**C# ile karşılaştırma:** MassTransit Saga State Machine vs Go explicit state machine + NATS — C#'ta framework, Go'da açık implementasyon

**Kontrol Soruları:**
1. Choreography vs Orchestration Saga — ne zaman hangisi? Avantaj/dezavantaj?
2. Compensating transaction her zaman mümkün mü? Örnek ver.
3. Idempotency key neden saga'da kritik?

---

## Genel Kontrol Soruları — Her Faz Sonrası

### Faz 1 Çıkış Soruları
1. Goroutine leak ne demek? Nasıl tespit edilir?
2. Interface satisfaction compile-time'da mı runtime'da mı? Neden önemli?
3. Go error handling'i exception'dan üstün yapan şey ne? Dezavantajı var mı?

### Faz 2 Çıkış Soruları
1. sqlc neden EF Core'un LINQ'sundan daha öngörülebilir?
2. `defer tx.Rollback()` + `tx.Commit()` pattern neden güvenli?
3. Validator ile middleware arasındaki sorumluluk sınırı nerede?

### Faz 3 Çıkış Soruları
1. Go'da CQRS MediatR olmadan nasıl yapılır? Ne kazanılır ne kaybedilir?
2. Clean Architecture'da domain neden hiçbir dış paketi import edemez?
3. Interface segregation Go'nun doğasında mı? Neden?

### Faz 4 Çıkış Soruları
1. `sync.Pool` neden GC baskısını azaltır?
2. pprof flame graph'ta "wide" fonksiyon ne anlama gelir?
3. Trace, metric, log üçlüsünde bir sorun olduğunda hangisinden başlarsın?

---

## Araçlar ve Kütüphane Seçimi

| Kategori | Seçim | Alternatif | Neden |
|---|---|---|---|
| Router | chi | gin, echo | Lightweight, stdlib uyumlu, iyi middleware |
| DB Driver | pgx/v5 | lib/pq | Daha performanslı, native feature'lar |
| Query Gen | sqlc | gorm, ent | Type-safe, öngörülebilir SQL, zero runtime reflection |
| Migration | golang-migrate | goose, atlas | Stabil, CLI + Go API |
| Validation | go-playground/validator | ozzo-validation | Tag-based, yaygın, cross-field desteği |
| JWT | golang-jwt/jwt | paseto | Yaygın, iyi dokümante |
| Logging | log/slog | zerolog, zap | Go 1.21+ standart, yeterli performans |
| Testing | testify | gomock, ginkgo | Minimal overhead, yaygın |
| Mock Gen | mockery | moq | Interface'den otomatik generate |
| Config | envconfig / viper | cleanenv | Basit projelerde envconfig, karmaşık projelerde viper |
| Cache | go-redis/redis/v9 | rueidis | Redis resmi olmayan ama en yaygın Go client |
| Rate Limiting | golang.org/x/time/rate | throttled | Built-in token bucket, sıfır bağımlılık |
| Health Check | alexliesenfeld/health | go-healthcheck | Liveness + readiness, K8s uyumlu |
| OAuth2 | golang.org/x/oauth2 | — | Go resmi OAuth2 client |
| OIDC | coreos/go-oidc/v3 | — | OIDC provider doğrulama |
| Lint | golangci-lint | staticcheck | Çok-linter runner, CI standardı |
| Load Test | k6 | vegeta | Language-agnostic, Grafana ekosistemi |
| Tracing | OpenTelemetry | Jaeger SDK | Vendor-neutral standart |
| Metrics | Prometheus | InfluxDB | Pull-based, Go native client, Grafana uyumu |
| gRPC | google.golang.org/grpc | twirp | Standart |
| Message Queue | NATS / JetStream | RabbitMQ, Kafka | Hafif, Go native, JetStream ile kalıcı |

---

## 500 vs 50K Kullanıcı — Ne Zaman Ne Kullan

| Özellik | 500 kullanıcı/ay | 50K kullanıcı/ay |
|---|---|---|
| sqlc + pgx | ✅ Yeterli | ✅ Yeterli |
| Connection pool 25 | ✅ | ⚠️ Pool boyutunu ölç |
| In-memory cache | ✅ | ❌ → Redis |
| Tek binary + docker | ✅ | ✅ |
| OpenTelemetry | ⚠️ Opsiyonel | ✅ Gerekli |
| Prometheus + Grafana | ❌ Overkill | ✅ Gerekli |
| gRPC | ❌ Overkill | ✅ Servisler arası |
| NATS/Kafka | ❌ Overkill | ✅ Async işler için |
| pprof aktif | ✅ Dev'de | ⚠️ Sadece gerektiğinde |
| Graceful shutdown | ✅ Her zaman | ✅ Her zaman |
| Migration startup'ta | ✅ | ⚠️ CI/CD'de daha iyi |
| testcontainers | ✅ | ✅ |
| CQRS | ❌ Overkill | ⚠️ Kompleks domain varsa |
| Clean Architecture | ⚠️ Opsiyonel | ✅ Büyük ekip için |
| Redis cache | ❌ In-memory yeter | ✅ Multi-instance gerekli |
| Rate limiting | ⚠️ Opsiyonel | ✅ Public API için zorunlu |
| OAuth2 / OIDC | ❌ JWT yeter | ✅ SSO, 3. taraf entegrasyonu |
| Multi-tenant | ❌ Single tenant | ⚠️ SaaS ürün için |
| Saga pattern | ❌ Overkill | ✅ Distributed transaction varsa |
| k6 yük testi | ⚠️ Opsiyonel | ✅ SLA garantisi için |
| golangci-lint CI | ✅ Her zaman | ✅ Her zaman |
