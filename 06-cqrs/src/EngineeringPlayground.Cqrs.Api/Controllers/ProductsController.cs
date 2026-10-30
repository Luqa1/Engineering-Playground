using EngineeringPlayground.Cqrs.Api.Commands;
using EngineeringPlayground.Cqrs.Api.Requests;
using EngineeringPlayground.Cqrs.Api.Responses;
using EngineeringPlayground.Cqrs.Domain;
using EngineeringPlayground.Cqrs.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EngineeringPlayground.Cqrs.Api.Controllers;

[ApiController]
[Route("products")]
public sealed class ProductsController : ControllerBase
{
    private readonly CqrsDbContext _database;
    private readonly UpdateProductPriceHandler _updateProductPriceHandler;
    private readonly AdjustInventoryHandler _adjustInventoryHandler;

    public ProductsController(CqrsDbContext database,
        UpdateProductPriceHandler updateProductPriceHandler, AdjustInventoryHandler adjustInventoryHandler)
    {
        _database = database;
        _updateProductPriceHandler = updateProductPriceHandler;
        _adjustInventoryHandler = adjustInventoryHandler;
    }

    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll(CancellationToken cancellationToken)
    {
        return await _database.Products.OrderBy(product => product.Name).ToListAsync(cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Product>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _database.Products.FindAsync([id], cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        return product;
    }

    [HttpPut("{id:guid}/price")]
    public async Task<ActionResult<ProductResponse>> UpdatePrice(
        Guid id, UpdatePriceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateProductPriceCommand(id, request.Price!.Value);
            var result = await _updateProductPriceHandler.HandleAsync(command, cancellationToken);
            if (result is null)
            {
                return NotFound();
            }

            return result;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: exception.Message);
        }
    }

    [HttpPut("{id:guid}/inventory")]
    public async Task<ActionResult<ProductResponse>> AdjustInventory(
        Guid id, AdjustInventoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var command = new AdjustInventoryCommand(id, request.Delta!.Value);
            var result = await _adjustInventoryHandler.HandleAsync(command, cancellationToken);
            if (result is null)
            {
                return NotFound();
            }

            return result;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: exception.Message);
        }
    }
}
