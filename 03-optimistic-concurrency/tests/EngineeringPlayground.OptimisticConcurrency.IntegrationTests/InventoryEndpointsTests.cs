using System.Net;
using System.Net.Http.Json;
using EngineeringPlayground.OptimisticConcurrency.Api.Contracts;

namespace EngineeringPlayground.OptimisticConcurrency.IntegrationTests;

public sealed class InventoryEndpointsTests :
    IClassFixture<OptimisticConcurrencyApiFactory>,
    IAsyncLifetime
{
    private static readonly Guid DemoItemId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly HttpClient client;

    public InventoryEndpointsTests(OptimisticConcurrencyApiFactory factory)
    {
        client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var currentItem = await GetDemoItemAsync();
        var response = await client.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new { Quantity = 100, currentItem.Version });

        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync());
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Get_returns_seeded_inventory_item()
    {
        var item = await client.GetFromJsonAsync<InventoryItemResponse>(
            $"/inventory/{DemoItemId}");

        Assert.NotNull(item);
        Assert.Equal(DemoItemId, item.Id);
        Assert.Equal("Demo Item", item.Name);
        Assert.Equal(100, item.Quantity);
        Assert.True(item.Version > 0);
    }

    [Fact]
    public async Task Put_with_current_version_returns_updated_item_with_new_version()
    {
        var currentItem = await GetDemoItemAsync();
        var response = await client.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new { Quantity = 90, currentItem.Version });

        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<InventoryItemResponse>();

        Assert.NotNull(item);
        Assert.Equal(90, item.Quantity);
        Assert.Equal(currentItem.Version + 1, item.Version);
    }

    [Fact]
    public async Task Put_with_stale_version_returns_conflict_with_current_state_and_client_can_recover()
    {
        var clientAItem = await GetDemoItemAsync();
        var clientBItem = await GetDemoItemAsync();

        Assert.Equal(clientAItem.Version, clientBItem.Version);

        var clientAResponse = await client.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new { Quantity = 90, clientAItem.Version });

        clientAResponse.EnsureSuccessStatusCode();
        var updatedByClientA = await clientAResponse.Content
            .ReadFromJsonAsync<InventoryItemResponse>();
        Assert.NotNull(updatedByClientA);

        var clientBResponse = await client.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new { Quantity = 80, clientBItem.Version });

        Assert.Equal(HttpStatusCode.Conflict, clientBResponse.StatusCode);

        var conflict = await clientBResponse.Content
            .ReadFromJsonAsync<InventoryItemConflictResponse>();
        Assert.NotNull(conflict);
        Assert.Equal("The inventory item was modified by another client.", conflict.Message);
        Assert.Equal(DemoItemId, conflict.Current.Id);
        Assert.Equal("Demo Item", conflict.Current.Name);
        Assert.Equal(90, conflict.Current.Quantity);
        Assert.Equal(updatedByClientA.Version, conflict.Current.Version);

        var persistedAfterConflict = await GetDemoItemAsync();
        Assert.Equal(90, persistedAfterConflict.Quantity);
        Assert.Equal(updatedByClientA.Version, persistedAfterConflict.Version);

        var recoveryResponse = await client.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new { Quantity = 80, conflict.Current.Version });

        recoveryResponse.EnsureSuccessStatusCode();
        var recoveredItem = await recoveryResponse.Content
            .ReadFromJsonAsync<InventoryItemResponse>();
        Assert.NotNull(recoveredItem);
        Assert.Equal(80, recoveredItem.Quantity);
        Assert.Equal(conflict.Current.Version + 1, recoveredItem.Version);
    }

    [Fact]
    public async Task Put_with_negative_quantity_returns_bad_request()
    {
        var currentItem = await GetDemoItemAsync();
        var response = await client.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new { Quantity = -1, currentItem.Version });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_with_invalid_version_returns_bad_request()
    {
        var response = await client.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new { Quantity = 90, Version = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_for_missing_inventory_item_returns_not_found()
    {
        var response = await client.GetAsync($"/inventory/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_persists_updated_quantity()
    {
        var currentItem = await GetDemoItemAsync();
        var updateResponse = await client.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new { Quantity = 75, currentItem.Version });

        updateResponse.EnsureSuccessStatusCode();

        var persistedItem = await client.GetFromJsonAsync<InventoryItemResponse>(
            $"/inventory/{DemoItemId}");

        Assert.NotNull(persistedItem);
        Assert.Equal(75, persistedItem.Quantity);
        Assert.Equal(currentItem.Version + 1, persistedItem.Version);
    }

    [Fact]
    public async Task Put_for_missing_inventory_item_returns_not_found()
    {
        var response = await client.PutAsJsonAsync(
            $"/inventory/{Guid.NewGuid()}",
            new { Quantity = 90, Version = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<InventoryItemResponse> GetDemoItemAsync()
    {
        var item = await client.GetFromJsonAsync<InventoryItemResponse>(
            $"/inventory/{DemoItemId}");

        return item ?? throw new InvalidOperationException("The demo item was not found.");
    }
}
