using EngineeringPlayground.Saga.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EngineeringPlayground.Saga.Api.Controllers;

public sealed record InventoryResponse(Guid Id, string Name, int AvailableQuantity, int ReservedQuantity);
[ApiController]
[Route("inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly IDbContextFactory<OrderDbContext> contextFactory;
    public InventoryController(IDbContextFactory<OrderDbContext> contextFactory)
        => this.contextFactory = contextFactory;
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InventoryResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? NotFound() : Ok(new InventoryResponse(item.Id, item.Name, item.AvailableQuantity, item.ReservedQuantity));
    }
}
