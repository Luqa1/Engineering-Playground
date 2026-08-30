using EngineeringPlayground.OptimisticConcurrency.Domain;
using Microsoft.EntityFrameworkCore;

namespace EngineeringPlayground.OptimisticConcurrency.Infrastructure;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AdvanceVersions();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AdvanceVersions();

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
    }

    private void AdvanceVersions()
    {
        foreach (var entry in ChangeTracker.Entries<InventoryItem>()
                     .Where(entry => entry.State == EntityState.Modified))
        {
            var version = entry.Property(item => item.Version);
            version.CurrentValue = version.OriginalValue + 1;
        }
    }
}
