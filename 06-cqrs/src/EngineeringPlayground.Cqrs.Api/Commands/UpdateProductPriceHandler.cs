using EngineeringPlayground.Cqrs.Api.Responses;
using EngineeringPlayground.Cqrs.Infrastructure;

namespace EngineeringPlayground.Cqrs.Api.Commands;

public sealed class UpdateProductPriceHandler
{
    private readonly CqrsDbContext _database;
    private readonly ILogger<UpdateProductPriceHandler> _logger;

    public UpdateProductPriceHandler(CqrsDbContext database, ILogger<UpdateProductPriceHandler> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<ProductResponse?> HandleAsync(
        UpdateProductPriceCommand command, CancellationToken cancellationToken)
    {
        var product = await _database.Products.FindAsync([command.ProductId], cancellationToken);
        if (product is null)
        {
            return null;
        }

        product.UpdatePrice(command.Price);
        await _database.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated price for product {ProductId} to {Price}.", product.Id, product.Price);

        return new ProductResponse(product.Id, product.Name, product.Price, product.AvailableQuantity);
    }
}
