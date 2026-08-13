using System.Net;
using System.Net.Http.Json;
using EngineeringPlayground.StructuredLogging.Api.Contracts;
using EngineeringPlayground.StructuredLogging.Api.Middleware;
using EngineeringPlayground.StructuredLogging.Domain.Payments;
using EngineeringPlayground.StructuredLogging.Infrastructure.Payments;
using EngineeringPlayground.StructuredLogging.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
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

    [Fact]
    public async Task DiagnosticFailureReturnsServerErrorWithCorrelationIdAndIsNotPersisted()
    {
        const string correlationId = "diagnostic-payment-request";
        var request = new CreatePaymentRequest
        {
            CustomerId = Guid.NewGuid(),
            Amount = SimulatedPaymentGateway.DiagnosticFailureAmount,
            Currency = "EUR"
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, "/payments")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var response = await factory.CreateClient().SendAsync(message);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(
            correlationId,
            Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
        Assert.NotNull(problem);
        Assert.Equal("Payment processing could not be completed.", problem.Title);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(nameof(PaymentGatewayException), responseBody);
        Assert.DoesNotContain("Simulated payment gateway failure", responseBody);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        Assert.False(await dbContext.Payments.AnyAsync(
            payment => payment.CustomerId == request.CustomerId));
    }

    [Fact]
    public async Task DiagnosticFailureDoesNotAffectAnUnrelatedPayment()
    {
        var client = factory.CreateClient();
        var failedRequest = new CreatePaymentRequest
        {
            CustomerId = Guid.NewGuid(),
            Amount = SimulatedPaymentGateway.DiagnosticFailureAmount,
            Currency = "EUR"
        };

        var failedResponse = await client.PostAsJsonAsync("/payments", failedRequest);

        var successfulRequest = new CreatePaymentRequest
        {
            CustomerId = Guid.NewGuid(),
            Amount = 24.00m,
            Currency = "EUR"
        };
        var successfulResponse = await client.PostAsJsonAsync("/payments", successfulRequest);
        var payment = await successfulResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.InternalServerError, failedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, successfulResponse.StatusCode);
        Assert.NotNull(payment);
        Assert.Equal(successfulRequest.CustomerId, payment.CustomerId);
        Assert.Equal(nameof(PaymentStatus.Completed), payment.Status);
    }
}
