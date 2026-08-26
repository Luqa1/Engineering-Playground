using EngineeringPlayground.OptimisticConcurrency.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace EngineeringPlayground.OptimisticConcurrency.Infrastructure.Migrations;

[DbContext(typeof(InventoryDbContext))]
partial class InventoryDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.4")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("EngineeringPlayground.OptimisticConcurrency.Domain.InventoryItem", entity =>
        {
            entity.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("id");

            entity.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)")
                .HasColumnName("name");

            entity.Property<int>("Quantity")
                .HasColumnType("integer")
                .HasColumnName("quantity");

            entity.HasKey("Id")
                .HasName("pk_inventory_items");

            entity.ToTable(
                "inventory_items",
                table => table.HasCheckConstraint(
                    "ck_inventory_items_quantity_non_negative",
                    "quantity >= 0"));
        });
#pragma warning restore 612, 618
    }
}
