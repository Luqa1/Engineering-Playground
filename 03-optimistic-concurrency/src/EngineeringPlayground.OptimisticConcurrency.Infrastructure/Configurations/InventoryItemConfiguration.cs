using EngineeringPlayground.OptimisticConcurrency.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EngineeringPlayground.OptimisticConcurrency.Infrastructure.Configurations;

public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable(
            "inventory_items",
            table => table.HasCheckConstraint(
                "ck_inventory_items_quantity_non_negative",
                "quantity >= 0"));

        builder.HasKey(item => item.Id)
            .HasName("pk_inventory_items");

        builder.Property(item => item.Id)
            .HasColumnName("id");

        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.Quantity)
            .HasColumnName("quantity")
            .IsRequired();
    }
}
