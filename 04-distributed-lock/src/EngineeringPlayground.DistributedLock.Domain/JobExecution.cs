namespace EngineeringPlayground.DistributedLock.Domain;

public sealed class JobExecution
{
    private JobExecution()
    {
        JobName = null!;
        WorkerInstance = null!;
    }

    public JobExecution(string jobName, string workerInstance, DateTimeOffset startedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerInstance);

        Id = Guid.NewGuid();
        JobName = jobName;
        WorkerInstance = workerInstance;
        StartedAtUtc = startedAtUtc;
    }

    public Guid Id { get; private set; }

    public string JobName { get; private set; }

    public string WorkerInstance { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        if (completedAtUtc < StartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedAtUtc),
                "Completion time cannot be earlier than start time.");
        }

        CompletedAtUtc = completedAtUtc;
    }
}
