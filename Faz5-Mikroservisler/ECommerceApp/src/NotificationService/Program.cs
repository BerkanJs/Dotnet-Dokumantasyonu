using MassTransit;
using NotificationService.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Gün 125 + 129 — MassTransit + RabbitMQ consumer
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>(cfg =>
    {
        cfg.UseMessageRetry(r =>
            r.Intervals(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30)
            ));
    });

    // Gün 129 — Saga'dan gelen event'leri dinle
    x.AddConsumer<OrderCompletedConsumer>();   // Siparişiniz yolda
    x.AddConsumer<OrderCancelledConsumer>();   // Siparişiniz iptal edildi
    x.AddConsumer<PaymentRefundedConsumer>();  // Ödemeniz iade edildi (compensating)

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ__Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ__Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ__Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("notification-order-created", e =>
        {
            e.PrefetchCount = 5;
            e.Durable = true;
            e.ConfigureDeadLetterQueueDeadLetterTransport();
            e.ConfigureConsumer<OrderCreatedConsumer>(context);
        });

        cfg.ConfigureEndpoints(context);
        // OrderCompleted, OrderCancelled, PaymentRefunded için otomatik queue oluşturur
    });
});

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "NotificationService" }));
app.Run();
