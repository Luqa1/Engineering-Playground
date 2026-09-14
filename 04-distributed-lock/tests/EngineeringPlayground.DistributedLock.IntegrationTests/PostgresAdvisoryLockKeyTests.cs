using EngineeringPlayground.DistributedLock.Infrastructure;

namespace EngineeringPlayground.DistributedLock.IntegrationTests;

public sealed class PostgresAdvisoryLockKeyTests
{
    [Fact]
    public void SameLogicalJobAlwaysProducesTheSameStableKey()
    {
        var first = PostgresAdvisoryLockKey.Create("daily-report", "2026-09-14");
        var second = PostgresAdvisoryLockKey.Create("daily-report", "2026-09-14");

        Assert.Equal(-1532972353578179735, first);
        Assert.Equal(first, second);
    }
}
