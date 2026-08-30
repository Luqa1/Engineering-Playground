namespace EngineeringPlayground.OptimisticConcurrency.Api.Contracts;

public sealed record InventoryItemConflictResponse(
    string Message,
    InventoryItemResponse Current);
