using EngineeringPlayground.Outbox.Api.Models;
using EngineeringPlayground.Outbox.Domain;
using EngineeringPlayground.Outbox.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace EngineeringPlayground.Outbox.Api.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly OutboxDbContext _dbContext;

    public OrdersController(OutboxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
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
            return ValidationProblem(new ValidationProblemDetails(validationErrors));
        }

        var order = new Order(
            request.CustomerId,
            request.TotalAmount,
            DateTime.UtcNow);

        var integrationEvent = new OrderCreatedIntegrationEvent(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.CreatedAtUtc);

        var outboxMessage = new OutboxMessage(
            Guid.NewGuid(),
            nameof(OrderCreatedIntegrationEvent),
            JsonSerializer.Serialize(integrationEvent),
            order.CreatedAtUtc);

        _dbContext.Orders.Add(order);
        _dbContext.OutboxMessages.Add(outboxMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new OrderResponse(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.CreatedAtUtc);

        return Created($"/orders/{order.Id}", response);
    }
}
