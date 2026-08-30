using EngineeringPlayground.OptimisticConcurrency.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngineeringPlayground.OptimisticConcurrency.IntegrationTests;

public sealed class LostUpdateTests(OptimisticConcurrencyApiFactory factory) :
    IClassFixture<OptimisticConcurrencyApiFactory>
{
    private static readonly Guid DemoItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Second_client_cannot_overwrite_first_clients_update_with_a_stale_version()
    {
        await using var clientAScope = factory.Services.CreateAsyncScope();
        await using var clientBScope = factory.Services.CreateAsyncScope();

        var clientAContext = clientAScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var clientBContext = clientBScope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var clientAItem = await clientAContext.InventoryItems.SingleAsync(item => item.Id == DemoItemId);
        var clientBItem = await clientBContext.InventoryItems.SingleAsync(item => item.Id == DemoItemId);

        Assert.Equal(100, clientAItem.Quantity);
        Assert.Equal(100, clientBItem.Quantity);
        Assert.Equal(clientAItem.Version, clientBItem.Version);

        var originalVersion = clientAItem.Version;

        clientAItem.Quantity = 90;
        clientBItem.Quantity = 80;

        await clientAContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => clientBContext.SaveChangesAsync());

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var persistedItem = await verificationContext.InventoryItems
            .AsNoTracking()
            .SingleAsync(item => item.Id == DemoItemId);

        Assert.Equal(90, persistedItem.Quantity);
        Assert.NotEqual(80, persistedItem.Quantity);
        Assert.Equal(originalVersion + 1, persistedItem.Version);
    }
}
