using EngineeringPlayground.Cqrs.Api.Responses;
using EngineeringPlayground.Cqrs.Infrastructure;

namespace EngineeringPlayground.Cqrs.Api.Commands;

public sealed class AdjustInventoryHandler
{
    private readonly CqrsDbContext _database;
    private readonly ILogger<AdjustInventoryHandler> _logger;

    public AdjustInventoryHandler(CqrsDbContext database, ILogger<AdjustInventoryHandler> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<ProductResponse?> HandleAsync(
        AdjustInventoryCommand command, CancellationToken cancellationToken)
    {
        var product = await _database.Products.FindAsync([command.ProductId], cancellationToken);
        if (product is null)
        {
            return null;
        }

        product.AdjustInventory(command.Delta);
        await _database.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Adjusted inventory for product {ProductId} by {Delta} to {Quantity}.",
            product.Id, command.Delta, product.AvailableQuantity);

        return new ProductResponse(product.Id, product.Name, product.Price, product.AvailableQuantity);
    }
}
