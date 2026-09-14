using EngineeringPlayground.DistributedLock.Infrastructure;

namespace EngineeringPlayground.DistributedLock.Worker;

public sealed class DailyReportJobRunner(
    PostgresAdvisoryLock advisoryLock,
    DailyReportJob job,
    WorkerOptions workerOptions,
    ILogger<DailyReportJobRunner> logger)
{
    public async Task<JobExecutionAttemptResult> TryExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Attempting job execution: {WorkerInstance} {JobName} {ExecutionKey}",
            workerOptions.Instance,
            DailyReportJob.JobName,
            workerOptions.JobExecutionKey);

        var lease = await advisoryLock.TryAcquireAsync(
            DailyReportJob.JobName,
            workerOptions.JobExecutionKey,
            cancellationToken);

        if (lease is null)
        {
            logger.LogInformation(
                "Distributed lock unavailable: {WorkerInstance} {JobName} {ExecutionKey}",
                workerOptions.Instance,
                DailyReportJob.JobName,
                workerOptions.JobExecutionKey);
            logger.LogInformation(
                "Job execution skipped: {WorkerInstance} {JobName} {ExecutionKey}",
                workerOptions.Instance,
                DailyReportJob.JobName,
                workerOptions.JobExecutionKey);
            return JobExecutionAttemptResult.Skipped;
        }

        logger.LogInformation(
            "Distributed lock acquired: {WorkerInstance} {JobName} {ExecutionKey}",
            workerOptions.Instance,
            DailyReportJob.JobName,
            workerOptions.JobExecutionKey);

        try
        {
            await job.ExecuteAsync(cancellationToken);
            return JobExecutionAttemptResult.Executed;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Protected operation failed: {WorkerInstance} {JobName} {ExecutionKey}",
                workerOptions.Instance,
                DailyReportJob.JobName,
                workerOptions.JobExecutionKey);
            throw;
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
