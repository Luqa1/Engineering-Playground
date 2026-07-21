using EngineeringPlayground.Outbox.Infrastructure.Messaging;
using EngineeringPlayground.Outbox.Infrastructure.Persistence;
using EngineeringPlayground.Outbox.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace EngineeringPlayground.Outbox.IntegrationTests;

public sealed class OutboxProcessorTests
{
    private const int MaxRetriesCount = 5;

    [Fact]
    public void PendingMessageBelowRetryLimitIsSelected()
    {
        var message = CreateMessage();

        var selected = Select(message);

        Assert.Contains(message, selected);
    }

    [Theory]
    [InlineData(MaxRetriesCount)]
    [InlineData(MaxRetriesCount + 1)]
    public void MessageAtOrAboveRetryLimitIsNotSelected(int retryCount)
    {
        var message = CreateMessageWithFailures(retryCount);

        var selected = Select(message);

        Assert.DoesNotContain(message, selected);
    }

    [Fact]
    public void MarkAsFailedIncrementsRetryCountExactlyOnceAndStoresLatestError()
    {
        var message = CreateMessageWithFailures(1);

        message.MarkAsFailed("latest error");

        Assert.Equal(2, message.RetryCount);
        Assert.Equal("latest error", message.Error);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Fact]
    public async Task FailedFinalAllowedAttemptIsPersistedWithoutProcessedTimestamp()
    {
        var message = CreateMessageWithFailures(MaxRetriesCount - 1);
        var saveCount = 0;

        var succeeded = await OutboxProcessor.ProcessMessageAsync(
            message,
            new FailingPublisher(),
            () =>
            {
                saveCount++;
                return Task.CompletedTask;
            },
            MaxRetriesCount,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.False(succeeded);
        Assert.Equal(MaxRetriesCount, message.RetryCount);
        Assert.Equal("publication failed", message.Error);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Equal(1, saveCount);
    }

    [Fact]
    public async Task SuccessfullyPublishedMessageIsMarkedAsProcessed()
    {
        var message = CreateMessageWithFailures(1);

        var succeeded = await OutboxProcessor.ProcessMessageAsync(
            message,
            new SuccessfulPublisher(),
            () => Task.CompletedTask,
            MaxRetriesCount,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.True(succeeded);
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Null(message.Error);
        Assert.Equal(1, message.RetryCount);
    }

    [Fact]
    public void PreviouslyFailedMessageBelowLimitCanBeSelectedAgain()
    {
        var message = CreateMessageWithFailures(MaxRetriesCount - 1);

        var selected = Select(message);

        Assert.Contains(message, selected);
    }

    private static List<OutboxMessage> Select(OutboxMessage message) =>
        OutboxProcessor.GetEligibleMessages(new[] { message }.AsQueryable(), MaxRetriesCount).ToList();

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

    private sealed class SuccessfulPublisher : IIntegrationEventPublisher
    {
        public Task PublishAsync(string eventType, string jsonPayload, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FailingPublisher : IIntegrationEventPublisher
    {
        public Task PublishAsync(string eventType, string jsonPayload, CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("publication failed"));
    }
}
