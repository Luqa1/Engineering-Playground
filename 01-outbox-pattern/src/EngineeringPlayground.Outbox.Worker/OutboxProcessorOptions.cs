namespace EngineeringPlayground.Outbox.Worker;

public sealed class OutboxProcessorOptions
{
    public const string SectionName = "OutboxProcessor";
    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(5);
    public int MaxRetriesCount { get; init; } = 5;
}
