using MassTransit;
using PaymentService.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Gün 128 — PaymentService: Saga'dan gelen ödeme komutlarını işler
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentRequestedConsumer>();
    // bunu yazmasaydık: PaymentRequestedEvent dinlenmez, ödeme hiç işlenmez

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ__Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ__Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ__Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "PaymentService" }));
app.Run();
