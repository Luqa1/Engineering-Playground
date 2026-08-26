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

    public async Task<InventoryItem?> UpdateQuantityAsync(
        Guid id,
        int quantity,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.InventoryItems
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (item is null)
        {
            return null;
        }

        item.Quantity = quantity;
        await dbContext.SaveChangesAsync(cancellationToken);

        return item;
    }
}
