using Microsoft.Extensions.Primitives;

namespace EngineeringPlayground.StructuredLogging.Api.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    private const int MaximumLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context.Request.Headers);

        context.Response.Headers[HeaderName] = correlationId;

        using var correlationScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        await next(context);
    }

    private static string GetCorrelationId(IHeaderDictionary headers)
    {
        if (headers.TryGetValue(HeaderName, out var values)
            && TryGetValidValue(values, out var correlationId))
        {
            return correlationId;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool TryGetValidValue(StringValues values, out string correlationId)
    {
        correlationId = string.Empty;

        if (values.Count != 1)
        {
            return false;
        }

        var suppliedValue = values[0]?.Trim();
        if (string.IsNullOrEmpty(suppliedValue) || suppliedValue.Length > MaximumLength)
        {
            return false;
        }

        foreach (var character in suppliedValue)
        {
            if (char.IsControl(character))
            {
                return false;
            }
        }

        correlationId = suppliedValue;
        return true;
    }
}
