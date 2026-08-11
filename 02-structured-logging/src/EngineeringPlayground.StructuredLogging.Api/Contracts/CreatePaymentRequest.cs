using System.ComponentModel.DataAnnotations;

namespace EngineeringPlayground.StructuredLogging.Api.Contracts;

public sealed class CreatePaymentRequest
{
    public Guid CustomerId { get; init; }

    public decimal Amount { get; init; }

    [Required]
    public string Currency { get; init; } = string.Empty;
}
