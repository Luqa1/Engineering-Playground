using EngineeringPlayground.Cqrs.Api.Commands;
using EngineeringPlayground.Cqrs.Api.Queries;
using EngineeringPlayground.Cqrs.Api.Queries.Models;
using EngineeringPlayground.Cqrs.Api.Requests;
using EngineeringPlayground.Cqrs.Api.Responses;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringPlayground.Cqrs.Api.Controllers;

[ApiController]
[Route("products")]
public sealed class ProductsController : ControllerBase
{
    private readonly GetProductsHandler _getProductsHandler;
    private readonly GetProductDetailsHandler _getProductDetailsHandler;
    private readonly UpdateProductPriceHandler _updateProductPriceHandler;
    private readonly AdjustInventoryHandler _adjustInventoryHandler;

    public ProductsController(GetProductsHandler getProductsHandler, GetProductDetailsHandler getProductDetailsHandler,
        UpdateProductPriceHandler updateProductPriceHandler, AdjustInventoryHandler adjustInventoryHandler)
    {
        _getProductsHandler = getProductsHandler;
        _getProductDetailsHandler = getProductDetailsHandler;
        _updateProductPriceHandler = updateProductPriceHandler;
        _adjustInventoryHandler = adjustInventoryHandler;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductListItem>>> GetAll(CancellationToken cancellationToken)
    {
        return await _getProductsHandler.HandleAsync(cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetails>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _getProductDetailsHandler.HandleAsync(new GetProductDetailsQuery(id), cancellationToken);
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
