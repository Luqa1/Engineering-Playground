using EngineeringPlayground.StructuredLogging.Api.Middleware;
using EngineeringPlayground.StructuredLogging.Api.Services;
using EngineeringPlayground.StructuredLogging.Domain.Payments;
using EngineeringPlayground.StructuredLogging.Infrastructure.Payments;
using EngineeringPlayground.StructuredLogging.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace EngineeringPlayground.StructuredLogging.IntegrationTests;

public sealed class PaymentFailureLoggingTests
{
    [Fact]
    public async Task DiagnosticGatewayFailureIsLoggedOnceWithStructuredContext()
    {
        var sink = new CollectingLogEventSink();
        using var serilogLogger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();
        using var loggerFactory = LoggerFactory.Create(logging =>
            logging.AddSerilog(serilogLogger, dispose: false));

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase($"failure-logging-{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new PaymentDbContext(options);
        var gateway = new SimulatedPaymentGateway(
            loggerFactory.CreateLogger<SimulatedPaymentGateway>());
        var processor = new PaymentProcessor(
            gateway,
            dbContext,
            loggerFactory.CreateLogger<PaymentProcessor>());
        var customerId = Guid.NewGuid();
        var middleware = new CorrelationIdMiddleware(
            async _ =>
            {
                await processor.ProcessAsync(
                    customerId,
                    SimulatedPaymentGateway.DiagnosticFailureAmount,
                    "EUR",
                    CancellationToken.None);
            },
            loggerFactory.CreateLogger<CorrelationIdMiddleware>());
        var context = new DefaultHttpContext();

        var exception = await Assert.ThrowsAsync<PaymentGatewayException>(
            () => middleware.InvokeAsync(context));

        var errorEvent = Assert.Single(
            sink.Events,
            logEvent => logEvent.Level == LogEventLevel.Error);

        Assert.Same(exception, errorEvent.Exception);
        Assert.Contains("Simulated payment gateway failure", exception.Message);
        AssertPropertyEquals(
            errorEvent,
            "CorrelationId",
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
        AssertPropertyEquals(errorEvent, "CustomerId", customerId);
        AssertPropertyEquals(errorEvent, "Operation", "ProcessPayment");
        AssertPropertyEquals(
            errorEvent,
            "Amount",
            SimulatedPaymentGateway.DiagnosticFailureAmount);
        AssertPropertyEquals(errorEvent, "Currency", "EUR");
        AssertPropertyValueEquals(errorEvent, "PaymentStatus", nameof(PaymentStatus.Pending));

        var paymentId = Assert.IsType<Guid>(
            Assert.IsType<ScalarValue>(errorEvent.Properties["PaymentId"]).Value);
        Assert.NotEqual(Guid.Empty, paymentId);
    }

    private static void AssertPropertyEquals(
        LogEvent logEvent,
        string propertyName,
        object expectedValue)
    {
        var value = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
        Assert.Equal(expectedValue, value.Value);
    }

    private static void AssertPropertyValueEquals(
        LogEvent logEvent,
        string propertyName,
        string expectedValue)
    {
        var value = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
        Assert.Equal(expectedValue, value.Value?.ToString());
    }

    private sealed class CollectingLogEventSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
