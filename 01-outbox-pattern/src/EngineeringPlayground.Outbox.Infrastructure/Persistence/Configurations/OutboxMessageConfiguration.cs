using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EngineeringPlayground.Outbox.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(message => message.Type)
            .HasColumnType("character varying(500)")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(message => message.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(message => message.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(message => message.ProcessedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(message => message.Error)
            .HasColumnType("text");

        builder.Property(message => message.RetryCount)
            .HasColumnType("integer")
            .IsRequired();
    }
}
