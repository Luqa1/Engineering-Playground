using EngineeringPlayground.StructuredLogging.Domain.Payments;
using EngineeringPlayground.StructuredLogging.Infrastructure.Payments;
using EngineeringPlayground.StructuredLogging.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngineeringPlayground.StructuredLogging.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IPaymentGateway, SimulatedPaymentGateway>();

        return services;
    }

    public static async Task ApplyMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }
}
