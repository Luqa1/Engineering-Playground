using EngineeringPlayground.Outbox.Infrastructure.Messaging;
using EngineeringPlayground.Outbox.Infrastructure.Persistence;
using EngineeringPlayground.Outbox.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EngineeringPlayground.Outbox.IntegrationTests;

public sealed class OutboxProcessorTests
{
    private const int MaxRetriesCount = 5;

    [Fact]
    public void NewPendingMessageIsEligible()
    {
        var message = CreateMessage();
        Assert.Contains(message, Select(message));
    }

    [Fact]
    public void ProcessedMessageIsNotEligible()
    {
        var message = CreateMessage();
        message.MarkAsProcessed(DateTime.UtcNow);
        Assert.DoesNotContain(message, Select(message));
    }

    [Fact]
    public void FailedMessageBelowRetryLimitIsEligible()
    {
        var message = CreateMessageWithFailures(MaxRetriesCount - 1);
        Assert.Contains(message, Select(message));
    }

    [Theory]
    [InlineData(MaxRetriesCount)]
    [InlineData(MaxRetriesCount + 1)]
    public void MessageAtOrAboveRetryLimitIsNotEligible(int retryCount)
    {
        var message = CreateMessageWithFailures(retryCount);
        Assert.DoesNotContain(message, Select(message));
    }

    [Fact]
    public async Task PublishFailureMarksMessageAsFailedExactlyOnce()
    {
        var message = CreateMessageWithFailures(1);
        var publisher = new RecordingPublisher(new InvalidOperationException("latest error"));
        var saveCount = 0;

        var succeeded = await ProcessAsync(message, publisher, () =>
        {
            saveCount++;
            return Task.CompletedTask;
        });

        Assert.False(succeeded);
        Assert.Equal(1, publisher.CallCount);
        Assert.Equal(2, message.RetryCount);
        Assert.Equal("latest error", message.Error);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Equal(1, saveCount);
    }

    [Fact]
    public async Task SuccessfulPublishMarksMessageAsProcessedAndKeepsFailureHistory()
    {
        var message = CreateMessageWithFailures(1);
        var publisher = new RecordingPublisher();

        var succeeded = await ProcessAsync(message, publisher);

        Assert.True(succeeded);
        Assert.Equal(1, publisher.CallCount);
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Null(message.Error);
        Assert.Equal(1, message.RetryCount);
    }

    [Fact]
    public async Task MessageRemainsPendingAfterFailedWorkerExecution()
    {
        var message = CreateMessage();
        var messages = new List<OutboxMessage> { message };
        var worker = new ProcessorHarness(messages, new RecordingPublisher(
            new InvalidOperationException("broker unavailable")));

        await worker.RunPollingCycleAsync();

        Assert.Null(message.ProcessedAtUtc);
        Assert.Equal(1, message.RetryCount);
        Assert.Equal("broker unavailable", message.Error);
        Assert.Contains(message, Select(message));
    }

    [Fact]
    public async Task NewWorkerInstanceCanProcessExistingPendingMessage()
    {
        var message = CreateMessage();
        var persistedMessages = new List<OutboxMessage> { message };
        var stoppedWorker = new ProcessorHarness(persistedMessages, new RecordingPublisher(
            new InvalidOperationException("broker unavailable")));
        await stoppedWorker.RunPollingCycleAsync();
        var recoveryPublisher = new RecordingPublisher();
        var restartedWorker = new ProcessorHarness(persistedMessages, recoveryPublisher);

        await restartedWorker.RunPollingCycleAsync();

        Assert.Equal(1, recoveryPublisher.CallCount);
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Null(message.Error);
        Assert.Equal(1, message.RetryCount);
    }

    [Fact]
    public async Task SuccessfullyProcessedMessageIsNotPublishedAgain()
    {
        var message = CreateMessage();
        var persistedMessages = new List<OutboxMessage> { message };
        var publisher = new RecordingPublisher();

        await new ProcessorHarness(persistedMessages, publisher).RunPollingCycleAsync();
        await new ProcessorHarness(persistedMessages, publisher).RunPollingCycleAsync();

        Assert.Equal(1, publisher.CallCount);
    }

    [Fact]
    public async Task FinalFailedAttemptStopsAutomaticProcessingAndLogsManualIntervention()
    {
        var message = CreateMessageWithFailures(MaxRetriesCount - 1);
        var publisher = new RecordingPublisher(new InvalidOperationException("publication failed"));
        var logger = new RecordingLogger();

        var succeeded = await ProcessAsync(message, publisher, logger: logger);

        Assert.False(succeeded);
        Assert.Equal(MaxRetriesCount, message.RetryCount);
        Assert.Equal("publication failed", message.Error);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Empty(Select(message));
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("manual investigation and reprocessing are required", StringComparison.Ordinal));
    }

    private static Task<bool> ProcessAsync(
        OutboxMessage message,
        IIntegrationEventPublisher publisher,
        Func<Task>? saveChangesAsync = null,
        ILogger? logger = null) =>
        OutboxProcessor.ProcessMessageAsync(
            message,
            publisher,
            saveChangesAsync ?? (() => Task.CompletedTask),
            MaxRetriesCount,
            logger ?? NullLogger.Instance,
            CancellationToken.None);

    private static List<OutboxMessage> Select(params OutboxMessage[] messages) =>
        OutboxProcessor.GetEligibleMessages(messages.AsQueryable(), MaxRetriesCount).ToList();

    private static OutboxMessage CreateMessage() =>
        new(Guid.NewGuid(), "order.created", "{}", DateTime.UtcNow);

    private static OutboxMessage CreateMessageWithFailures(int failureCount)
    {
        var message = CreateMessage();
        for (var attempt = 0; attempt < failureCount; attempt++)
        {
            message.MarkAsFailed($"failure {attempt + 1}");
        }

        return message;
    }

    private sealed class ProcessorHarness(
        IReadOnlyCollection<OutboxMessage> persistedMessages,
        IIntegrationEventPublisher publisher)
    {
        public async Task RunPollingCycleAsync()
        {
            var messages = OutboxProcessor.GetEligibleMessages(
                    persistedMessages.AsQueryable(),
                    MaxRetriesCount)
                .ToList();

            foreach (var message in messages)
            {
                var succeeded = await ProcessAsync(message, publisher);
                if (!succeeded)
                {
                    return;
                }
            }
        }
    }

    private sealed class RecordingPublisher(Exception? failure = null) : IIntegrationEventPublisher
    {
        public int CallCount { get; private set; }

        public Task PublishAsync(string eventType, string jsonPayload, CancellationToken cancellationToken)
        {
            CallCount++;
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
