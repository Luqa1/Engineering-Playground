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
}
