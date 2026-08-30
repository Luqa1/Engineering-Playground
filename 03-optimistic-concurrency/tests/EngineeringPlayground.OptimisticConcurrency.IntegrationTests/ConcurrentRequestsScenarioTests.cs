using System.Net;
using System.Net.Http.Json;
using EngineeringPlayground.OptimisticConcurrency.Api.Contracts;

namespace EngineeringPlayground.OptimisticConcurrency.IntegrationTests;

public sealed class ConcurrentRequestsScenarioTests :
    IClassFixture<OptimisticConcurrencyApiFactory>,
    IAsyncLifetime
{
    private static readonly Guid DemoItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly HttpClient clientA;
    private readonly HttpClient clientB;

    public ConcurrentRequestsScenarioTests(OptimisticConcurrencyApiFactory factory)
    {
        clientA = factory.CreateClient();
        clientB = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var currentItem = await GetDemoItemAsync(clientA);
        var response = await clientA.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new UpdateInventoryItemRequest(Quantity: 100, currentItem.Version));

        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync());
    }

    public Task DisposeAsync()
    {
        clientA.Dispose();
        clientB.Dispose();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Stale_client_observes_conflict_then_makes_an_explicit_new_update()
    {
        var itemReadByClientA = await GetDemoItemAsync(clientA);
        var itemReadByClientB = await GetDemoItemAsync(clientB);

        Assert.Equal(100, itemReadByClientA.Quantity);
        Assert.Equal(100, itemReadByClientB.Quantity);
        Assert.Equal(itemReadByClientA.Version, itemReadByClientB.Version);

        var clientAResponse = await clientA.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new UpdateInventoryItemRequest(Quantity: 90, itemReadByClientA.Version));

        clientAResponse.EnsureSuccessStatusCode();
        var itemUpdatedByClientA = await clientAResponse.Content
            .ReadFromJsonAsync<InventoryItemResponse>();

        Assert.NotNull(itemUpdatedByClientA);
        Assert.Equal(90, itemUpdatedByClientA.Quantity);
        Assert.Equal(itemReadByClientA.Version + 1, itemUpdatedByClientA.Version);

        var staleClientBResponse = await clientB.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new UpdateInventoryItemRequest(Quantity: 80, itemReadByClientB.Version));

        Assert.Equal(HttpStatusCode.Conflict, staleClientBResponse.StatusCode);

        var conflict = await staleClientBResponse.Content
            .ReadFromJsonAsync<InventoryItemConflictResponse>();

        Assert.NotNull(conflict);
        Assert.Equal(90, conflict.Current.Quantity);
        Assert.Equal(itemUpdatedByClientA.Version, conflict.Current.Version);

        var currentItemObservedByClientB = await GetDemoItemAsync(clientB);

        Assert.Equal(90, currentItemObservedByClientB.Quantity);
        Assert.Equal(itemUpdatedByClientA.Version, currentItemObservedByClientB.Version);

        var deliberateClientBResponse = await clientB.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new UpdateInventoryItemRequest(Quantity: 80, currentItemObservedByClientB.Version));

        deliberateClientBResponse.EnsureSuccessStatusCode();
        var itemUpdatedByClientB = await deliberateClientBResponse.Content
            .ReadFromJsonAsync<InventoryItemResponse>();

        Assert.NotNull(itemUpdatedByClientB);
        Assert.Equal(80, itemUpdatedByClientB.Quantity);
        Assert.Equal(currentItemObservedByClientB.Version + 1, itemUpdatedByClientB.Version);

        var finalItem = await GetDemoItemAsync(clientA);

        Assert.Equal(80, finalItem.Quantity);
        Assert.Equal(itemUpdatedByClientB.Version, finalItem.Version);
    }

    private static async Task<InventoryItemResponse> GetDemoItemAsync(HttpClient client)
    {
        var item = await client.GetFromJsonAsync<InventoryItemResponse>(
            $"/inventory/{DemoItemId}");

        return item ?? throw new InvalidOperationException("The demo item was not found.");
    }
}
