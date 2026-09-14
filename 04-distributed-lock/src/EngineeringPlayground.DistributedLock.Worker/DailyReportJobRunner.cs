using EngineeringPlayground.DistributedLock.Infrastructure;

namespace EngineeringPlayground.DistributedLock.Worker;

public sealed class DailyReportJobRunner(
    PostgresAdvisoryLock advisoryLock,
    DailyReportJob job,
    WorkerOptions workerOptions,
    ILogger<DailyReportJobRunner> logger)
{
    public async Task<Guid?> TryExecuteAsync(CancellationToken cancellationToken = default)
    {
        var lease = await advisoryLock.TryAcquireAsync(
            DailyReportJob.JobName,
            workerOptions.JobExecutionKey,
            cancellationToken);

        if (lease is null)
        {
            logger.LogInformation(
                "Job execution skipped because another instance owns the distributed lock: {WorkerInstance} {JobName} {ExecutionKey}",
                workerOptions.Instance,
                DailyReportJob.JobName,
                workerOptions.JobExecutionKey);
            return null;
        }

        logger.LogInformation(
            "Distributed lock acquired: {WorkerInstance} {JobName} {ExecutionKey}",
            workerOptions.Instance,
            DailyReportJob.JobName,
            workerOptions.JobExecutionKey);

        try
        {
            return await job.ExecuteAsync(cancellationToken);
        }
        finally
        {
            await lease.DisposeAsync();
            logger.LogInformation(
                "Distributed lock released: {WorkerInstance} {JobName} {ExecutionKey}",
                workerOptions.Instance,
                DailyReportJob.JobName,
                workerOptions.JobExecutionKey);
        }
    }
}
