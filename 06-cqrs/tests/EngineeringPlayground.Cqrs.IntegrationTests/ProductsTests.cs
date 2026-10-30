using System.Net;
using System.Net.Http.Json;
using EngineeringPlayground.Cqrs.Domain;
using EngineeringPlayground.Cqrs.Infrastructure;
using Microsoft.AspNetCore.Hosting;
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
        Assert.Equal(price, returned!.Price);
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
        Assert.Equal(expectedQuantity, returned!.AvailableQuantity);
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
        await using var database = CreateDatabase();
        var persisted = await database.Products.SingleAsync(product => product.Id == KeyboardId);
        Assert.Equal(15, persisted.AvailableQuantity);
    }

    [Fact]
    public async Task NegativePriceIsRejectedWithoutChangingPersistedPrice()
    {
        var response = await _client.PutAsJsonAsync($"/products/{KeyboardId}/price", new { price = -1.00m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private CqrsDbContext CreateDatabase() => new(new DbContextOptionsBuilder<CqrsDbContext>()
        .UseNpgsql(_connectionString).Options);
}
