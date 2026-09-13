namespace EngineeringPlayground.DistributedLock.Worker;

public sealed class OneShotWorker(
    IServiceScopeFactory scopeFactory,
    WorkerOptions workerOptions,
    IHostApplicationLifetime applicationLifetime,
    ILogger<OneShotWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker starting: {WorkerInstance}", workerOptions.Instance);

        await using var scope = scopeFactory.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<DailyReportJob>();
        await job.ExecuteAsync(stoppingToken);

        applicationLifetime.StopApplication();
    }
}
