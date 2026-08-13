using EngineeringPlayground.StructuredLogging.Domain.Payments;
using EngineeringPlayground.StructuredLogging.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace EngineeringPlayground.StructuredLogging.Api.Services;

public sealed class PaymentProcessor(
    IPaymentGateway paymentGateway,
    PaymentDbContext dbContext,
    ILogger<PaymentProcessor> logger)
{
    public async Task<Payment> ProcessAsync(
        Guid customerId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        var payment = Payment.Create(customerId, amount, currency);

        using var paymentScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["PaymentId"] = payment.Id,
            ["CustomerId"] = payment.CustomerId,
            ["Operation"] = "ProcessPayment"
        });

        logger.LogInformation(
            "Payment processing started with amount {Amount} {Currency} and status {PaymentStatus}",
            payment.Amount,
            payment.Currency,
            payment.Status);

        try
        {
            await paymentGateway.ProcessAsync(payment, cancellationToken);

            var previousStatus = payment.Status;
            payment.MarkCompleted();

            logger.LogInformation(
                "Payment status changed from {PreviousPaymentStatus} to {PaymentStatus}",
                previousStatus,
                payment.Status);

            dbContext.Payments.Add(payment);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Payment persisted");

            logger.LogInformation("Payment processing completed");

            return payment;
        }
        catch (PaymentGatewayException exception)
        {
            logger.LogError(
                exception,
                "Payment gateway invocation failed with amount {Amount} {Currency} and status {PaymentStatus}",
                payment.Amount,
                payment.Currency,
                payment.Status);

            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Payment processing failed with status {PaymentStatus}",
                payment.Status);

            throw;
        }
    }
}
