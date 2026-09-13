using EngineeringPlayground.DistributedLock.Domain;
using EngineeringPlayground.DistributedLock.Infrastructure;

namespace EngineeringPlayground.DistributedLock.Worker;

public sealed class DailyReportJob(
    DistributedLockDbContext dbContext,
    WorkerOptions workerOptions,
    TimeProvider timeProvider,
    ILogger<DailyReportJob> logger)
{
    public const string JobName = "daily-report";

    public async Task<Guid> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var execution = new JobExecution(
            JobName,
            workerOptions.JobExecutionKey,
            workerOptions.Instance,
            timeProvider.GetUtcNow());

        dbContext.JobExecutions.Add(execution);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Job execution started: {JobName} {ExecutionKey} {WorkerInstance} {JobExecutionId}",
            execution.JobName,
            execution.ExecutionKey,
            execution.WorkerInstance,
            execution.Id);

        execution.Complete(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Job execution completed: {JobName} {ExecutionKey} {WorkerInstance} {JobExecutionId}",
            execution.JobName,
            execution.ExecutionKey,
            execution.WorkerInstance,
            execution.Id);

        return execution.Id;
    }
}
