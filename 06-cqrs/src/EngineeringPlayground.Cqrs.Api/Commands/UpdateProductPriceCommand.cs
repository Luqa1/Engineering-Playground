namespace EngineeringPlayground.Cqrs.Api.Commands;

public sealed record UpdateProductPriceCommand(Guid ProductId, decimal Price);
