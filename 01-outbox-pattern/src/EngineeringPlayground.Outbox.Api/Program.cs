using EngineeringPlayground.Outbox.Domain;
using EngineeringPlayground.Outbox.Infrastructure;
using EngineeringPlayground.Outbox.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapPost("/orders", async (
    CreateOrderRequest request,
    OutboxDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var validationErrors = new Dictionary<string, string[]>();

    if (request.CustomerId == Guid.Empty)
    {
        validationErrors[nameof(request.CustomerId)] =
            ["CustomerId must not be empty."];
    }

    if (request.TotalAmount <= 0)
    {
        validationErrors[nameof(request.TotalAmount)] =
            ["TotalAmount must be greater than zero."];
    }

    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var order = new Order(
        request.CustomerId,
        request.TotalAmount,
        DateTime.UtcNow);

    dbContext.Orders.Add(order);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Created($"/orders/{order.Id}", new OrderResponse(
        order.Id,
        order.CustomerId,
        order.TotalAmount,
        order.CreatedAtUtc));
});

app.Run();

internal sealed record CreateOrderRequest(Guid CustomerId, decimal TotalAmount);

internal sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime CreatedAtUtc);
