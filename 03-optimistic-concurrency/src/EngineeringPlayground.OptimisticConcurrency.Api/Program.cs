using EngineeringPlayground.OptimisticConcurrency.Api.Services;
using EngineeringPlayground.OptimisticConcurrency.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("InventoryDatabase")
    ?? throw new InvalidOperationException("Connection string 'InventoryDatabase' is not configured.");

builder.Services.AddOptimisticConcurrencyInfrastructure(connectionString);
builder.Services.AddScoped<InventoryService>();

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await InventoryDatabaseInitializer.InitializeAsync(dbContext);
}

app.MapControllers();

app.Run();

public partial class Program;
