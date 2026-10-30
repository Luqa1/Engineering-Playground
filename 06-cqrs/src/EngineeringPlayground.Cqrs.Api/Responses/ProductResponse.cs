namespace EngineeringPlayground.Cqrs.Api.Responses;

public sealed record ProductResponse(Guid Id, string Name, decimal Price, int AvailableQuantity);
