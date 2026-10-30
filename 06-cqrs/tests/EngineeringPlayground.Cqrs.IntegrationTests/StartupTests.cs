using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EngineeringPlayground.Cqrs.IntegrationTests;

public sealed class StartupTests
{
    [Fact]
    public async Task ProductionStartsWithoutConnectingToDatabaseForMigrations()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            // An unavailable database makes accidental startup migration attempts fail the test.
            builder.UseSetting("ConnectionStrings:Cqrs",
                "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=test;Timeout=1");
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/unknown-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
