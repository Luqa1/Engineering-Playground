using EngineeringPlayground.OptimisticConcurrency.Api.Contracts;
using EngineeringPlayground.OptimisticConcurrency.Api.Services;
using EngineeringPlayground.OptimisticConcurrency.Domain;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringPlayground.OptimisticConcurrency.Api.Controllers;

[ApiController]
[Route("inventory")]
public sealed class InventoryController(InventoryService inventoryService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InventoryItemResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await inventoryService.GetAsync(id, cancellationToken);

        return item is null
            ? NotFound()
            : Ok(ToResponse(item));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InventoryItemResponse>> Put(
        Guid id,
        UpdateInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await inventoryService.UpdateQuantityAsync(
            id,
            request.Quantity,
            cancellationToken);

        return item is null
            ? NotFound()
            : Ok(ToResponse(item));
    }

    private static InventoryItemResponse ToResponse(InventoryItem item)
    {
        return new InventoryItemResponse(item.Id, item.Name, item.Quantity);
    }
}
