using EngineeringPlayground.Saga.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace EngineeringPlayground.Saga.Infrastructure;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
    }
}
public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
        // Reject stale concurrent reservations instead of overwriting committed stock.
        builder.Property(x => x.AvailableQuantity).IsConcurrencyToken();
        builder.HasData(new InventoryItem(InventoryItem.DemoId, "Demo Item", 10));
    }
}
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasOne<Order>().WithOne().HasForeignKey<Payment>(x => x.OrderId);
    }
}
public sealed class OrderSagaStateConfiguration : IEntityTypeConfiguration<OrderSagaState>
{
    public void Configure(EntityTypeBuilder<OrderSagaState> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasOne<Order>().WithOne().HasForeignKey<OrderSagaState>(x => x.OrderId);
    }
}
