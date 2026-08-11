using EngineeringPlayground.StructuredLogging.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EngineeringPlayground.StructuredLogging.IntegrationTests;

public sealed class PaymentApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"payments-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<PaymentDbContext>();
            services.RemoveAll<DbContextOptions<PaymentDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PaymentDbContext>>();

            services.AddDbContext<PaymentDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
        });
    }
}
