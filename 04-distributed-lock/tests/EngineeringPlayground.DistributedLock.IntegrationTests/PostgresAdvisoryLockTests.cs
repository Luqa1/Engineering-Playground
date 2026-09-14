using EngineeringPlayground.DistributedLock.Infrastructure;
using EngineeringPlayground.DistributedLock.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace EngineeringPlayground.DistributedLock.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgresAdvisoryLockTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task IndependentPostgreSqlSessionsEnforceExclusiveOwnershipAndAllowLaterAcquisition()
    {
        var executionKey = $"advisory-lock-window-{Guid.NewGuid():N}";
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);
        var participantA = new PostgresAdvisoryLock(dataSource);
        var participantB = new PostgresAdvisoryLock(dataSource);

        var participantALease = await participantA.TryAcquireAsync(
            DailyReportJob.JobName,
            executionKey);
        Assert.NotNull(participantALease);

        try
        {
            var participantBLeaseWhileHeld = await participantB.TryAcquireAsync(
                DailyReportJob.JobName,
                executionKey);
            Assert.Null(participantBLeaseWhileHeld);

            await using var dbContext = fixture.CreateDbContext();
            var job = new DailyReportJob(
                dbContext,
                new WorkerOptions("participant-a", executionKey),
                TimeProvider.System,
                NullLogger<DailyReportJob>.Instance);

            await job.ExecuteAsync();
        }
        finally
        {
            await participantALease.DisposeAsync();
        }

        var participantBLeaseAfterRelease = await participantB.TryAcquireAsync(
            DailyReportJob.JobName,
            executionKey);
        Assert.NotNull(participantBLeaseAfterRelease);
        await participantBLeaseAfterRelease.DisposeAsync();
    }

    [Fact]
    public async Task OwnerSessionRetainsLockAndAnotherSessionCannotReleaseIt()
    {
        var lockKey = PostgresAdvisoryLockKey.Create(
            DailyReportJob.JobName,
            $"owner-session-window-{Guid.NewGuid():N}");
        await using var connectionA = CreateNonPooledConnection();
        await using var connectionB = CreateNonPooledConnection();
        await connectionA.OpenAsync();
        await connectionB.OpenAsync();

        Assert.NotEqual(
            await GetBackendProcessIdAsync(connectionA),
            await GetBackendProcessIdAsync(connectionB));
        Assert.True(await TryAcquireAsync(connectionA, lockKey));

        try
        {
            Assert.False(await TryAcquireAsync(connectionB, lockKey));
            Assert.False(await ReleaseAsync(connectionB, lockKey));
            Assert.False(await TryAcquireAsync(connectionB, lockKey));
        }
        finally
        {
            await ReleaseAsync(connectionA, lockKey);
        }
    }

    [Fact]
    public async Task ExplicitReleaseAllowsAnotherSessionToAcquireLock()
    {
        var lockKey = PostgresAdvisoryLockKey.Create(
            DailyReportJob.JobName,
            $"explicit-release-window-{Guid.NewGuid():N}");
        await using var connectionA = CreateNonPooledConnection();
        await using var connectionB = CreateNonPooledConnection();
        await connectionA.OpenAsync();
        await connectionB.OpenAsync();

        Assert.NotEqual(
            await GetBackendProcessIdAsync(connectionA),
            await GetBackendProcessIdAsync(connectionB));
        Assert.True(await TryAcquireAsync(connectionA, lockKey));
        Assert.False(await TryAcquireAsync(connectionB, lockKey));

        Assert.True(await ReleaseAsync(connectionA, lockKey));
        Assert.True(await TryAcquireAsync(connectionB, lockKey));
        Assert.True(await ReleaseAsync(connectionB, lockKey));
    }

    [Fact]
    public async Task EndingOwnerSessionAutomaticallyReleasesLock()
    {
        var lockKey = PostgresAdvisoryLockKey.Create(
            DailyReportJob.JobName,
            $"session-loss-window-{Guid.NewGuid():N}");
        await using var connectionA = CreateNonPooledConnection();
        await using var connectionB = CreateNonPooledConnection();
        await connectionA.OpenAsync();
        await connectionB.OpenAsync();

        Assert.NotEqual(
            await GetBackendProcessIdAsync(connectionA),
            await GetBackendProcessIdAsync(connectionB));
        Assert.True(await TryAcquireAsync(connectionA, lockKey));
        Assert.False(await TryAcquireAsync(connectionB, lockKey));

        await connectionA.DisposeAsync();

        Assert.True(await TryAcquireAsync(connectionB, lockKey));
        Assert.True(await ReleaseAsync(connectionB, lockKey));
    }

    private NpgsqlConnection CreateNonPooledConnection()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Pooling = false
        };

        return new NpgsqlConnection(connectionString.ConnectionString);
    }

    private static async Task<int> GetBackendProcessIdAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT pg_backend_pid();", connection);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> TryAcquireAsync(NpgsqlConnection connection, long lockKey)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_try_advisory_lock(@lockKey);",
            connection);
        command.Parameters.AddWithValue("lockKey", lockKey);

        return await command.ExecuteScalarAsync() is true;
    }

    private static async Task<bool> ReleaseAsync(NpgsqlConnection connection, long lockKey)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_unlock(@lockKey);",
            connection);
        command.Parameters.AddWithValue("lockKey", lockKey);

        return await command.ExecuteScalarAsync() is true;
    }
}
