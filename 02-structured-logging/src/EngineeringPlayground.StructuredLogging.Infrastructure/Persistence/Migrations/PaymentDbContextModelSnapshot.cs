using EngineeringPlayground.StructuredLogging.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringPlayground.StructuredLogging.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PaymentDbContext))]
public partial class PaymentDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("EngineeringPlayground.StructuredLogging.Domain.Payments.Payment", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedNever()
                .HasColumnType("uuid")
                .HasColumnName("id");

            entity.Property<decimal>("Amount")
                .HasPrecision(18, 2)
                .HasColumnType("numeric(18,2)")
                .HasColumnName("amount");

            entity.Property<DateTimeOffset>("CreatedAtUtc")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at_utc");

            entity.Property<string>("Currency")
                .IsRequired()
                .HasMaxLength(16)
                .HasColumnType("character varying(16)")
                .HasColumnName("currency");

            entity.Property<Guid>("CustomerId")
                .HasColumnType("uuid")
                .HasColumnName("customer_id");

            entity.Property<string>("Status")
                .IsRequired()
                .HasMaxLength(16)
                .HasColumnType("character varying(16)")
                .HasColumnName("status");

            entity.HasKey("Id");

            entity.ToTable("payments");
        });
#pragma warning restore 612, 618
    }
}
