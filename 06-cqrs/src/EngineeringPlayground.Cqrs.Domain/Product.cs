namespace EngineeringPlayground.Cqrs.Domain;

public sealed class Product
{
    private Product()
    {
    }

    public Product(Guid id, string name, decimal price, int availableQuantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(availableQuantity);
        Id = id;
        Name = name.Trim();
        UpdatePrice(price);
        AvailableQuantity = availableQuantity;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }
    public int AvailableQuantity { get; private set; }

    public void UpdatePrice(decimal price)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        Price = price;
    }

    public void AdjustInventory(int delta)
    {
        long quantity = (long)AvailableQuantity + delta;
        if (quantity < 0 || quantity > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(delta),
                "The resulting inventory quantity must be between zero and Int32.MaxValue.");
        }

        AvailableQuantity = (int)quantity;
    }
}
