using EngineeringPlayground.Cqrs.Api.Responses;
using EngineeringPlayground.Cqrs.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EngineeringPlayground.Cqrs.Api.Queries;

public sealed class GetInventoryDashboardHandler
{
    private readonly CqrsDbContext _database;

    public GetInventoryDashboardHandler(CqrsDbContext database)
    {
        _database = database;
    }

    public async Task<InventoryDashboardResponse> HandleAsync(CancellationToken cancellationToken)
    {
        const int lowStockThreshold = 5;
        var products = _database.Products.AsNoTracking();

        // Run sequentially: these queries share one scoped DbContext.
        var totalProducts = await products.CountAsync(cancellationToken);
        var outOfStockCount = await products.CountAsync(
            product => product.AvailableQuantity == 0, cancellationToken);
        var lowStockCount = await products.CountAsync(
            product => product.AvailableQuantity > 0 && product.AvailableQuantity <= lowStockThreshold,
            cancellationToken);
        var totalInventoryValue = await products.SumAsync(
            product => (decimal?)(product.Price * product.AvailableQuantity), cancellationToken) ?? 0m;

        var attentionItems = await products
            .Where(product => product.AvailableQuantity <= lowStockThreshold)
            .Select(product => new InventoryAttentionItemResponse(
                product.Id,
                product.Name,
                product.AvailableQuantity,
                product.AvailableQuantity == 0 ? "OutOfStock" : "LowStock",
                product.Price * product.AvailableQuantity))
            .ToListAsync(cancellationToken);

        // Preserve M3's exact UTF-16 ordinal ordering, which PostgreSQL collations do not provide.
        // Only filtered, projected attention DTOs are sorted in memory.
        var sortedAttentionItems = attentionItems
            .OrderBy(item => item.AvailableQuantity == 0 ? 0 : 1)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .ToList();

        return new InventoryDashboardResponse(
            totalProducts, outOfStockCount, lowStockCount, totalInventoryValue, sortedAttentionItems);
    }
}
