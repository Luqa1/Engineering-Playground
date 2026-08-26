using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngineeringPlayground.OptimisticConcurrency.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddOptimisticConcurrencyInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
