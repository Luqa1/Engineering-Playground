using System.Net;
using System.Net.Http.Json;
using EngineeringPlayground.StructuredLogging.Api.Contracts;
using EngineeringPlayground.StructuredLogging.Domain.Payments;
using EngineeringPlayground.StructuredLogging.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EngineeringPlayground.StructuredLogging.IntegrationTests;

public sealed class PaymentsApiTests(PaymentApiFactory factory) : IClassFixture<PaymentApiFactory>
{
    [Fact]
    public async Task ValidPaymentCanBeProcessed()
    {
        var request = new CreatePaymentRequest
        {
            CustomerId = Guid.NewGuid(),
            Amount = 100.00m,
            Currency = "EUR"
        };

        var response = await factory.CreateClient().PostAsJsonAsync("/payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(payment);
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal(request.CustomerId, payment.CustomerId);
        Assert.Equal(request.Amount, payment.Amount);
        Assert.Equal("EUR", payment.Currency);
        Assert.Equal(nameof(PaymentStatus.Completed), payment.Status);
    }

    [Fact]
    public async Task InvalidAmountIsRejected()
    {
        var request = new CreatePaymentRequest
        {
            CustomerId = Guid.NewGuid(),
            Amount = 0,
            Currency = "EUR"
        };

        var response = await factory.CreateClient().PostAsJsonAsync("/payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyCustomerIdIsRejected()
    {
        var request = new CreatePaymentRequest
        {
            CustomerId = Guid.Empty,
            Amount = 100.00m,
            Currency = "EUR"
        };

        var response = await factory.CreateClient().PostAsJsonAsync("/payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompletedPaymentIsPersisted()
    {
        var request = new CreatePaymentRequest
        {
            CustomerId = Guid.NewGuid(),
            Amount = 75.50m,
            Currency = "eur"
        };

        var response = await factory.CreateClient().PostAsJsonAsync("/payments", request);
        var createdPayment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(createdPayment);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var persistedPayment = await dbContext.Payments.SingleAsync(
            payment => payment.Id == createdPayment.Id);

        Assert.Equal(PaymentStatus.Completed, persistedPayment.Status);
        Assert.Equal(request.CustomerId, persistedPayment.CustomerId);
        Assert.Equal(request.Amount, persistedPayment.Amount);
        Assert.Equal("EUR", persistedPayment.Currency);
    }
}
