using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace EngineeringPlayground.Saga.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderProcessing(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<OrderDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<OrderOperations>();
        services.AddScoped<OrderSaga>();
        services.AddScoped<OrderSagaStateService>();
        return services;
    }
}
