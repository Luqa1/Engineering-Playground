namespace EngineeringPlayground.StructuredLogging.Api.Contracts;

public sealed record PaymentResponse(
    Guid Id,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAtUtc);
