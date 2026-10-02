namespace EngineeringPlayground.Saga.Domain;

public sealed class InventoryItem
{
    public static readonly Guid DemoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int AvailableQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    private InventoryItem() { }
    public InventoryItem(Guid id, string name, int availableQuantity)
    {
        Id = id;
        Name = name;
        AvailableQuantity = availableQuantity;
    }
    public void Reserve(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (quantity > AvailableQuantity)
            throw new InvalidOperationException("Insufficient inventory.");
        AvailableQuantity -= quantity;
        ReservedQuantity += quantity;
    }
}
