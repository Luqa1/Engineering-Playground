using EngineeringPlayground.StructuredLogging.Api.Contracts;
using EngineeringPlayground.StructuredLogging.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EngineeringPlayground.StructuredLogging.Api.Controllers;

[ApiController]
[Route("payments")]
public sealed class PaymentsController(PaymentProcessor paymentProcessor) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentResponse>> Create(
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(request.CustomerId), "Customer ID cannot be empty.");
        }

        if (request.Amount <= 0)
        {
            ModelState.AddModelError(nameof(request.Amount), "Amount must be greater than zero.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var payment = await paymentProcessor.ProcessAsync(
            request.CustomerId,
            request.Amount,
            request.Currency,
            cancellationToken);

        var response = new PaymentResponse(
            payment.Id,
            payment.CustomerId,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            payment.CreatedAtUtc);

        return Created($"/payments/{payment.Id}", response);
    }
}
