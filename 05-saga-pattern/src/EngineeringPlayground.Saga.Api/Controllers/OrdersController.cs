using System.ComponentModel.DataAnnotations;
using EngineeringPlayground.Saga.Infrastructure;
using EngineeringPlayground.Saga.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EngineeringPlayground.Saga.Api.Controllers;

public sealed record CreateOrderRequest(
    Guid InventoryItemId,
    [Range(1, int.MaxValue)] int Quantity,
    [Range(typeof(decimal), "0.01", "9999999999999999.99")] decimal Amount,
    [Required, RegularExpression("^(Succeed|Fail)$")] string PaymentMode = "Succeed");
public sealed record OrderResponse(Guid OrderId, string OrderStatus, string? PaymentStatus);
[ApiController]
[Route("orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly OrderProcessor processor;
    private readonly IDbContextFactory<OrderDbContext> contextFactory;
    public OrdersController(OrderProcessor processor, IDbContextFactory<OrderDbContext> contextFactory)
    {
        this.processor = processor;
        this.contextFactory = contextFactory;
    }
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var id = await processor.ProcessAsync(request.InventoryItemId, request.Quantity, request.Amount, Enum.Parse<PaymentMode>(request.PaymentMode), cancellationToken);
        var response = await ReadAsync(id, cancellationToken);
        if (response?.PaymentStatus == PaymentStatus.Failed.ToString())
            return UnprocessableEntity(response);
        return CreatedAtAction(nameof(Get), new { id }, response);
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var response = await ReadAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
    private async Task<OrderResponse?> ReadAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null) return null;
        var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == id, cancellationToken);
        return new OrderResponse(order.Id, order.Status.ToString(), payment?.Status.ToString());
    }
}
