using EngineeringPlayground.DistributedLock.Infrastructure;
using EngineeringPlayground.DistributedLock.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace EngineeringPlayground.DistributedLock.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class DailyReportJobRunnerTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task CompetingWorkersPersistOnlyTheLockOwnersExecution()
    {
        var executionKey = $"protected-window-{Guid.NewGuid():N}";
        var workerAGate = new FirstSaveGate();
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);

        await using var workerAContext = fixture.CreateDbContext(workerAGate);
        await using var workerBContext = fixture.CreateDbContext();
        var workerA = CreateRunner(dataSource, workerAContext, "worker-a", executionKey);
        var workerB = CreateRunner(dataSource, workerBContext, "worker-b", executionKey);

        var workerAExecution = workerA.TryExecuteAsync();
        Guid? workerBExecution;

        try
        {
            await workerAGate.WaitUntilFirstSaveAsync();
            workerBExecution = await workerB.TryExecuteAsync();
            Assert.Null(workerBExecution);
        }
        finally
        {
            workerAGate.AllowSave();
        }

        var workerAExecutionId = await workerAExecution;
        Assert.NotNull(workerAExecutionId);

        await using var verificationContext = fixture.CreateDbContext();
        var executions = await verificationContext.JobExecutions
            .Where(candidate => candidate.JobName == DailyReportJob.JobName
                && candidate.ExecutionKey == executionKey)
            .ToListAsync();

        var execution = Assert.Single(executions);
        Assert.Equal(workerAExecutionId, execution.Id);
        Assert.Equal("worker-a", execution.WorkerInstance);
    }

    private static DailyReportJobRunner CreateRunner(
        NpgsqlDataSource dataSource,
        DistributedLockDbContext dbContext,
        string workerInstance,
        string executionKey)
    {
        var options = new WorkerOptions(workerInstance, executionKey);
        var job = new DailyReportJob(
            dbContext,
            options,
            TimeProvider.System,
            NullLogger<DailyReportJob>.Instance);

        return new DailyReportJobRunner(
            new PostgresAdvisoryLock(dataSource),
            job,
            options,
            NullLogger<DailyReportJobRunner>.Instance);
    }

    private sealed class FirstSaveGate : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource firstSaveStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource saveAllowed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int hasArrived;

        public Task WaitUntilFirstSaveAsync()
        {
            return firstSaveStarted.Task;
        }

        public void AllowSave()
        {
            saveAllowed.TrySetResult();
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref hasArrived, 1) == 0)
            {
                firstSaveStarted.SetResult();
                await saveAllowed.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}
