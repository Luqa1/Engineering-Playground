namespace EngineeringPlayground.StructuredLogging.Domain.Payments;

public sealed class Payment
{
    private Payment()
    {
    }

    private Payment(Guid customerId, decimal amount, string currency)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));
        }

        Id = Guid.NewGuid();
        CustomerId = customerId;
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        Status = PaymentStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Payment Create(Guid customerId, decimal amount, string currency)
    {
        return new Payment(customerId, amount, currency);
    }

    public void MarkCompleted()
    {
        EnsurePending();
        Status = PaymentStatus.Completed;
    }

    public void MarkFailed()
    {
        EnsurePending();
        Status = PaymentStatus.Failed;
    }

    private void EnsurePending()
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException($"A {Status} payment cannot change status.");
        }
    }
}
