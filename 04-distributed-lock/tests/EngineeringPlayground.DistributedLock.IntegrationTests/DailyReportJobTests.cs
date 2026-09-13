using EngineeringPlayground.DistributedLock.Domain;
using EngineeringPlayground.DistributedLock.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EngineeringPlayground.DistributedLock.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class DailyReportJobTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task JobExecutionCanBePersisted()
    {
        var startedAtUtc = new DateTimeOffset(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);
        var execution = new JobExecution("PersistenceTestJob", "test-worker", startedAtUtc);

        await using (var dbContext = fixture.CreateDbContext())
        {
            dbContext.JobExecutions.Add(execution);
            await dbContext.SaveChangesAsync();
        }

        await using var verificationContext = fixture.CreateDbContext();
        var persistedExecution = await verificationContext.JobExecutions.SingleAsync(
            candidate => candidate.Id == execution.Id);

        Assert.Equal("PersistenceTestJob", persistedExecution.JobName);
        Assert.Equal(startedAtUtc, persistedExecution.StartedAtUtc);
    }

    [Fact]
    public async Task ExecuteRecordsConfiguredWorkerInstance()
    {
        var workerInstance = $"configured-worker-{Guid.NewGuid():N}";

        var executionId = await ExecuteJobAsync(workerInstance);

        await using var verificationContext = fixture.CreateDbContext();
        var execution = await verificationContext.JobExecutions.SingleAsync(
            candidate => candidate.Id == executionId);
        Assert.Equal(workerInstance, execution.WorkerInstance);
    }

    [Fact]
    public async Task ExecuteOnceCreatesOneExecutionRecord()
    {
        var workerInstance = $"one-shot-worker-{Guid.NewGuid():N}";

        await ExecuteJobAsync(workerInstance);

        await using var verificationContext = fixture.CreateDbContext();
        var executionCount = await verificationContext.JobExecutions.CountAsync(
            candidate => candidate.JobName == DailyReportJob.JobName
                && candidate.WorkerInstance == workerInstance);
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task ExecutePersistsCompletionInformation()
    {
        var workerInstance = $"completion-worker-{Guid.NewGuid():N}";
        var timestamp = new DateTimeOffset(2026, 9, 13, 9, 30, 0, TimeSpan.Zero);

        var executionId = await ExecuteJobAsync(workerInstance, timestamp);

        await using var verificationContext = fixture.CreateDbContext();
        var execution = await verificationContext.JobExecutions.SingleAsync(
            candidate => candidate.Id == executionId);
        Assert.Equal(timestamp, execution.CompletedAtUtc);
    }

    private async Task<Guid> ExecuteJobAsync(
        string workerInstance,
        DateTimeOffset? timestamp = null)
    {
        await using var dbContext = fixture.CreateDbContext();
        var timeProvider = new FixedTimeProvider(
            timestamp ?? new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero));
        var job = new DailyReportJob(
            dbContext,
            new WorkerOptions(workerInstance),
            timeProvider,
            NullLogger<DailyReportJob>.Instance);

        return await job.ExecuteAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return timestamp;
        }
    }
}
