using AnalyticsService.Consumers;
using ECommerce.Contracts.Events;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Gün 126 — MassTransit + Kafka Rider (consumer tarafı)
builder.Services.AddMassTransit(x =>
{
    x.AddRider(rider =>
    {
        // bunu yazmasaydık: consumer DI container'a kaydolmaz, mesaj alınamaz
        rider.AddConsumer<OrderAnalyticsConsumer>();

        rider.UsingKafka((ctx, k) =>
        {
            // docker-compose'da iç servis adı "kafka", port 29092
            // bunu yazmasaydık: localhost'a bağlanır, docker network'te çalışmaz
            k.Host(builder.Configuration["Kafka__BootstrapServers"] ?? "localhost:9092");

            // "order-events" topic'ini "analytics-group" consumer group'u ile dinle
            // NotificationService farklı bir group kullanıyor (RabbitMQ üzerinden)
            // → AnalyticsService tüm sipariş eventlerini bağımsız olarak okur
            // bunu yazmasaydık: farklı servisler mesajları paylaşır, tümünü göremez
            k.TopicEndpoint<OrderCreatedEvent>("order-events", "analytics-group", e =>
            {
                e.ConfigureConsumer<OrderAnalyticsConsumer>(ctx);
                // bunu yazmasaydık: consumer Kafka'ya register edilmez, mesaj alınmaz

                // Earliest: ilk başlangıçta geçmiş mesajları da oku (geliştirme için iyi)
                // Latest: sadece bu andan itibaren gelen yeni mesajları oku (production)
                e.AutoOffsetReset = AutoOffsetReset.Earliest;
            });
        });
    });
});

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "AnalyticsService" }));
app.Run();
