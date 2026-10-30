using System.Globalization;
using EngineeringPlayground.Cqrs.Domain;

namespace EngineeringPlayground.Cqrs.IntegrationTests;

public sealed class ProductTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NameCannotBeEmpty(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Product(Guid.NewGuid(), name!, 10.00m, 1));
    }

    [Fact]
    public void InitialPriceCannotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Product(Guid.NewGuid(), "Product", -1.00m, 1));
    }

    [Fact]
    public void InitialInventoryCannotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Product(Guid.NewGuid(), "Product", 10.00m, -1));
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.234")]
    [InlineData("10000000000000000")]
    public void InvalidPriceDoesNotMutateProduct(string value)
    {
        var price = decimal.Parse(value, CultureInfo.InvariantCulture);
        var product = new Product(Guid.NewGuid(), "Product", 10.00m, 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.UpdatePrice(price));
        Assert.Equal(10.00m, product.Price);
        Assert.Equal(5, product.AvailableQuantity);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Product(Guid.NewGuid(), "Product", price, 5));
    }

    [Theory]
    [InlineData(-6)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void InvalidAdjustmentDoesNotMutateProduct(int delta)
    {
        var product = new Product(Guid.NewGuid(), "Product", 10.00m, 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.AdjustInventory(delta));
        Assert.Equal(5, product.AvailableQuantity);
        Assert.Equal(10.00m, product.Price);
    }
}
