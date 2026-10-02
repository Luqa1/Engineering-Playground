using EngineeringPlayground.Saga.Domain;
using EngineeringPlayground.Saga.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EngineeringPlayground.Saga.IntegrationTests;

// xUnit creates a new instance (and isolated database container) for every test.
public sealed class OrderProcessingTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();
    private ServiceProvider services = null!;
    private IDbContextFactory<OrderDbContext> factory = null!;
    private OrderProcessor processor = null!;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        services = new ServiceCollection()
            .AddLogging()
            .AddOrderProcessing(postgres.GetConnectionString())
            .BuildServiceProvider();
        factory = services.GetRequiredService<IDbContextFactory<OrderDbContext>>();
        processor = services.GetRequiredService<OrderProcessor>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (services is not null) await services.DisposeAsync();
        await postgres.DisposeAsync();
    }

    [Fact]
    public async Task Successful_workflow_persists_completed_order_reserved_inventory_and_payment()
    {
        var id = await processor.ProcessAsync(InventoryItem.DemoId, 1, 100m);

        await using var db = await factory.CreateDbContextAsync();
        var order = await db.Orders.SingleAsync(x => x.Id == id);
        var inventory = await db.InventoryItems.SingleAsync(x => x.Id == InventoryItem.DemoId);
        var payment = await db.Payments.SingleAsync(x => x.OrderId == id);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(9, inventory.AvailableQuantity);
        Assert.Equal(1, inventory.ReservedQuantity);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(100m, payment.Amount);
    }

    [Fact]
    public async Task Every_operation_is_visible_from_a_fresh_context_before_the_next_operation()
    {
        var id = await processor.CreateOrderAsync();
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync(x => x.Id == id)).Status);
            Assert.Empty(await db.Payments.ToListAsync());
            Assert.Equal(10, (await db.InventoryItems.SingleAsync()).AvailableQuantity);
        }

        await processor.ReserveInventoryAsync(id, InventoryItem.DemoId, 1);
        await using (var db = await factory.CreateDbContextAsync())
        {
            var inventory = await db.InventoryItems.SingleAsync();
            Assert.Equal(9, inventory.AvailableQuantity);
            Assert.Equal(1, inventory.ReservedQuantity);
            Assert.Empty(await db.Payments.ToListAsync());
            Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync()).Status);
        }

        await processor.ProcessPaymentAsync(id, 100m);
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert.Equal(PaymentStatus.Succeeded, (await db.Payments.SingleAsync(x => x.OrderId == id)).Status);
            Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync()).Status);
        }

        await processor.CompleteOrderAsync(id);
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert.Equal(OrderStatus.Completed, (await db.Orders.SingleAsync()).Status);
        }
    }
}
