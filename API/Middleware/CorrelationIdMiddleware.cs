using System.Diagnostics;

namespace API.Middleware;

/// <summary>
/// Unifies the correlation ID with the OpenTelemetry trace ID.
/// If OTel is active (Activity.Current exists), the trace ID IS the correlation ID —
/// logs, traces, metrics, and the X-Correlation-Id response header all share the same ID.
/// Falls back to client-provided header or a new GUID if OTel is not active.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Activity.Current?.TraceId.ToString()
            ?? context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Response.Headers[HeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        await next(context);
    }
}
