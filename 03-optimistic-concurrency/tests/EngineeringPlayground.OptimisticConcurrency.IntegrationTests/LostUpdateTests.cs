using EngineeringPlayground.OptimisticConcurrency.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngineeringPlayground.OptimisticConcurrency.IntegrationTests;

public sealed class LostUpdateTests(OptimisticConcurrencyApiFactory factory) :
    IClassFixture<OptimisticConcurrencyApiFactory>
{
    private static readonly Guid DemoItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Second_client_silently_overwrites_first_clients_update()
    {
        await using var clientAScope = factory.Services.CreateAsyncScope();
        await using var clientBScope = factory.Services.CreateAsyncScope();

        var clientAContext = clientAScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var clientBContext = clientBScope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var clientAItem = await clientAContext.InventoryItems.SingleAsync(item => item.Id == DemoItemId);
        var clientBItem = await clientBContext.InventoryItems.SingleAsync(item => item.Id == DemoItemId);

        Assert.Equal(100, clientAItem.Quantity);
        Assert.Equal(100, clientBItem.Quantity);

        clientAItem.Quantity = 90;
        clientBItem.Quantity = 80;

        var clientASaveException = await Record.ExceptionAsync(async () =>
        {
            await clientAContext.SaveChangesAsync();
        });

        var clientBSaveException = await Record.ExceptionAsync(async () =>
        {
            await clientBContext.SaveChangesAsync();
        });

        Assert.Null(clientASaveException);
        Assert.Null(clientBSaveException);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var persistedItem = await verificationContext.InventoryItems
            .AsNoTracking()
            .SingleAsync(item => item.Id == DemoItemId);

        Assert.Equal(80, persistedItem.Quantity);
    }
}
