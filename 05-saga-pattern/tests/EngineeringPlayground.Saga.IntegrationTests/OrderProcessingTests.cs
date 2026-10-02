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
    private OrderSaga saga = null!;
    private OrderOperations operations = null!;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        services = new ServiceCollection()
            .AddLogging()
            .AddOrderProcessing(postgres.GetConnectionString())
            .BuildServiceProvider();
        factory = services.GetRequiredService<IDbContextFactory<OrderDbContext>>();
        saga = services.GetRequiredService<OrderSaga>();
        operations = services.GetRequiredService<OrderOperations>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (services is not null) await services.DisposeAsync();
        await postgres.DisposeAsync();
    }

    [Fact]
    public async Task Successful_saga_persists_completed_order_reserved_inventory_and_payment()
    {
        var id = await saga.ProcessAsync(InventoryItem.DemoId, 1, 100m);

        await using var db = await factory.CreateDbContextAsync();
        var order = await db.Orders.SingleAsync(x => x.Id == id);
        var inventory = await db.InventoryItems.SingleAsync(x => x.Id == InventoryItem.DemoId);
        var payment = await db.Payments.SingleAsync(x => x.OrderId == id);
        Assert.Equal(OrderSagaStatus.Completed, (await db.OrderSagaStates.SingleAsync(x => x.OrderId == id)).Status);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(9, inventory.AvailableQuantity);
        Assert.Equal(1, inventory.ReservedQuantity);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(100m, payment.Amount);
    }

    [Fact]
    public async Task Payment_failure_compensates_saga_and_persists_cancelled_order_and_restored_inventory()
    {
        var id = await saga.ProcessAsync(InventoryItem.DemoId, 1, 100m, PaymentMode.Fail);

        await using var db = await factory.CreateDbContextAsync();
        var order = await db.Orders.SingleAsync(x => x.Id == id);
        var inventory = await db.InventoryItems.SingleAsync(x => x.Id == InventoryItem.DemoId);
        var payment = await db.Payments.SingleAsync(x => x.OrderId == id);
        Assert.Equal(OrderSagaStatus.Compensated, (await db.OrderSagaStates.SingleAsync(x => x.OrderId == id)).Status);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(10, inventory.AvailableQuantity);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal(100m, payment.Amount);
    }

    [Fact]
    public async Task Compensation_failure_persists_unresolved_saga_and_keeps_committed_reservation()
    {
        var id = await saga.ProcessAsync(InventoryItem.DemoId, 2, 100m, PaymentMode.Fail,
            inventoryReleaseMode: InventoryReleaseMode.Fail);

        await using var db = await factory.CreateDbContextAsync();
        var state = await db.OrderSagaStates.SingleAsync(x => x.OrderId == id);
        Assert.Equal(OrderSagaStatus.CompensationFailed, state.Status);
        Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync(x => x.Id == id)).Status);
        Assert.Equal(PaymentStatus.Failed, (await db.Payments.SingleAsync(x => x.OrderId == id)).Status);
        var inventory = await db.InventoryItems.SingleAsync();
        Assert.Equal(8, inventory.AvailableQuantity);
        Assert.Equal(2, inventory.ReservedQuantity);
        Assert.True(state.UpdatedAtUtc >= state.CreatedAtUtc);
    }

    [Fact]
    public async Task Release_refusal_occurs_after_committed_compensating_state_and_preserves_stock()
    {
        var id = await operations.CreateOrderAsync();
        var states = services.GetRequiredService<OrderSagaStateService>();
        var state = await states.StartAsync(id, default);
        await operations.ReserveInventoryAsync(id, InventoryItem.DemoId, 1);
        await operations.ProcessPaymentAsync(id, 100m, PaymentMode.Fail);
        state.BeginCompensation();
        await states.SaveAsync(state, default);
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert.Equal(OrderSagaStatus.Compensating, (await db.OrderSagaStates.SingleAsync()).Status);
            Assert.Equal(1, (await db.InventoryItems.SingleAsync()).ReservedQuantity);
            Assert.Equal(PaymentStatus.Failed, (await db.Payments.SingleAsync()).Status);
        }
        Assert.False(await operations.ReleaseInventoryAsync(id, InventoryItem.DemoId, 1,
            releaseMode: InventoryReleaseMode.Fail));
        await using var final = await factory.CreateDbContextAsync();
        Assert.Equal(1, (await final.InventoryItems.SingleAsync()).ReservedQuantity);
        Assert.Equal(OrderStatus.Pending, (await final.Orders.SingleAsync()).Status);
    }

    [Fact]
    public async Task Reservation_release_and_cancellation_commit_as_separate_business_operations()
    {
        var id = await operations.CreateOrderAsync();
        await operations.ReserveInventoryAsync(id, InventoryItem.DemoId, 2);
        await operations.ProcessPaymentAsync(id, 100m, PaymentMode.Fail);
        await using (var db = await factory.CreateDbContextAsync())
        {
            var inventory = await db.InventoryItems.SingleAsync();
            Assert.Equal(8, inventory.AvailableQuantity);
            Assert.Equal(2, inventory.ReservedQuantity);
            Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync()).Status);
            Assert.Equal(PaymentStatus.Failed, (await db.Payments.SingleAsync()).Status);
        }

        await operations.ReleaseInventoryAsync(id, InventoryItem.DemoId, 2);
        await using (var db = await factory.CreateDbContextAsync())
        {
            var inventory = await db.InventoryItems.SingleAsync();
            Assert.Equal(10, inventory.AvailableQuantity);
            Assert.Equal(0, inventory.ReservedQuantity);
            Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync()).Status);
            Assert.Equal(PaymentStatus.Failed, (await db.Payments.SingleAsync()).Status);
        }

        await operations.CancelOrderAsync(id);
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert.Equal(OrderStatus.Cancelled, (await db.Orders.SingleAsync()).Status);
            Assert.Equal(PaymentStatus.Failed, (await db.Payments.SingleAsync()).Status);
            Assert.Equal(10, (await db.InventoryItems.SingleAsync()).AvailableQuantity);
            Assert.Equal(0, (await db.InventoryItems.SingleAsync()).ReservedQuantity);
        }
    }

    [Fact]
    public async Task Every_operation_is_visible_from_a_fresh_context_before_the_next_operation()
    {
        var id = await operations.CreateOrderAsync();
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync(x => x.Id == id)).Status);
            Assert.Empty(await db.Payments.ToListAsync());
            Assert.Equal(10, (await db.InventoryItems.SingleAsync()).AvailableQuantity);
        }

        await operations.ReserveInventoryAsync(id, InventoryItem.DemoId, 1);
        await using (var db = await factory.CreateDbContextAsync())
        {
            var inventory = await db.InventoryItems.SingleAsync();
            Assert.Equal(9, inventory.AvailableQuantity);
            Assert.Equal(1, inventory.ReservedQuantity);
            Assert.Empty(await db.Payments.ToListAsync());
            Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync()).Status);
        }

        await operations.ProcessPaymentAsync(id, 100m);
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert.Equal(PaymentStatus.Succeeded, (await db.Payments.SingleAsync(x => x.OrderId == id)).Status);
            Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync()).Status);
        }

        await operations.CompleteOrderAsync(id);
        await using (var db = await factory.CreateDbContextAsync())
        {
            Assert.Equal(OrderStatus.Completed, (await db.Orders.SingleAsync()).Status);
        }
    }
}
