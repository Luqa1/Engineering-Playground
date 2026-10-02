using EngineeringPlayground.Saga.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace EngineeringPlayground.Saga.Infrastructure;

public sealed class OrderOperations
{
    private readonly IDbContextFactory<OrderDbContext> contextFactory;
    private readonly ILogger<OrderOperations> logger;
    public OrderOperations(IDbContextFactory<OrderDbContext> contextFactory, ILogger<OrderOperations> logger)
    {
        this.contextFactory = contextFactory;
        this.logger = logger;
    }
    // Each operation owns a fresh context and local transaction. There is no outer transaction.
    public async Task<Guid> CreateOrderAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = new Order();
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Order created {OrderId}", order.Id);
        return order.Id;
    }
    public async Task ReserveInventoryAsync(Guid orderId, Guid inventoryItemId, int quantity, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var item = await db.InventoryItems.SingleAsync(x => x.Id == inventoryItemId, cancellationToken);
        item.Reserve(quantity);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Inventory reserved {InventoryItemId} for order {OrderId}", inventoryItemId, orderId);
    }
    public async Task<PaymentStatus> ProcessPaymentAsync(Guid orderId, decimal amount, PaymentMode paymentMode = PaymentMode.Succeed, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var payment = new Payment(orderId, amount, paymentMode);
        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (payment.Status == PaymentStatus.Failed)
            logger.LogInformation("Payment failed {PaymentId} for order {OrderId}", payment.Id, orderId);
        else
            logger.LogInformation("Payment succeeded {PaymentId} for order {OrderId}", payment.Id, orderId);
        return payment.Status;
    }
    // Compensation is a new committed business operation, not rollback of the reservation.
    public async Task<bool> ReleaseInventoryAsync(Guid orderId, Guid inventoryItemId, int quantity, CancellationToken cancellationToken = default, InventoryReleaseMode releaseMode = InventoryReleaseMode.Succeed)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var item = await db.InventoryItems.SingleAsync(x => x.Id == inventoryItemId, cancellationToken);
        if (!Enum.IsDefined(releaseMode)) throw new ArgumentOutOfRangeException(nameof(releaseMode));
        // Expected demo refusal occurs inside the compensation operation, before any stock mutation.
        if (releaseMode == InventoryReleaseMode.Fail) return false;
        item.Release(quantity);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Inventory released {InventoryItemId} for order {OrderId}", inventoryItemId, orderId);
        return true;
    }
    public async Task CancelOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await db.Orders.SingleAsync(x => x.Id == orderId, cancellationToken);
        order.Cancel();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Order cancelled {OrderId}", orderId);
    }
    public async Task CompleteOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await db.Orders.SingleAsync(x => x.Id == orderId, cancellationToken);
        order.Complete();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Order completed {OrderId}", orderId);
    }
}
