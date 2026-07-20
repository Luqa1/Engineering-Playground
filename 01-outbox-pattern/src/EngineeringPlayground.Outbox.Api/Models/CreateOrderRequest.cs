namespace EngineeringPlayground.Outbox.Api.Models;

public sealed record CreateOrderRequest(Guid CustomerId, decimal TotalAmount);
