namespace EngineeringPlayground.Saga.Domain;

public enum OrderStatus { Pending, Completed }
public sealed class Order
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public void Complete() => Status = OrderStatus.Completed;
}
