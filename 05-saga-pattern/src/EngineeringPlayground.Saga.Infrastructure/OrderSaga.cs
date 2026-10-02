using EngineeringPlayground.Saga.Domain;
using Microsoft.Extensions.Logging;

namespace EngineeringPlayground.Saga.Infrastructure;

public sealed class OrderSaga
{
    private readonly OrderOperations operations;
    private readonly ILogger<OrderSaga> logger;

    public OrderSaga(OrderOperations operations, ILogger<OrderSaga> logger)
    {
        this.operations = operations;
        this.logger = logger;
    }

    public async Task<Guid> ProcessAsync(Guid inventoryItemId, int quantity, decimal amount,
        PaymentMode paymentMode = PaymentMode.Succeed, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (!Enum.IsDefined(paymentMode)) throw new ArgumentOutOfRangeException(nameof(paymentMode));

        logger.LogInformation("Order Saga started");
        // Each operation commits before the next starts. The Saga owns no transaction.
        var orderId = await operations.CreateOrderAsync(cancellationToken);
        await operations.ReserveInventoryAsync(orderId, inventoryItemId, quantity, cancellationToken);
        var paymentStatus = await operations.ProcessPaymentAsync(orderId, amount, paymentMode, cancellationToken);

        if (paymentStatus == PaymentStatus.Failed)
        {
            logger.LogInformation("Starting compensation after payment failure for order {OrderId} and inventory {InventoryItemId}", orderId, inventoryItemId);
            await operations.ReleaseInventoryAsync(orderId, inventoryItemId, quantity, cancellationToken);
            await operations.CancelOrderAsync(orderId, cancellationToken);
            logger.LogInformation("Compensation completed for order {OrderId}", orderId);
            return orderId;
        }

        await operations.CompleteOrderAsync(orderId, cancellationToken);
        logger.LogInformation("Order Saga completed {OrderId}", orderId);
        return orderId;
    }
}
