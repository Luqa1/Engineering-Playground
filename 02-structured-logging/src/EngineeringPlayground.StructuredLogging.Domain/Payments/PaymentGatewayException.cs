namespace EngineeringPlayground.StructuredLogging.Domain.Payments;

public sealed class PaymentGatewayException(string message) : Exception(message);
