namespace EngineeringPlayground.Outbox.Domain;

public sealed class Order
{
    private Order()
    {
    }

    public Order(Guid customerId, decimal totalAmount, DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        TotalAmount = totalAmount;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
