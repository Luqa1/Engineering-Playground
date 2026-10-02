using EngineeringPlayground.Saga.Domain;
using Microsoft.EntityFrameworkCore;
namespace EngineeringPlayground.Saga.Infrastructure;

public sealed class OrderSagaStateService
{
    private readonly IDbContextFactory<OrderDbContext> contextFactory;
    public OrderSagaStateService(IDbContextFactory<OrderDbContext> contextFactory) => this.contextFactory = contextFactory;
    public async Task<OrderSagaState> StartAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var state = new OrderSagaState(orderId);
        db.OrderSagaStates.Add(state);
        await db.SaveChangesAsync(cancellationToken);
        return state;
    }
    public async Task SaveAsync(OrderSagaState state, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.OrderSagaStates.Update(state);
        await db.SaveChangesAsync(cancellationToken);
    }
}
