using ECommerce.Contracts.Events;
using MassTransit;

namespace OrderService.Saga;

public class OrderSaga : MassTransitStateMachine<OrderSagaState>
{
    // ── Durumlar ───────────────────────────────────────────────────────────
    public State AwaitingPayment  { get; private set; } = null!;
    public State AwaitingShipment { get; private set; } = null!;
    public State Completed        { get; private set; } = null!;
    public State Cancelled        { get; private set; } = null!;

    // ── Eventler ──────────────────────────────────────────────────────────
    public Event<OrderCreatedEvent>     OrderCreated     { get; private set; } = null!;
    public Event<PaymentCompletedEvent> PaymentCompleted { get; private set; } = null!;
    public Event<PaymentFailedEvent>    PaymentFailed    { get; private set; } = null!;
    public Event<ShipmentPreparedEvent> ShipmentPrepared { get; private set; } = null!;
    public Event<ShipmentFailedEvent>   ShipmentFailed   { get; private set; } = null!;

    public OrderSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreated,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentCompleted,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentFailed,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => ShipmentPrepared,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => ShipmentFailed,
            x => x.CorrelateById(ctx => ctx.Message.OrderId));

        // ── Adım 1: Sipariş oluştu → Ödeme iste ──────────────────────────
        Initially(
            When(OrderCreated)
                .Then(ctx =>
                {
                    ctx.Saga.OrderId       = ctx.Message.OrderId;
                    ctx.Saga.CustomerEmail = ctx.Message.CustomerEmail;
                    ctx.Saga.CustomerName  = ctx.Message.CustomerName;
                    ctx.Saga.ProductName   = ctx.Message.ProductName;
                    ctx.Saga.Quantity      = ctx.Message.Quantity;
                    ctx.Saga.TotalAmount   = ctx.Message.TotalAmount;
                })
                .Publish(ctx => new PaymentRequestedEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.CustomerEmail))
                .TransitionTo(AwaitingPayment)
        );

        // ── Adım 2: Ödeme bekleniyor ──────────────────────────────────────
        During(AwaitingPayment,

            When(PaymentCompleted)
                .Then(ctx =>
                {
                    ctx.Saga.PaidAt        = DateTime.UtcNow;
                    ctx.Saga.TransactionId = ctx.Message.TransactionId;
                })
                .Publish(ctx => new ShipmentRequestedEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerName,
                    ctx.Saga.ProductName,
                    ctx.Saga.Quantity))
                // bunu yazmasaydık: ödeme tamam ama kargo başlamaz
                .TransitionTo(AwaitingShipment),

            When(PaymentFailed)
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Publish(ctx => new OrderCancelledEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.FailureReason ?? "Ödeme başarısız"))
                .TransitionTo(Cancelled)
                .Finalize()
        );

        // ── Adım 3: Kargo bekleniyor ──────────────────────────────────────
        During(AwaitingShipment,

            When(ShipmentPrepared)
                .Then(ctx =>
                {
                    ctx.Saga.ShippedAt      = DateTime.UtcNow;
                    ctx.Saga.TrackingNumber = ctx.Message.TrackingNumber;
                })
                .Publish(ctx => new OrderCompletedEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.TotalAmount))
                .TransitionTo(Completed)
                .Finalize(),

            When(ShipmentFailed)
                // Compensating transaction:
                // Kargo olmadı ama ödeme alınmıştı → iade et
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .Publish(ctx => new PaymentRefundedEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail,
                    ctx.Saga.TotalAmount,
                    ctx.Saga.FailureReason ?? "Stokta yok"))
                // bunu yazmasaydık: kargo başarısız ama müşteriden para kesik kalır
                .Publish(ctx => new OrderCancelledEvent(
                    ctx.Saga.OrderId,
                    ctx.Saga.CustomerEmail,
                    $"Kargo hazırlanamadı: {ctx.Saga.FailureReason}"))
                .TransitionTo(Cancelled)
                .Finalize()
        );

        SetCompletedWhenFinalized();
    }
}
