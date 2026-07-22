using EngineeringPlayground.Outbox.Infrastructure;
using EngineeringPlayground.Outbox.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseMigration");

    try
    {
        logger.LogInformation("Starting automatic database migration");

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        await dbContext.Database.MigrateAsync(app.Lifetime.ApplicationStopping);

        logger.LogInformation("Automatic database migration completed successfully");
    }
    catch (Exception exception)
    {
        logger.LogCritical(exception, "Automatic database migration failed");
        throw;
    }
}

app.MapControllers();

app.Run();
