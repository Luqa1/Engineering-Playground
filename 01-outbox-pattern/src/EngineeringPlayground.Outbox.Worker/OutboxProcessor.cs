using EngineeringPlayground.Outbox.Infrastructure.Messaging;
using EngineeringPlayground.Outbox.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EngineeringPlayground.Outbox.Worker;

public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _pollingInterval;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, IOptions<OutboxProcessorOptions> options, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollingInterval = options.Value.PollingInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started with polling interval {PollingInterval}", _pollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox polling cycle failed");
                await Task.Delay(_pollingInterval, stoppingToken);
            }
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var messages = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null)
            .OrderBy(message => message.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {PendingMessageCount} pending Outbox messages", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message.Type, message.Payload, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var error = exception.Message.Length <= 2000 ? exception.Message : exception.Message[..2000];
                message.MarkAsFailed(error);
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogError(exception, "Failed to publish Outbox message {OutboxMessageId} with event type {EventType}; retry count is {RetryCount}", message.Id, message.Type, message.RetryCount);
                return;
            }

            message.MarkAsProcessed(DateTime.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Published Outbox message {OutboxMessageId} with event type {EventType}", message.Id, message.Type);
        }
    }
}
