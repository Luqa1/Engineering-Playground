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
        var response = await client.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new { Quantity = 100 });

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
    }

    [Fact]
    public async Task Put_with_valid_quantity_returns_updated_item()
    {
        var response = await client.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new { Quantity = 90 });

        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<InventoryItemResponse>();

        Assert.NotNull(item);
        Assert.Equal(90, item.Quantity);
    }

    [Fact]
    public async Task Put_with_negative_quantity_returns_bad_request()
    {
        var response = await client.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new { Quantity = -1 });

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
        var updateResponse = await client.PutAsJsonAsync(
            $"/inventory/{DemoItemId}",
            new { Quantity = 75 });

        updateResponse.EnsureSuccessStatusCode();

        var persistedItem = await client.GetFromJsonAsync<InventoryItemResponse>(
            $"/inventory/{DemoItemId}");

        Assert.NotNull(persistedItem);
        Assert.Equal(75, persistedItem.Quantity);
    }
}
