using EngineeringPlayground.Saga.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace EngineeringPlayground.Saga.Infrastructure;

public sealed class OrderProcessor
{
    private readonly IDbContextFactory<OrderDbContext> contextFactory;
    private readonly ILogger<OrderProcessor> logger;
    public OrderProcessor(IDbContextFactory<OrderDbContext> contextFactory, ILogger<OrderProcessor> logger)
    {
        this.contextFactory = contextFactory;
        this.logger = logger;
    }
    public async Task<Guid> ProcessAsync(Guid inventoryItemId, int quantity, decimal amount, PaymentMode paymentMode = PaymentMode.Succeed, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (!Enum.IsDefined(paymentMode)) throw new ArgumentOutOfRangeException(nameof(paymentMode));
        var orderId = await CreateOrderAsync(cancellationToken);
        await ReserveInventoryAsync(orderId, inventoryItemId, quantity, cancellationToken);
        var paymentStatus = await ProcessPaymentAsync(orderId, amount, paymentMode, cancellationToken);
        if (paymentStatus == PaymentStatus.Failed)
        {
            logger.LogInformation("Order processing stopped after payment failure {OrderId}", orderId);
            return orderId;
        }
        await CompleteOrderAsync(orderId, cancellationToken);
        return orderId;
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
