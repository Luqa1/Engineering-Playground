using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EngineeringPlayground.DistributedLock.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDistributedLockInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<PostgresAdvisoryLock>();

        services.AddDbContext<DistributedLockDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>()));

        return services;
    }
}
