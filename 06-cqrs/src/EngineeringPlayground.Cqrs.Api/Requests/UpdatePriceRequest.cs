using System.ComponentModel.DataAnnotations;

namespace EngineeringPlayground.Cqrs.Api.Requests;

public sealed class UpdatePriceRequest
{
    [Required]
    public decimal? Price { get; init; }
}
