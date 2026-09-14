namespace EngineeringPlayground.DistributedLock.Worker;

public sealed record WorkerOptions(
    string Instance,
    string JobExecutionKey,
    TimeSpan ProtectedWorkDuration = default);
