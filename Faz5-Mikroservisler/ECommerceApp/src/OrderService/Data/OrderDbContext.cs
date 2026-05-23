using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Entities;

namespace OrderService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.HasIndex(o => o.IdempotencyKey).IsUnique();
            // bunu yazmasaydık: aynı IdempotencyKey ile iki kayıt girebilir → duplicate sipariş
        });

        // MassTransit'in Outbox tablolarını şemaya ekle
        // bunu yazmasaydık: OutboxMessage, OutboxState tabloları oluşmaz → Publish çağrısı hata verir
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
