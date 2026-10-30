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
}
