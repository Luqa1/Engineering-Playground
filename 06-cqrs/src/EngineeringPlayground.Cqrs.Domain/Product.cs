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
        // Match numeric(18,2) so a successful write never rounds or overflows in PostgreSQL.
        if (price > 9999999999999999.99m || decimal.Round(price, 2) != price)
        {
            throw new ArgumentOutOfRangeException(nameof(price),
                "Price must not exceed 9999999999999999.99 and must have at most two decimal places.");
        }

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
