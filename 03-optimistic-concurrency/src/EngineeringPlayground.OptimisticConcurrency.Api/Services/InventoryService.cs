using EngineeringPlayground.OptimisticConcurrency.Domain;
using EngineeringPlayground.OptimisticConcurrency.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EngineeringPlayground.OptimisticConcurrency.Api.Services;

public sealed class InventoryService(InventoryDbContext dbContext)
{
    public Task<InventoryItem?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.InventoryItems
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<InventoryItemUpdateResult> UpdateQuantityAsync(
        Guid id,
        int quantity,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.InventoryItems
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (item is null)
        {
            return new InventoryItemUpdateResult(Item: null, HasConflict: false);
        }

        item.Quantity = quantity;
        var entry = dbContext.Entry(item);
        entry.Property(inventoryItem => inventoryItem.Quantity).IsModified = true;
        entry.Property(inventoryItem => inventoryItem.Version).OriginalValue = expectedVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            return new InventoryItemUpdateResult(item, HasConflict: false);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();

            var currentItem = await GetAsync(id, cancellationToken);

            return new InventoryItemUpdateResult(
                currentItem,
                HasConflict: currentItem is not null);
        }
    }
}

public sealed record InventoryItemUpdateResult(InventoryItem? Item, bool HasConflict);
