using System.Net;
using System.Net.Http.Json;
using EngineeringPlayground.Cqrs.Api.Responses;
using EngineeringPlayground.Cqrs.Domain;
using EngineeringPlayground.Cqrs.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EngineeringPlayground.Cqrs.IntegrationTests;

public sealed class ProductsTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly Guid KeyboardId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly string _databaseName = $"cqrs_test_{Guid.NewGuid():N}";
    private string _connectionString = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(postgres.Container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
        await command.ExecuteNonQueryAsync();

        _connectionString = new NpgsqlConnectionStringBuilder(postgres.Container.GetConnectionString())
        {
            Database = _databaseName
        }.ConnectionString;

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Cqrs", _connectionString);
        });
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_connectionString is not null)
        {
            using var pooledConnection = new NpgsqlConnection(_connectionString);
            NpgsqlConnection.ClearPool(pooledConnection);
        }

        await using var connection = new NpgsqlConnection(postgres.Container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task GetReturnsSeededProducts()
    {
        var products = await _client.GetFromJsonAsync<List<Product>>("/products");

        Assert.NotNull(products);
        Assert.Equal(3, products.Count);
        Assert.Contains(products, product => product.Id == KeyboardId && product.Name == "Mechanical Keyboard"
            && product.Price == 299.00m && product.AvailableQuantity == 15);
        Assert.Contains(products, product => product.Id == Guid.Parse("22222222-2222-2222-2222-222222222222")
            && product.Name == "Wireless Mouse" && product.Price == 129.00m && product.AvailableQuantity == 30);
        Assert.Contains(products, product => product.Id == Guid.Parse("33333333-3333-3333-3333-333333333333")
            && product.Name == "USB-C Dock" && product.Price == 449.00m && product.AvailableQuantity == 8);
    }

    [Fact]
    public async Task GetReturnsOneProduct()
    {
        var product = await _client.GetFromJsonAsync<Product>($"/products/{KeyboardId}");

        Assert.NotNull(product);
        Assert.Equal(KeyboardId, product.Id);
        Assert.Equal("Mechanical Keyboard", product.Name);
    }

    [Theory]
    [InlineData(319)]
    [InlineData(0)]
    public async Task UpdatingPricePersistsChange(decimal price)
    {
        var response = await _client.PutAsJsonAsync($"/products/{KeyboardId}/price", new { price });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = await response.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(returned);
        Assert.Equal(KeyboardId, returned.Id);
        Assert.Equal("Mechanical Keyboard", returned.Name);
        Assert.Equal(price, returned.Price);
        Assert.Equal(15, returned.AvailableQuantity);
        await using var database = CreateDatabase();
        var persisted = await database.Products.SingleAsync(product => product.Id == KeyboardId);
        Assert.Equal(price, persisted.Price);
        Assert.Equal(15, persisted.AvailableQuantity);
    }

    [Theory]
    [InlineData(-3, 12)]
    [InlineData(5, 20)]
    [InlineData(-15, 0)]
    public async Task AdjustingInventoryPersistsQuantity(int delta, int expectedQuantity)
    {
        var response = await _client.PutAsJsonAsync($"/products/{KeyboardId}/inventory", new { delta });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var returned = await response.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(returned);
        Assert.Equal(KeyboardId, returned.Id);
        Assert.Equal("Mechanical Keyboard", returned.Name);
        Assert.Equal(299.00m, returned.Price);
        Assert.Equal(expectedQuantity, returned.AvailableQuantity);
        await using var database = CreateDatabase();
        var persisted = await database.Products.SingleAsync(product => product.Id == KeyboardId);
        Assert.Equal(expectedQuantity, persisted.AvailableQuantity);
        Assert.Equal(299.00m, persisted.Price);
    }

    [Theory]
    [InlineData(-16)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public async Task InvalidInventoryIsRejectedWithoutChangingPersistedQuantity(int delta)
    {
        var response = await _client.PutAsJsonAsync($"/products/{KeyboardId}/inventory", new { delta });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Contains("The resulting inventory quantity must be between zero and Int32.MaxValue.", problem.Detail);
        await using var database = CreateDatabase();
        var persisted = await database.Products.SingleAsync(product => product.Id == KeyboardId);
        Assert.Equal(15, persisted.AvailableQuantity);
    }

    [Fact]
    public async Task NegativePriceIsRejectedWithoutChangingPersistedPrice()
    {
        var response = await _client.PutAsJsonAsync($"/products/{KeyboardId}/price", new { price = -1.00m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Contains("price", problem.Detail);
        await using var database = CreateDatabase();
        var persisted = await database.Products.SingleAsync(product => product.Id == KeyboardId);
        Assert.Equal(299.00m, persisted.Price);
    }

    [Theory]
    [InlineData("price")]
    [InlineData("inventory")]
    public async Task MissingRequestValueIsRejected(string operation)
    {
        var response = await _client.PutAsJsonAsync($"/products/{KeyboardId}/{operation}", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var database = CreateDatabase();
        var persisted = await database.Products.SingleAsync(product => product.Id == KeyboardId);
        Assert.Equal(299.00m, persisted.Price);
        Assert.Equal(15, persisted.AvailableQuantity);
    }

    [Fact]
    public async Task UnknownProductReturnsNotFoundForReadsAndWrites()
    {
        const string unknown = "/products/99999999-9999-9999-9999-999999999999";

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(unknown)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PutAsJsonAsync($"{unknown}/price", new { price = 10.00m })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PutAsJsonAsync($"{unknown}/inventory", new { delta = 1 })).StatusCode);
    }

    [Fact]
    public async Task ReapplyingMigrationsDoesNotDuplicateOrResetSeedData()
    {
        var response = await _client.PutAsJsonAsync($"/products/{KeyboardId}/price", new { price = 319.00m });
        response.EnsureSuccessStatusCode();

        await using (var database = CreateDatabase())
        {
            await database.Database.MigrateAsync();
        }

        await using var verification = CreateDatabase();
        Assert.Equal(3, await verification.Products.CountAsync());
        Assert.Equal(319.00m, (await verification.Products.SingleAsync(product => product.Id == KeyboardId)).Price);
    }

    [Fact]
    public async Task DashboardSummarizesPersistedProductsAtStockBoundaries()
    {
        await ReplaceProductsAsync(
            new Product(Guid.Parse("10000000-0000-0000-0000-000000000001"), "Empty", 99.99m, 0),
            new Product(Guid.Parse("10000000-0000-0000-0000-000000000002"), "One left", 12.34m, 1),
            new Product(Guid.Parse("10000000-0000-0000-0000-000000000003"), "Five left", 2.55m, 5),
            new Product(Guid.Parse("10000000-0000-0000-0000-000000000004"), "Six left", 10.01m, 6),
            new Product(Guid.Parse("10000000-0000-0000-0000-000000000005"), "Free item", 0m, 2));

        var dashboard = await _client.GetFromJsonAsync<InventoryDashboardResponse>("/inventory/dashboard");

        Assert.NotNull(dashboard);
        Assert.Equal(5, dashboard.TotalProducts);
        Assert.Equal(1, dashboard.OutOfStockCount);
        Assert.Equal(3, dashboard.LowStockCount);
        // 99.99 * 0 + 12.34 * 1 + 2.55 * 5 + 10.01 * 6 + 0 * 2 = 85.15.
        Assert.Equal(85.15m, dashboard.TotalInventoryValue);
    }

    [Fact]
    public async Task DashboardAttentionListClassifiesValuesAndSortsByStatusNameThenId()
    {
        var emptyAlpha = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var emptyZulu = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var lowAlphaFirst = Guid.Parse("20000000-0000-0000-0000-000000000003");
        var lowAlphaSecond = Guid.Parse("20000000-0000-0000-0000-000000000004");
        var lowZulu = Guid.Parse("20000000-0000-0000-0000-000000000005");
        // Deliberately scramble insertion order and give same-name products different quantities.
        await ReplaceProductsAsync(
            new Product(lowAlphaSecond, "Alpha", 2.50m, 1),
            new Product(lowZulu, "Zulu", 12.34m, 1),
            new Product(emptyZulu, "Zulu", 99.99m, 0),
            new Product(Guid.Parse("20000000-0000-0000-0000-000000000006"), "In stock", 10m, 6),
            new Product(lowAlphaFirst, "Alpha", 1.25m, 5),
            new Product(emptyAlpha, "Alpha", 5m, 0));

        var dashboard = await _client.GetFromJsonAsync<InventoryDashboardResponse>("/inventory/dashboard");

        Assert.NotNull(dashboard);
        InventoryAttentionItemResponse[] expected =
        [
            new(emptyAlpha, "Alpha", 0, "OutOfStock", 0m),
            new(emptyZulu, "Zulu", 0, "OutOfStock", 0m),
            new(lowAlphaFirst, "Alpha", 5, "LowStock", 6.25m),
            new(lowAlphaSecond, "Alpha", 1, "LowStock", 2.50m),
            new(lowZulu, "Zulu", 1, "LowStock", 12.34m)
        ];
        Assert.Equal(expected, dashboard.ProductsNeedingAttention);
    }

    [Theory]
    [InlineData(-1, 5, 1, 126.85)]
    [InlineData(-6, 0, 0, 0)]
    [InlineData(1, 7, 0, 177.59)]
    public async Task DashboardReflectsPersistedPriceAndInventoryWrites(
        int delta, int expectedQuantity, int expectedLowStockCount, decimal expectedValue)
    {
        await ReplaceProductsAsync(new Product(KeyboardId, "Mechanical Keyboard", 10m, 6));
        var before = await _client.GetFromJsonAsync<InventoryDashboardResponse>("/inventory/dashboard");
        Assert.NotNull(before);
        Assert.Equal(60m, before.TotalInventoryValue);
        Assert.Empty(before.ProductsNeedingAttention);

        var priceResponse = await _client.PutAsJsonAsync($"/products/{KeyboardId}/price", new { price = 25.37m });
        Assert.Equal(HttpStatusCode.OK, priceResponse.StatusCode);
        var inventoryResponse = await _client.PutAsJsonAsync($"/products/{KeyboardId}/inventory", new { delta });
        Assert.Equal(HttpStatusCode.OK, inventoryResponse.StatusCode);

        var dashboard = await _client.GetFromJsonAsync<InventoryDashboardResponse>("/inventory/dashboard");

        Assert.NotNull(dashboard);
        Assert.Equal(1, dashboard.TotalProducts);
        Assert.Equal(expectedQuantity == 0 ? 1 : 0, dashboard.OutOfStockCount);
        Assert.Equal(expectedLowStockCount, dashboard.LowStockCount);
        Assert.Equal(expectedValue, dashboard.TotalInventoryValue);
        if (expectedQuantity <= 5)
        {
            var attentionItem = Assert.Single(dashboard.ProductsNeedingAttention);
            Assert.Equal(new InventoryAttentionItemResponse(KeyboardId, "Mechanical Keyboard", expectedQuantity,
                expectedQuantity == 0 ? "OutOfStock" : "LowStock", expectedValue), attentionItem);
        }
        else
        {
            Assert.Empty(dashboard.ProductsNeedingAttention);
        }

        await using var verification = CreateDatabase();
        var persisted = await verification.Products.SingleAsync(product => product.Id == KeyboardId);
        Assert.Equal(25.37m, persisted.Price);
        Assert.Equal(expectedQuantity, persisted.AvailableQuantity);
    }

    [Fact]
    public async Task DashboardReturnsZeroSummaryAndEmptyAttentionListForEmptyCatalog()
    {
        await ReplaceProductsAsync();

        var dashboard = await _client.GetFromJsonAsync<InventoryDashboardResponse>("/inventory/dashboard");

        Assert.NotNull(dashboard);
        Assert.Equal(0, dashboard.TotalProducts);
        Assert.Equal(0, dashboard.OutOfStockCount);
        Assert.Equal(0, dashboard.LowStockCount);
        Assert.Equal(0m, dashboard.TotalInventoryValue);
        Assert.Empty(dashboard.ProductsNeedingAttention);
    }

    private async Task ReplaceProductsAsync(params Product[] products)
    {
        await using var database = CreateDatabase();
        await database.Products.ExecuteDeleteAsync();
        database.Products.AddRange(products);
        await database.SaveChangesAsync();
    }

    private CqrsDbContext CreateDatabase() => new(new DbContextOptionsBuilder<CqrsDbContext>()
        .UseNpgsql(_connectionString).Options);
}
