using EngineeringPlayground.StructuredLogging.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace EngineeringPlayground.StructuredLogging.Infrastructure.Payments;

public sealed class SimulatedPaymentGateway(ILogger<SimulatedPaymentGateway> logger) : IPaymentGateway
{
    public const decimal DiagnosticFailureAmount = 13.37m;

    public Task ProcessAsync(Payment payment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);
        cancellationToken.ThrowIfCancellationRequested();

        using var gatewayScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["Gateway"] = "SimulatedPaymentGateway"
        });

        logger.LogInformation("Payment gateway invocation started");

        if (payment.Amount == DiagnosticFailureAmount)
        {
            throw new PaymentGatewayException(
                $"Simulated payment gateway failure for diagnostic amount {DiagnosticFailureAmount}.");
        }

        logger.LogInformation("Payment gateway result received");

        return Task.CompletedTask;
    }
}
