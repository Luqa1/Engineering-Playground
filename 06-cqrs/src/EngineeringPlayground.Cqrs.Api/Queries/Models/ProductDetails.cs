namespace EngineeringPlayground.Cqrs.Api.Queries.Models;

public sealed record ProductDetails(Guid Id, string Name, decimal Price, int AvailableQuantity);
