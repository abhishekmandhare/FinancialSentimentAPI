using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tests.Api;

/// <summary>
/// Integration tests verifying that the analyze endpoint is rate-limited
/// and all other endpoints are unaffected.
///
/// Each test creates its own WebApplicationFactory to get a fresh rate-limiter
/// state — this avoids shared-state interference between tests.
///
/// The factory:
///   - Sets environment to "Testing" (skips DB migration, uses Mock AI)
///   - Removes background workers (they need RSS feeds and a real DB)
///   - Clears logging providers (avoids Windows EventLog permission errors)
///   - Overrides the analyze rate limit to 2 requests per window via
///     services.Configure<RateLimiterOptions> so tests can trigger 429 quickly
///
/// Controller actions that reach the DB will fail (500), but rate-limiting
/// tests only assert on the HTTP status code, so 500 ≠ 429 is sufficient.
/// </summary>
public class RateLimitingTests
{
    private static RateLimitingWebApplicationFactory CreateFactory() => new();

    private static StringContent AnalyzeBody() =>
        new(
            """{"symbol":"AAPL","text":"Apple reported strong quarterly earnings beating all estimates."}""",
            Encoding.UTF8,
            "application/json");

    [Fact]
    public async Task Analyze_WithinRateLimit_DoesNotReturn429()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // The factory sets PermitLimit=2; first two requests must not be rate-limited.
        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsync("/api/sentiment/analyze", AnalyzeBody());
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                because: $"request {i + 1} is within the configured limit of 2");
        }
    }

    [Fact]
    public async Task Analyze_ExceedsRateLimit_Returns429TooManyRequests()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Exhaust the 2-request limit.
        for (var i = 0; i < 2; i++)
            await client.PostAsync("/api/sentiment/analyze", AnalyzeBody());

        var limitedResponse = await client.PostAsync("/api/sentiment/analyze", AnalyzeBody());

        limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Analyze_ExceedsRateLimit_IncludesRetryAfterHeader()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var i = 0; i < 2; i++)
            await client.PostAsync("/api/sentiment/analyze", AnalyzeBody());

        var limitedResponse = await client.PostAsync("/api/sentiment/analyze", AnalyzeBody());

        limitedResponse.Headers.Should().ContainKey("Retry-After",
            because: "clients need to know when they can retry");
    }

    [Fact]
    public async Task GetHistory_ManyRequests_IsNeverRateLimited()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Make more requests than the analyze limit — GET endpoints have no rate limit.
        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("/api/sentiment/AAPL/history");
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                because: "GET /history has no rate limiting policy");
        }
    }

    [Fact]
    public async Task HealthCheck_ManyRequests_IsNeverRateLimited()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("/health/live");
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                because: "health checks must always be reachable");
        }
    }
}

/// <summary>
/// Configures the API host for rate limiting integration tests:
/// - "Testing" environment skips DB migration and uses Mock AI.
/// - Background workers removed (they need real external services).
/// - Logging cleared to avoid Windows EventLog permission errors.
/// - Rate limit policy overridden to 2 requests so tests can trigger 429 quickly.
///   Uses services.Configure&lt;RateLimiterOptions&gt; which runs AFTER AddRateLimiter,
///   overriding the production policy stored under the same policy name.
/// </summary>
public class RateLimitingWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" environment triggers the migration-skip guard in Program.cs
        // and loads appsettings.Testing.json (Mock AI, slow ingestion polling).
        builder.UseEnvironment("Testing");

        // Override rate limit to 2 requests so tests can trigger 429 quickly.
        // Program.cs reads these config values when building the rate limiter policy.
        builder.UseSetting("RateLimiting:AnalyzePermitLimit", "2");
        builder.UseSetting("RateLimiting:AnalyzeWindowSeconds", "60");

        // Clear logging providers so the Windows EventLog logger (added by default
        // on Windows) doesn't fail with permission errors during integration tests.
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureServices(services =>
        {
            // Remove background workers — they try to connect to RSS feeds and the DB,
            // which aren't available in the integration test environment.
            services.RemoveAll<IHostedService>();
        });
    }
}
