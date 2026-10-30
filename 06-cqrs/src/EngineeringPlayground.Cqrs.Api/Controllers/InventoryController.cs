using EngineeringPlayground.Cqrs.Api.Responses;
using EngineeringPlayground.Cqrs.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EngineeringPlayground.Cqrs.Api.Controllers;

[ApiController]
[Route("inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly CqrsDbContext _database;

    public InventoryController(CqrsDbContext database)
    {
        _database = database;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<InventoryDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        const int lowStockThreshold = 5;

        // M3 keeps the shared Product model; dashboard calculations follow materialization.
        var products = await _database.Products.AsNoTracking().ToListAsync(cancellationToken);

        var attentionItems = products
            .Where(product => product.AvailableQuantity <= lowStockThreshold)
            .OrderBy(product => product.AvailableQuantity == 0 ? 0 : 1)
            .ThenBy(product => product.Name, StringComparer.Ordinal)
            .ThenBy(product => product.Id)
            .Select(product => new InventoryAttentionItemResponse(
                product.Id,
                product.Name,
                product.AvailableQuantity,
                product.AvailableQuantity == 0 ? "OutOfStock" : "LowStock",
                product.Price * product.AvailableQuantity))
            .ToList();

        return new InventoryDashboardResponse(
            products.Count,
            products.Count(product => product.AvailableQuantity == 0),
            products.Count(product => product.AvailableQuantity > 0
                && product.AvailableQuantity <= lowStockThreshold),
            products.Sum(product => product.Price * product.AvailableQuantity),
            attentionItems);
    }
}
