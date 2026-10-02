using EngineeringPlayground.Saga.Domain;
using Microsoft.EntityFrameworkCore;
namespace EngineeringPlayground.Saga.Infrastructure;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);
}
