using System.Net;
using System.Net.Http.Json;
using EngineeringPlayground.Saga.Api.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
    private WebApplicationFactory<Program> application = null!;
    private HttpClient client = null!;
    private OrderOperations operations = null!;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        services = new ServiceCollection()
            .AddLogging()
            .AddOrderProcessing(postgres.GetConnectionString())
            .BuildServiceProvider();
        factory = services.GetRequiredService<IDbContextFactory<OrderDbContext>>();

        operations = services.GetRequiredService<OrderOperations>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
        application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Development")
                .UseSetting("ConnectionStrings:Orders", postgres.GetConnectionString()));
        client = application.CreateClient();
    }

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (application is not null) await application.DisposeAsync();
        if (services is not null) await services.DisposeAsync();
        await postgres.DisposeAsync();
    }

    [Fact]
    public async Task Successful_processing_completes_order_and_saga()
    {
        var id = await ProcessThroughHttpAsync(new CreateOrderRequest(InventoryItem.DemoId, 1, 100m),
            HttpStatusCode.Created, "Completed", "Succeeded", "Completed");

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
    public async Task Payment_failure_releases_inventory_cancels_order_and_marks_saga_compensated()
    {
        var id = await ProcessThroughHttpAsync(new CreateOrderRequest(InventoryItem.DemoId, 1, 100m, "Fail"),
            HttpStatusCode.UnprocessableEntity, "Cancelled", "Failed", "Compensated");

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
    public async Task Compensation_failure_leaves_inventory_reserved_and_marks_saga_compensation_failed()
    {
        var id = await ProcessThroughHttpAsync(new CreateOrderRequest(InventoryItem.DemoId, 2, 100m, "Fail", "Fail"),
            HttpStatusCode.UnprocessableEntity, "Pending", "Failed", "CompensationFailed");

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
    [Fact]
    public async Task Cancelled_order_cannot_be_completed_and_remains_cancelled()
    {
        var id = await operations.CreateOrderAsync();
        await operations.CancelOrderAsync(id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.CompleteOrderAsync(id));

        await using var db = await factory.CreateDbContextAsync();
        Assert.Equal(OrderStatus.Cancelled, (await db.Orders.SingleAsync(x => x.Id == id)).Status);
    }

    private async Task<Guid> ProcessThroughHttpAsync(CreateOrderRequest request, HttpStatusCode expectedStatus,
        string orderStatus, string paymentStatus, string sagaStatus)
    {
        // Each test owns a migrated PostgreSQL container with known stock, independent of test order.
        await using (var initial = await factory.CreateDbContextAsync())
        {
            var inventory = await initial.InventoryItems.SingleAsync(x => x.Id == InventoryItem.DemoId);
            Assert.Equal(10, inventory.AvailableQuantity);
            Assert.Equal(0, inventory.ReservedQuantity);
        }

        using var response = await client.PostAsJsonAsync("/orders", request);
        Assert.True(response.StatusCode == expectedStatus,
            $"Expected {expectedStatus}, received {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var result = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.Equal(orderStatus, result.OrderStatus);
        Assert.Equal(paymentStatus, result.PaymentStatus);
        Assert.Equal(sagaStatus, result.SagaStatus);
        if (expectedStatus == HttpStatusCode.Created)
            Assert.EndsWith($"/orders/{result.OrderId}", response.Headers.Location?.ToString());

        using var read = await client.GetAsync($"/orders/{result.OrderId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(result, await read.Content.ReadFromJsonAsync<OrderResponse>());
        return result.OrderId;
    }
}
