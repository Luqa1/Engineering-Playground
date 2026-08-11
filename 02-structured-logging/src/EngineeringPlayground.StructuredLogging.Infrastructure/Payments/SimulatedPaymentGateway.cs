using EngineeringPlayground.StructuredLogging.Domain.Payments;

namespace EngineeringPlayground.StructuredLogging.Infrastructure.Payments;

public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    public Task ProcessAsync(Payment payment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}
