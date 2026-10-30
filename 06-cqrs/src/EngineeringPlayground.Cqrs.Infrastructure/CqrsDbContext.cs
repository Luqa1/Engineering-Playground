using EngineeringPlayground.Cqrs.Domain;
using Microsoft.EntityFrameworkCore;

namespace EngineeringPlayground.Cqrs.Infrastructure;

public sealed class CqrsDbContext(DbContextOptions<CqrsDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CqrsDbContext).Assembly);
    }
}
