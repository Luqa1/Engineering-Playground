namespace EngineeringPlayground.StructuredLogging.Domain.Payments;

public interface IPaymentGateway
{
    Task ProcessAsync(Payment payment, CancellationToken cancellationToken);
}
