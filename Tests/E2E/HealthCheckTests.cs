using System.Net;
using FluentAssertions;

namespace Tests.E2E;

/// <summary>
/// End-to-end tests verifying health check endpoints return 200.
/// These endpoints are critical for Cloud Run readiness probes.
/// </summary>
[Trait("Category", "E2E")]
public class HealthCheckTests : IAsyncLifetime
{
    private readonly E2EWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HealthCheckTests()
    {
        _factory = new E2EWebApplicationFactory();
        _factory.EnsureDatabaseCreated();
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task LiveHealthCheck_Returns200()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadyHealthCheck_Returns200()
    {
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
