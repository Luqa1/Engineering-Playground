namespace EngineeringPlayground.Cqrs.Api.Commands;

public sealed record AdjustInventoryCommand(Guid ProductId, int Delta);
