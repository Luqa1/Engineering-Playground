using System.ComponentModel.DataAnnotations;

namespace EngineeringPlayground.OptimisticConcurrency.Api.Contracts;

public sealed record UpdateInventoryItemRequest(
    [Range(0, int.MaxValue)] int Quantity);
