using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngineeringPlayground.DistributedLock.Infrastructure;

public static class DatabaseMigrator
{
    public static async Task MigrateAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DistributedLockDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
