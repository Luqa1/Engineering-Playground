using EngineeringPlayground.Cqrs.Api.Commands;
using EngineeringPlayground.Cqrs.Api.Queries;
using EngineeringPlayground.Cqrs.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<UpdateProductPriceHandler>();
builder.Services.AddScoped<AdjustInventoryHandler>();
builder.Services.AddScoped<GetProductsHandler>();
builder.Services.AddScoped<GetProductDetailsHandler>();
builder.Services.AddScoped<GetInventoryDashboardHandler>();
builder.Services.AddDbContext<CqrsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Cqrs")
        ?? throw new InvalidOperationException("ConnectionStrings:Cqrs must be configured.")));

var app = builder.Build();
app.UseExceptionHandler();

if (!app.Environment.IsProduction())
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<CqrsDbContext>();
    app.Logger.LogInformation("Applying pending CQRS baseline database migrations.");
    await database.Database.MigrateAsync();
}

app.MapControllers();
app.Run();

public partial class Program;
