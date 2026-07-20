namespace EngineeringPlayground.Outbox.Api.Models;

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime CreatedAtUtc);
