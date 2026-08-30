namespace EngineeringPlayground.OptimisticConcurrency.Domain;

public sealed class InventoryItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public long Version { get; private set; } = 1;
}
