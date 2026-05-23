using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using OrderService.Data;
using OrderService.HttpClients;
using OrderService.Saga;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Gün 133 — Fallback için cache
builder.Services.AddMemoryCache();
// bunu yazmasaydık: ProductHttpClient IMemoryCache inject edemez → DI hatası

// ── Gün 130 + 131 — Circuit Breaker + Retry + Timeout: ProductService ────
builder.Services.AddHttpClient<IProductHttpClient, ProductHttpClient>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["ProductService__BaseUrl"] ?? "http://product-service:8080/");
        // bunu yazmasaydık: her istekte base URL yazılmak zorunda kalınır
        client.Timeout = TimeSpan.FromSeconds(15);
        // Toplam HttpClient timeout — tüm retry'lar dahil maksimum süre
        // bunu yazmasaydık: default 100sn — pipeline'ın tüm döngüleri bu süreyi kullanabilir
    })
    .AddResilienceHandler("product-pipeline", pipeline =>
    {
        // Sıra önemli: CB (dış) → Retry (orta) → Timeout (iç, attempt başına)
        // CB OPEN ise Retry'a bile gidilmez — gereksiz backoff beklenmez

        // ── 1. Circuit Breaker — en dışta ────────────────────────────────
        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration  = TimeSpan.FromSeconds(30),
            MinimumThroughput = 5,
            // bunu yazmasaydık: 1 hata bile devreyi açar — false positive
            FailureRatio      = 0.5,
            BreakDuration     = TimeSpan.FromSeconds(30),
            // bunu yazmasaydık: devre bir daha kapanmaz

            OnOpened     = args => { Console.WriteLine($"🔴 CB AÇILDI {args.BreakDuration.TotalSeconds}sn"); return default; },
            OnClosed     = args => { Console.WriteLine("🟢 CB KAPANDI");                                     return default; },
            OnHalfOpened = args => { Console.WriteLine("🟡 CB YARI AÇIK");                                  return default; }
        });

        // ── 2. Concurrency Limiter (Bulkhead) — Gün 132 ─────────────────
        pipeline.AddConcurrencyLimiter(new System.Threading.RateLimiting.ConcurrencyLimiterOptions
        {
            PermitLimit = 10,
            // Aynı anda en fazla 10 eş zamanlı istek — bunu yazmasaydık: ProductService yavaşlayınca
            // tüm thread havuzunu tüketebilir, PaymentService ve ShipmentService de çöker
            QueueLimit  = 0
            // Kuyruk yok: 11. istek anında reddedilir (RateLimiterRejectedException)
            // bunu yazmasaydık: kuyruk da dolabilir, gecikme birikerek cascade'e yol açabilir
        });

        // ── 3. Retry — CB CLOSED + slot müsaitken çalışır ───────────────
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType      = DelayBackoffType.Exponential,
            // 1sn → 2sn → 4sn — bunu yazmasaydık: hepsi aynı anda retry → thundering herd
            UseJitter        = true,
            // Her beklemeye rastgele %0-50 eklenir — bunu yazmasaydık: 1000 kullanıcı aynı anda retry
            Delay            = TimeSpan.FromSeconds(1),

            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(r => (int)r.StatusCode >= 500),
                // 5xx → retry, 4xx → retry yapma (iş mantığı hatası, tekrar denesek de aynı sonuç)
                // bunu yazmasaydık: 404 "ürün bulunamadı" için de 3 kez retry → anlamsız yük

            OnRetry = args =>
            {
                Console.WriteLine($"🔄 Retry {args.AttemptNumber + 1}/3 | Bekleme: {args.RetryDelay.TotalMilliseconds:F0}ms");
                return default;
            }
        });

        // ── 4. Timeout — en içte (attempt başına) ────────────────────────
        pipeline.AddTimeout(TimeSpan.FromSeconds(4));
        // Her bir deneme için 4sn limit
        // bunu yazmasaydık: tek deneme 15sn sürebilir, Retry tüm süre boyunca bekler
    });
// ─────────────────────────────────────────────────────────────────────────

// Gün 127 — EF Core InMemory DB
builder.Services.AddDbContext<OrderDbContext>(opt =>
    opt.UseInMemoryDatabase("OrdersDb"));

// Gün 125 + 127 + 128 — MassTransit + RabbitMQ + Outbox + Saga
builder.Services.AddMassTransit(x =>
{
    // Gün 128 — Saga State Machine
    // OrderCreated → Saga başlar → PaymentRequested → PaymentCompleted/Failed → Completed/Cancelled
    x.AddSagaStateMachine<OrderSaga, OrderSagaState>()
        .InMemoryRepository();
    // InMemory: geliştirme için — servis restart'ta Saga durumları sıfırlanır
    // Gerçek projede: .EntityFrameworkRepository(r => r.ExistingDbContext<OrderDbContext>())
    // bunu yazmasaydık: Saga DI container'a kaydolmaz, event'ler işlenmez

    // Gün 127 — Outbox
    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UseInMemoryOutbox();
        o.UseBusOutbox();
        o.QueryDelay = TimeSpan.FromSeconds(1);
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ__Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ__Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ__Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
        // Saga için otomatik queue oluşturur: "order-saga"
        // bunu yazmasaydık: Saga event'leri dinleyemez
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "OrderService" }));
app.Run();
