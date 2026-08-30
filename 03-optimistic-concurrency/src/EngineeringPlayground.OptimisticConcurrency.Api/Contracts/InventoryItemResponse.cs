namespace EngineeringPlayground.OptimisticConcurrency.Api.Contracts;

public sealed record InventoryItemResponse(Guid Id, string Name, int Quantity, long Version);
