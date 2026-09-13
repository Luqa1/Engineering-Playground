using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngineeringPlayground.DistributedLock.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDistributedLockInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<DistributedLockDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
