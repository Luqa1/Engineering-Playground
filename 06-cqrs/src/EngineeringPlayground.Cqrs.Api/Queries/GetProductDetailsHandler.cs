using EngineeringPlayground.Cqrs.Api.Queries.Models;
using EngineeringPlayground.Cqrs.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EngineeringPlayground.Cqrs.Api.Queries;

public sealed class GetProductDetailsHandler
{
    private readonly CqrsDbContext _database;

    public GetProductDetailsHandler(CqrsDbContext database)
    {
        _database = database;
    }

    public Task<ProductDetails?> HandleAsync(GetProductDetailsQuery query, CancellationToken cancellationToken)
    {
        return _database.Products
            .AsNoTracking()
            .Where(product => product.Id == query.ProductId)
            .Select(product => new ProductDetails(
                product.Id, product.Name, product.Price, product.AvailableQuantity))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
