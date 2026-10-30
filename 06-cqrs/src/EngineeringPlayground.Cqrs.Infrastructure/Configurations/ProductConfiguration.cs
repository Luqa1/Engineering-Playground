using EngineeringPlayground.Cqrs.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EngineeringPlayground.Cqrs.Infrastructure.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Name).IsRequired();
        builder.Property(product => product.Price).HasPrecision(18, 2);
        builder.Property(product => product.AvailableQuantity).IsRequired();
        builder.HasData(
            new Product(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Mechanical Keyboard", 299.00m, 15),
            new Product(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Wireless Mouse", 129.00m, 30),
            new Product(Guid.Parse("33333333-3333-3333-3333-333333333333"), "USB-C Dock", 449.00m, 8));
    }
}
