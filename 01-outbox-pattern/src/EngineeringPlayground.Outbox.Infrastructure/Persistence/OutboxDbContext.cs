using EngineeringPlayground.Outbox.Domain;
using Microsoft.EntityFrameworkCore;

namespace EngineeringPlayground.Outbox.Infrastructure.Persistence;

public sealed class OutboxDbContext(DbContextOptions<OutboxDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OutboxDbContext).Assembly);
    }
}
