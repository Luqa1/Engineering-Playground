using EngineeringPlayground.Saga.Domain;
using Microsoft.Extensions.Logging;

namespace EngineeringPlayground.Saga.Infrastructure;

public sealed class OrderSaga
{
    private readonly OrderOperations operations;
    private readonly OrderSagaStateService states;
    private readonly ILogger<OrderSaga> logger;

    public OrderSaga(OrderOperations operations, OrderSagaStateService states, ILogger<OrderSaga> logger)
    {
        this.operations = operations;
        this.states = states;
        this.logger = logger;
    }

    public async Task<Guid> ProcessAsync(Guid inventoryItemId, int quantity, decimal amount,
        PaymentMode paymentMode = PaymentMode.Succeed, CancellationToken cancellationToken = default, InventoryReleaseMode inventoryReleaseMode = InventoryReleaseMode.Succeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (!Enum.IsDefined(paymentMode)) throw new ArgumentOutOfRangeException(nameof(paymentMode));

        if (!Enum.IsDefined(inventoryReleaseMode)) throw new ArgumentOutOfRangeException(nameof(inventoryReleaseMode));
        // Each operation commits before the next starts. The Saga owns no transaction.
        var orderId = await operations.CreateOrderAsync(cancellationToken);
        var state = await states.StartAsync(orderId, cancellationToken);
        logger.LogInformation("Saga started {SagaId} for order {OrderId}", state.Id, orderId);
        await operations.ReserveInventoryAsync(orderId, inventoryItemId, quantity, cancellationToken);
        var paymentStatus = await operations.ProcessPaymentAsync(orderId, amount, paymentMode, cancellationToken);

        if (paymentStatus == PaymentStatus.Failed)
        {
            state.BeginCompensation();
            await states.SaveAsync(state, cancellationToken);
            logger.LogInformation("Saga {SagaId} entered compensation for order {OrderId}; inventory compensation started {InventoryItemId}", state.Id, orderId, inventoryItemId);
            var released = await operations.ReleaseInventoryAsync(orderId, inventoryItemId, quantity, cancellationToken, inventoryReleaseMode);
            if (!released)
            {
                state.FailCompensation();
                await states.SaveAsync(state, cancellationToken);
                logger.LogWarning("Inventory compensation failed {InventoryItemId}; Saga {SagaId} marked CompensationFailed for order {OrderId}", inventoryItemId, state.Id, orderId);
                return orderId;
            }
            await operations.CancelOrderAsync(orderId, cancellationToken);
            state.FinishCompensation();
            await states.SaveAsync(state, cancellationToken);
            logger.LogInformation("Saga compensated {SagaId} for order {OrderId}", state.Id, orderId);
            return orderId;
        }

        await operations.CompleteOrderAsync(orderId, cancellationToken);
        state.Complete();
        await states.SaveAsync(state, cancellationToken);
        logger.LogInformation("Saga completed {SagaId} for order {OrderId}", state.Id, orderId);
        return orderId;
    }
}
