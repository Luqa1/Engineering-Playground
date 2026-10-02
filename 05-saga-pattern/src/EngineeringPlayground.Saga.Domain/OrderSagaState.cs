namespace EngineeringPlayground.Saga.Domain;

public enum OrderSagaStatus { Running, Completed, Compensating, Compensated, CompensationFailed }
public enum InventoryReleaseMode { Succeed, Fail }

public sealed class OrderSagaState
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; private set; }
    public OrderSagaStatus Status { get; private set; } = OrderSagaStatus.Running;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    private OrderSagaState() { }
    public OrderSagaState(Guid orderId) => OrderId = orderId;
    public void Complete() => Transition(OrderSagaStatus.Running, OrderSagaStatus.Completed);
    public void BeginCompensation() => Transition(OrderSagaStatus.Running, OrderSagaStatus.Compensating);
    public void FinishCompensation() => Transition(OrderSagaStatus.Compensating, OrderSagaStatus.Compensated);
    public void FailCompensation() => Transition(OrderSagaStatus.Compensating, OrderSagaStatus.CompensationFailed);
    private void Transition(OrderSagaStatus expected, OrderSagaStatus next)
    {
        if (Status != expected) throw new InvalidOperationException($"Cannot transition {Status} to {next}.");
        Status = next;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
