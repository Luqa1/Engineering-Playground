using EngineeringPlayground.DistributedLock.Domain;
using EngineeringPlayground.DistributedLock.Infrastructure;
using EngineeringPlayground.DistributedLock.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace EngineeringPlayground.DistributedLock.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class DailyReportJobTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task JobExecutionCanBePersisted()
    {
        var startedAtUtc = new DateTimeOffset(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);
        var execution = new JobExecution(
            "persistence-test-job",
            "persistence-test-window",
            "test-worker",
            startedAtUtc);

        await using (var dbContext = fixture.CreateDbContext())
        {
            dbContext.JobExecutions.Add(execution);
            await dbContext.SaveChangesAsync();
        }

        await using var verificationContext = fixture.CreateDbContext();
        var persistedExecution = await verificationContext.JobExecutions.SingleAsync(
            candidate => candidate.Id == execution.Id);

        Assert.Equal("persistence-test-job", persistedExecution.JobName);
        Assert.Equal("persistence-test-window", persistedExecution.ExecutionKey);
        Assert.Equal(startedAtUtc, persistedExecution.StartedAtUtc);
    }

    [Fact]
    public async Task ExecuteRecordsConfiguredWorkerInstance()
    {
        var workerInstance = $"configured-worker-{Guid.NewGuid():N}";

        var executionId = await ExecuteJobAsync(workerInstance, $"window-{Guid.NewGuid():N}");

        await using var verificationContext = fixture.CreateDbContext();
        var execution = await verificationContext.JobExecutions.SingleAsync(
            candidate => candidate.Id == executionId);
        Assert.Equal(workerInstance, execution.WorkerInstance);
    }

    [Fact]
    public async Task ExecuteOnceCreatesOneExecutionRecord()
    {
        var workerInstance = $"one-shot-worker-{Guid.NewGuid():N}";
        var executionKey = $"window-{Guid.NewGuid():N}";

        await ExecuteJobAsync(workerInstance, executionKey);

        await using var verificationContext = fixture.CreateDbContext();
        var executionCount = await verificationContext.JobExecutions.CountAsync(
            candidate => candidate.JobName == DailyReportJob.JobName
                && candidate.ExecutionKey == executionKey);
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task ExecutePersistsCompletionInformation()
    {
        var workerInstance = $"completion-worker-{Guid.NewGuid():N}";
        var executionKey = $"window-{Guid.NewGuid():N}";
        var timestamp = new DateTimeOffset(2026, 9, 13, 9, 30, 0, TimeSpan.Zero);

        var executionId = await ExecuteJobAsync(workerInstance, executionKey, timestamp);

        await using var verificationContext = fixture.CreateDbContext();
        var execution = await verificationContext.JobExecutions.SingleAsync(
            candidate => candidate.Id == executionId);
        Assert.Equal(timestamp, execution.CompletedAtUtc);
    }

    [Fact]
    public async Task TwoIndependentWorkersExecuteTheSameLogicalJobTwice()
    {
        var executionKey = $"duplicate-window-{Guid.NewGuid():N}";
        var rendezvous = new ExecutionRendezvous(2);

        await using var workerAContext = fixture.CreateDbContext(
            new FirstSaveRendezvousInterceptor(rendezvous));
        await using var workerBContext = fixture.CreateDbContext(
            new FirstSaveRendezvousInterceptor(rendezvous));

        var workerA = CreateJob(workerAContext, "worker-a", executionKey);
        var workerB = CreateJob(workerBContext, "worker-b", executionKey);

        var workerAExecution = workerA.ExecuteAsync();
        var workerBExecution = workerB.ExecuteAsync();

        await Task.WhenAll(workerAExecution, workerBExecution);

        await using var verificationContext = fixture.CreateDbContext();
        var executions = await verificationContext.JobExecutions
            .Where(candidate => candidate.JobName == DailyReportJob.JobName
                && candidate.ExecutionKey == executionKey)
            .OrderBy(candidate => candidate.WorkerInstance)
            .ToListAsync();

        Assert.Equal(2, executions.Count);
        Assert.Collection(
            executions,
            execution => Assert.Equal("worker-a", execution.WorkerInstance),
            execution => Assert.Equal("worker-b", execution.WorkerInstance));
    }

    private async Task<Guid> ExecuteJobAsync(
        string workerInstance,
        string executionKey,
        DateTimeOffset? timestamp = null)
    {
        await using var dbContext = fixture.CreateDbContext();
        var job = CreateJob(dbContext, workerInstance, executionKey, timestamp);

        return await job.ExecuteAsync();
    }

    private static DailyReportJob CreateJob(
        DistributedLockDbContext dbContext,
        string workerInstance,
        string executionKey,
        DateTimeOffset? timestamp = null)
    {
        var timeProvider = new FixedTimeProvider(
            timestamp ?? new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero));

        return new DailyReportJob(
            dbContext,
            new WorkerOptions(workerInstance, executionKey),
            timeProvider,
            NullLogger<DailyReportJob>.Instance);
    }

    private sealed class FirstSaveRendezvousInterceptor(ExecutionRendezvous rendezvous)
        : SaveChangesInterceptor
    {
        private int hasArrived;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref hasArrived, 1) == 0)
            {
                await rendezvous.ArriveAndWaitAsync(cancellationToken);
            }

            return result;
        }
    }

    private sealed class ExecutionRendezvous(int participantCount)
    {
        private readonly Lock gate = new();
        private readonly TaskCompletionSource allParticipantsArrived = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivedParticipants;

        public async Task ArriveAndWaitAsync(CancellationToken cancellationToken)
        {
            lock (gate)
            {
                arrivedParticipants++;
                if (arrivedParticipants == participantCount)
                {
                    allParticipantsArrived.SetResult();
                }
            }

            await allParticipantsArrived.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return timestamp;
        }
    }
}
