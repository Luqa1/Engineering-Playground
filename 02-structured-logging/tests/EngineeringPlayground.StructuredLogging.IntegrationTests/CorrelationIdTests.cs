using EngineeringPlayground.StructuredLogging.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace EngineeringPlayground.StructuredLogging.IntegrationTests;

public sealed class CorrelationIdTests(PaymentApiFactory factory) : IClassFixture<PaymentApiFactory>
{
    [Fact]
    public async Task MissingCorrelationIdIsGeneratedAndReturned()
    {
        var response = await factory.CreateClient().GetAsync("/does-not-exist");

        var correlationId = GetResponseCorrelationId(response);

        Assert.True(Guid.TryParseExact(correlationId, "N", out _));
    }

    [Fact]
    public async Task ValidIncomingCorrelationIdIsPreserved()
    {
        const string expectedCorrelationId = "client-request-42";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/does-not-exist");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, expectedCorrelationId);

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(expectedCorrelationId, GetResponseCorrelationId(response));
    }

    [Theory]
    [MemberData(nameof(InvalidCorrelationIds))]
    public async Task InvalidIncomingCorrelationIdIsReplaced(string suppliedCorrelationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/does-not-exist");
        request.Headers.TryAddWithoutValidation(
            CorrelationIdMiddleware.HeaderName,
            suppliedCorrelationId);

        var response = await factory.CreateClient().SendAsync(request);
        var correlationId = GetResponseCorrelationId(response);

        Assert.NotEqual(suppliedCorrelationId, correlationId);
        Assert.True(Guid.TryParseExact(correlationId, "N", out _));
    }

    [Fact]
    public async Task CorrelationIdIsIncludedInNestedRequestLogs()
    {
        var sink = new CollectingLogEventSink();
        using var serilogLogger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();
        using var loggerFactory = LoggerFactory.Create(logging =>
            logging.AddSerilog(serilogLogger, dispose: false));

        var downstreamLogger = loggerFactory.CreateLogger("PaymentProcessing");
        var middleware = new CorrelationIdMiddleware(
            _ =>
            {
                using var paymentScope = downstreamLogger.BeginScope(new Dictionary<string, object>
                {
                    ["PaymentId"] = Guid.NewGuid(),
                    ["CustomerId"] = Guid.NewGuid(),
                    ["Operation"] = "ProcessPayment"
                });

                downstreamLogger.LogInformation(
                    "Payment finished with status {PaymentStatus}",
                    "Completed");

                return Task.CompletedTask;
            },
            loggerFactory.CreateLogger<CorrelationIdMiddleware>());
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        var correlationId = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        var logEvent = Assert.Single(sink.Events);

        AssertPropertyEquals(logEvent, "CorrelationId", correlationId);
        Assert.True(logEvent.Properties.ContainsKey("PaymentId"));
        Assert.True(logEvent.Properties.ContainsKey("CustomerId"));
        AssertPropertyEquals(logEvent, "Operation", "ProcessPayment");
        AssertPropertyEquals(logEvent, "PaymentStatus", "Completed");
    }

    public static TheoryData<string> InvalidCorrelationIds => new()
    {
        "   ",
        new string('x', 129)
    };

    private static string GetResponseCorrelationId(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues(
            CorrelationIdMiddleware.HeaderName,
            out var values));

        return Assert.Single(values);
    }

    private static void AssertPropertyEquals(
        LogEvent logEvent,
        string propertyName,
        object expectedValue)
    {
        var value = Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]);
        Assert.Equal(expectedValue, value.Value);
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
