using EngineeringPlayground.Outbox.Infrastructure.Persistence;
using EngineeringPlayground.Outbox.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EngineeringPlayground.Outbox.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException(
                "Connection string 'PostgreSQL' is not configured.");

        services.AddDbContext<OutboxDbContext>(options =>
            options.UseNpgsql(connectionString));
        var rabbitMqSection = configuration.GetSection(RabbitMqOptions.SectionName);
        services.Configure<RabbitMqOptions>(options =>
        {
            options.Host = rabbitMqSection[nameof(RabbitMqOptions.Host)] ?? "localhost";
            options.Port = int.TryParse(rabbitMqSection[nameof(RabbitMqOptions.Port)], out var port) ? port : 5672;
            options.Username = rabbitMqSection[nameof(RabbitMqOptions.Username)] ?? "guest";
            options.Password = rabbitMqSection[nameof(RabbitMqOptions.Password)] ?? "guest";
        });
        services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();

        return services;
    }
}
