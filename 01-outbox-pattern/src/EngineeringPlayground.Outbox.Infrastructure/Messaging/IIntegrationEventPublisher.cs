namespace EngineeringPlayground.Outbox.Infrastructure.Messaging;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(string eventType, string jsonPayload, CancellationToken cancellationToken);
}
