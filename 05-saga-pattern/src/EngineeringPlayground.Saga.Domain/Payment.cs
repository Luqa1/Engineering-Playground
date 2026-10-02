namespace EngineeringPlayground.Saga.Domain;

public enum PaymentStatus { Succeeded }
public sealed class Payment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; } = PaymentStatus.Succeeded;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    private Payment() { }
    public Payment(Guid orderId, decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        OrderId = orderId;
        Amount = amount;
    }
}
