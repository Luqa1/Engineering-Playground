namespace EngineeringPlayground.DistributedLock.Worker;

public sealed class OneShotWorker(
    IServiceScopeFactory scopeFactory,
    WorkerOptions workerOptions,
    IHostApplicationLifetime applicationLifetime,
    ILogger<OneShotWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Worker starting: {WorkerInstance} for {JobName} {ExecutionKey}",
            workerOptions.Instance,
            DailyReportJob.JobName,
            workerOptions.JobExecutionKey);

        await using var scope = scopeFactory.CreateAsyncScope();
        var jobRunner = scope.ServiceProvider.GetRequiredService<DailyReportJobRunner>();
        await jobRunner.TryExecuteAsync(stoppingToken);

        applicationLifetime.StopApplication();
    }
}
