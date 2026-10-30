using EngineeringPlayground.Cqrs.Api.Queries.Models;
using EngineeringPlayground.Cqrs.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EngineeringPlayground.Cqrs.Api.Queries;

public sealed class GetProductsHandler
{
    private readonly CqrsDbContext _database;

    public GetProductsHandler(CqrsDbContext database)
    {
        _database = database;
    }

    public Task<List<ProductListItem>> HandleAsync(CancellationToken cancellationToken)
    {
        return _database.Products
            .AsNoTracking()
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .Select(product => new ProductListItem(
                product.Id, product.Name, product.Price, product.AvailableQuantity))
            .ToListAsync(cancellationToken);
    }
}
