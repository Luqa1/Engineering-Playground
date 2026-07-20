using EngineeringPlayground.Outbox.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EngineeringPlayground.Outbox.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint(
                "CK_Orders_CustomerId_NotEmpty",
                "\"CustomerId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "CK_Orders_TotalAmount_Positive",
                "\"TotalAmount\" > 0");
        });

        builder.HasKey(order => order.Id);

        builder.Property(order => order.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(order => order.CustomerId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(order => order.TotalAmount)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(order => order.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
