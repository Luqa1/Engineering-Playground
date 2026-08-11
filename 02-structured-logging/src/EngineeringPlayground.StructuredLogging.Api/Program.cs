using EngineeringPlayground.StructuredLogging.Api.Services;
using EngineeringPlayground.StructuredLogging.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddScoped<PaymentProcessor>();

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    await app.Services.ApplyMigrationsAsync();
}

app.MapControllers();

app.Run();

public partial class Program;
