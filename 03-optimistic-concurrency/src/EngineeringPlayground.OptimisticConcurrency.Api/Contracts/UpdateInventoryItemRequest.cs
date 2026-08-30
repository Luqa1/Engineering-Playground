using System.ComponentModel.DataAnnotations;

namespace EngineeringPlayground.OptimisticConcurrency.Api.Contracts;

public sealed record UpdateInventoryItemRequest(
    [Range(0, int.MaxValue)] int Quantity,
    [Range(typeof(long), "1", "9223372036854775806")] long Version);
