using EngineeringPlayground.Cqrs.Api.Requests;
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
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(CqrsDbContext database, ILogger<ProductsController> logger)
    {
        _database = database;
        _logger = logger;
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
    public async Task<ActionResult<Product>> UpdatePrice(
        Guid id, UpdatePriceRequest request, CancellationToken cancellationToken)
    {
        var product = await _database.Products.FindAsync([id], cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        try
        {
            product.UpdatePrice(request.Price!.Value);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: exception.Message);
        }

        await _database.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated price for product {ProductId} to {Price}.", id, product.Price);
        return product;
    }

    [HttpPut("{id:guid}/inventory")]
    public async Task<ActionResult<Product>> AdjustInventory(
        Guid id, AdjustInventoryRequest request, CancellationToken cancellationToken)
    {
        var product = await _database.Products.FindAsync([id], cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        try
        {
            product.AdjustInventory(request.Delta!.Value);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: exception.Message);
        }

        await _database.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Adjusted inventory for product {ProductId} by {Delta} to {Quantity}.",
            id, request.Delta, product.AvailableQuantity);
        return product;
    }
}
