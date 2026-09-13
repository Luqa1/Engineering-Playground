using EngineeringPlayground.DistributedLock.Domain;
using Microsoft.EntityFrameworkCore;

namespace EngineeringPlayground.DistributedLock.Infrastructure;

public sealed class DistributedLockDbContext(DbContextOptions<DistributedLockDbContext> options)
    : DbContext(options)
{
    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DistributedLockDbContext).Assembly);
    }
}
