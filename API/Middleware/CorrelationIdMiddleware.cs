namespace API.Middleware;

/// <summary>
/// Adds a correlation ID to every request.
/// If the client sends X-Correlation-Id, we reuse it (distributed tracing support).
/// If not, we generate one. The ID appears in all structured log lines for the request —
/// grep by correlation ID to trace a complete request across services.
///
/// Foundation for future OpenTelemetry trace context propagation.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Response.Headers[HeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        await next(context);
    }
}
