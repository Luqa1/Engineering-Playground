using System.ComponentModel.DataAnnotations;

namespace EngineeringPlayground.Cqrs.Api.Requests;

public sealed class AdjustInventoryRequest
{
    [Required]
    public int? Delta { get; init; }
}
