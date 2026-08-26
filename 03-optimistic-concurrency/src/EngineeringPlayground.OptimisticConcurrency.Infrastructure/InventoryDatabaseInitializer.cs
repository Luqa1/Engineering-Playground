using EngineeringPlayground.OptimisticConcurrency.Domain;
using Microsoft.EntityFrameworkCore;

namespace EngineeringPlayground.OptimisticConcurrency.Infrastructure;

public static class InventoryDatabaseInitializer
{
    public static readonly Guid DemoItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task InitializeAsync(
        InventoryDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        var demoItemExists = await dbContext.InventoryItems
            .AnyAsync(item => item.Id == DemoItemId, cancellationToken);

        if (demoItemExists)
        {
            return;
        }

        dbContext.InventoryItems.Add(new InventoryItem
        {
            Id = DemoItemId,
            Name = "Demo Item",
            Quantity = 100
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
