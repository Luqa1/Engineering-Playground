using EngineeringPlayground.StructuredLogging.Api.Services;
using EngineeringPlayground.StructuredLogging.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

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

app.UseSerilogRequestLogging();

app.MapControllers();

app.Run();

public partial class Program;
