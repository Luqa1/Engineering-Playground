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
    private readonly int _maxRetriesCount;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, IOptions<OutboxProcessorOptions> options, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollingInterval = options.Value.PollingInterval;
        _maxRetriesCount = options.Value.MaxRetriesCount;
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
        var messages = await GetEligibleMessages(dbContext.OutboxMessages, _maxRetriesCount)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {PendingMessageCount} pending Outbox messages", messages.Count);

        foreach (var message in messages)
        {
            var succeeded = await ProcessMessageAsync(
                message,
                publisher,
                () => dbContext.SaveChangesAsync(cancellationToken),
                _maxRetriesCount,
                _logger,
                cancellationToken);

            if (!succeeded)
            {
                return;
            }
        }
    }

    internal static IQueryable<OutboxMessage> GetEligibleMessages(
        IQueryable<OutboxMessage> messages,
        int maxRetriesCount)
    {
        return messages
            .Where(message =>
                message.ProcessedAtUtc == null &&
                message.RetryCount < maxRetriesCount)
            .OrderBy(message => message.CreatedAtUtc);
    }

    internal static async Task<bool> ProcessMessageAsync(
        OutboxMessage message,
        IIntegrationEventPublisher publisher,
        Func<Task> saveChangesAsync,
        int maxRetriesCount,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await publisher.PublishAsync(message.Type, message.Payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var error = exception.Message.Length <= 2000
                ? exception.Message
                : exception.Message[..2000];

            message.MarkAsFailed(error);
            await saveChangesAsync();

            logger.LogError(
                exception,
                "Failed to publish Outbox message {OutboxMessageId} with event type {EventType}; retry count is {RetryCount} of {MaxRetriesCount}",
                message.Id,
                message.Type,
                message.RetryCount,
                maxRetriesCount);

            if (message.RetryCount >= maxRetriesCount)
            {
                logger.LogWarning(
                    "Automatic processing stopped for Outbox message {OutboxMessageId} with event type {EventType} after {RetryCount} of {MaxRetriesCount} attempts; manual investigation and reprocessing are required",
                    message.Id,
                    message.Type,
                    message.RetryCount,
                    maxRetriesCount);
            }

            return false;
        }

        message.MarkAsProcessed(DateTime.UtcNow);
        await saveChangesAsync();
        logger.LogInformation(
            "Published Outbox message {OutboxMessageId} with event type {EventType}",
            message.Id,
            message.Type);
        return true;
    }
}
