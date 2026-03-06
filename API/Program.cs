using System.Threading.RateLimiting;
using API;
using API.Middleware;
using Application;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ────────────────────────────────────────────────────────────────

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register rate limiter services and the OnRejected handler.
// The fixed-window policy is configured separately via AddOptions<RateLimiterOptions>
// so that integration tests can override config values via IWebHostBuilder.UseSetting:
// reading config inside IOptions.Configure<IConfiguration> is deferred until after
// all ConfigureAppConfiguration delegates (including test overrides) have been applied.
//builder.Services.AddRateLimiter(options =>
//{
//    options.OnRejected = async (context, cancellationToken) =>
//    {
//        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
//        var windowSecs = config.GetValue("RateLimiting:AnalyzeWindowSeconds", 60);
//        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
//        context.HttpContext.Response.Headers["Retry-After"] = windowSecs.ToString();
//        await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", cancellationToken);
//    };
//});

//builder.Services.AddOptions<RateLimiterOptions>()
//    .Configure<IConfiguration>((options, config) =>
//    {
//        var permitLimit = config.GetValue("RateLimiting:AnalyzePermitLimit", 10);
//        var windowSeconds = config.GetValue("RateLimiting:AnalyzeWindowSeconds", 60);
//        options.AddFixedWindowLimiter(RateLimitPolicies.AnalyzeEndpoint, limiterOptions =>
//        {
//            limiterOptions.PermitLimit = permitLimit;
//            limiterOptions.Window = TimeSpan.FromSeconds(windowSeconds);
//            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
//            limiterOptions.QueueLimit = 0;
//        });
//    });

// Each layer owns its own DI registration.
// API only calls AddApplication() and AddInfrastructure() — knows nothing about internals.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Rate Limiting ─────────────────────────────────────────────────────────────
// Fixed-window limiter keyed per client IP — applied only to the analyze endpoint.
// Limits are configurable via appsettings.json RateLimiting section.

var windowSeconds = builder.Configuration.GetValue<int>("RateLimiting:AnalyzeWindowSeconds", 60);
var permitLimit   = builder.Configuration.GetValue<int>("RateLimiting:AnalyzePermitLimit", 10);

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(RateLimitPolicies.AnalyzeEndpoint, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window                = TimeSpan.FromSeconds(windowSeconds),
                PermitLimit           = permitLimit,
                QueueProcessingOrder  = QueueProcessingOrder.OldestFirst,
                QueueLimit            = 0,
                AutoReplenishment     = true
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

        context.HttpContext.Response.ContentType = "application/problem+json";

        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title  = "Too Many Requests",
            Detail = $"Rate limit exceeded. You may make {permitLimit} requests per {windowSeconds} seconds.",
            Type   = "https://httpstatuses.io/429"
        }, cancellationToken);
    };
});

// ── App pipeline ─────────────────────────────────────────────────────────────

var app = builder.Build();

// Auto-migrate on every startup — idempotent, safe for single-instance deployments
// (home server, Cloud Run). For multi-instance HA, extract to a separate migration job.
// Skipped in the "Testing" environment where no real database is available.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(opts =>
    {
        opts.Title = "Financial Sentiment API";
        opts.Theme = ScalarTheme.DeepSpace;
    });
}

// Middleware order matters:
// 1. Correlation ID — tag every request first so all subsequent logs carry the ID
// 2. Exception handling — catch anything that bubbles up from the pipeline
// 3. HTTPS redirect
// 4. Rate limiting — applied before controllers, keyed per IP
// 5. Auth (placeholder — not yet implemented)
// 6. Controllers
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

// Health checks — required for Cloud Run readiness probes
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

// Expose Program to the test assembly via WebApplicationFactory<Program>.
// Top-level statement programs generate an internal Program class by default.
public partial class Program { }
