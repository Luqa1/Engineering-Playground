namespace EngineeringPlayground.Saga.Domain;

public enum OrderStatus { Pending, Completed, Cancelled }
public sealed class Order
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public void Complete() => Status = OrderStatus.Completed;
    public void Cancel()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only a pending order can be cancelled.");
        Status = OrderStatus.Cancelled;
    }
}
