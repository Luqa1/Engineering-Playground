using EngineeringPlayground.Saga.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOrderProcessing(builder.Configuration.GetConnectionString("Orders")
    ?? throw new InvalidOperationException("ConnectionStrings:Orders is required."));
var app = builder.Build();
if (!app.Environment.IsProduction())
{
    var factory = app.Services.GetRequiredService<IDbContextFactory<OrderDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}
app.MapControllers();
app.Run();
