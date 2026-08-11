using EngineeringPlayground.StructuredLogging.Domain.Payments;
using EngineeringPlayground.StructuredLogging.Infrastructure.Persistence;

namespace EngineeringPlayground.StructuredLogging.Api.Services;

public sealed class PaymentProcessor(
    IPaymentGateway paymentGateway,
    PaymentDbContext dbContext)
{
    public async Task<Payment> ProcessAsync(
        Guid customerId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        var payment = Payment.Create(customerId, amount, currency);

        await paymentGateway.ProcessAsync(payment, cancellationToken);
        payment.MarkCompleted();

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return payment;
    }
}
