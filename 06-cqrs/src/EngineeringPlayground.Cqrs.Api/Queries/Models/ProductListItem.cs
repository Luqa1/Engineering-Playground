namespace EngineeringPlayground.Cqrs.Api.Queries.Models;

public sealed record ProductListItem(Guid Id, string Name, decimal Price, int AvailableQuantity);
