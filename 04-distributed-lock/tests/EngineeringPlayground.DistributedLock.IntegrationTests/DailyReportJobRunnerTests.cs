using EngineeringPlayground.DistributedLock.Infrastructure;
using EngineeringPlayground.DistributedLock.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace EngineeringPlayground.DistributedLock.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class DailyReportJobRunnerTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task CompetingWorkersExecuteOnceAndTheSkippedWorkerCanAcquireAfterRelease()
    {
        var executionKey = $"protected-window-{Guid.NewGuid():N}";
        var workerAOptions = new WorkerOptions("worker-a", executionKey);
        var workerBOptions = new WorkerOptions("worker-b", executionKey);
        var workerAGate = new FirstSaveGate();
        await using var workerAServices = CreateWorkerServices(
            workerAOptions,
            workerAGate);
        await using var workerBServices = CreateWorkerServices(workerBOptions);
        await using var workerAScope = workerAServices.CreateAsyncScope();
        await using var workerBScope = workerBServices.CreateAsyncScope();

        var workerA = workerAScope.ServiceProvider.GetRequiredService<DailyReportJobRunner>();
        var workerB = workerBScope.ServiceProvider.GetRequiredService<DailyReportJobRunner>();

        Assert.Equal(
            PostgresAdvisoryLockKey.Create(
                DailyReportJob.JobName,
                workerAOptions.JobExecutionKey),
            PostgresAdvisoryLockKey.Create(
                DailyReportJob.JobName,
                workerBOptions.JobExecutionKey));

        var workerAAttempt = workerA.TryExecuteAsync();
        JobExecutionAttemptResult workerBResult;

        try
        {
            await workerAGate.WaitUntilFirstSaveAsync();
            workerBResult = await workerB.TryExecuteAsync();
            Assert.Equal(JobExecutionAttemptResult.Skipped, workerBResult);
        }
        finally
        {
            workerAGate.AllowSave();
        }

        var workerAResult = await workerAAttempt;
        Assert.Equal(JobExecutionAttemptResult.Executed, workerAResult);

        await using var verificationContext = fixture.CreateDbContext();
        var executions = await verificationContext.JobExecutions
            .Where(candidate => candidate.JobName == DailyReportJob.JobName
                && candidate.ExecutionKey == executionKey)
            .ToListAsync();

        var execution = Assert.Single(executions);
        Assert.Equal("worker-a", execution.WorkerInstance);

        var workerBLock = workerBScope.ServiceProvider
            .GetRequiredService<PostgresAdvisoryLock>();
        var workerBLeaseAfterRelease = await workerBLock.TryAcquireAsync(
            DailyReportJob.JobName,
            executionKey);

        Assert.NotNull(workerBLeaseAfterRelease);
        await workerBLeaseAfterRelease.DisposeAsync();
    }

    [Fact]
    public async Task ProtectedOperationFailureReleasesLockForAnotherParticipant()
    {
        var executionKey = $"failed-operation-window-{Guid.NewGuid():N}";
        var failure = new InvalidOperationException("The protected operation failed.");
        await using var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);
        await using var failingContext = fixture.CreateDbContext(
            new FailingSaveInterceptor(failure));
        var failingRunner = CreateRunner(
            dataSource,
            failingContext,
            "worker-a",
            executionKey);

        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => failingRunner.TryExecuteAsync());

        Assert.Same(failure, thrownException);

        var participantB = new PostgresAdvisoryLock(dataSource);
        var participantBOwnership = await participantB.TryAcquireAsync(
            DailyReportJob.JobName,
            executionKey);
        Assert.NotNull(participantBOwnership);
        await participantBOwnership.DisposeAsync();
    }

    [Fact]
    public async Task CoordinationInfrastructureFailureDoesNotExecuteTheJob()
    {
        var executionKey = $"coordination-failure-window-{Guid.NewGuid():N}";
        var dataSource = NpgsqlDataSource.Create(fixture.ConnectionString);
        await dataSource.DisposeAsync();
        await using var dbContext = fixture.CreateDbContext();
        var runner = CreateRunner(
            dataSource,
            dbContext,
            "worker-a",
            executionKey);

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => runner.TryExecuteAsync());

        await using var verificationContext = fixture.CreateDbContext();
        var executionCount = await verificationContext.JobExecutions.CountAsync(
            candidate => candidate.JobName == DailyReportJob.JobName
                && candidate.ExecutionKey == executionKey);
        Assert.Equal(0, executionCount);
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

    private ServiceProvider CreateWorkerServices(
        WorkerOptions workerOptions,
        IInterceptor? interceptor = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedLockInfrastructure(fixture.ConnectionString);

        if (interceptor is not null)
        {
            services.AddDbContext<DistributedLockDbContext>(options =>
                options.AddInterceptors(interceptor));
        }

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(workerOptions);
        services.AddScoped<DailyReportJob>();
        services.AddScoped<DailyReportJobRunner>();

        return services.BuildServiceProvider(validateScopes: true);
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

    private sealed class FailingSaveInterceptor(InvalidOperationException failure)
        : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            throw failure;
        }
    }
}
