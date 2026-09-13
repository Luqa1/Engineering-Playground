using EngineeringPlayground.DistributedLock.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EngineeringPlayground.DistributedLock.IntegrationTests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18-alpine")
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        return container.DisposeAsync().AsTask();
    }

    public DistributedLockDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DistributedLockDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new DistributedLockDbContext(options);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL";
}
