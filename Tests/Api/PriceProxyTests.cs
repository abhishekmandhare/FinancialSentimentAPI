using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tests.Api;

/// <summary>
/// Integration tests for PriceProxyController hitting the real Yahoo Finance API.
/// These verify the proxy works end-to-end: correct URL construction, User-Agent
/// header, response passthrough, and input validation (range/interval whitelist).
///
/// Marked with [Trait("Category", "Integration")] so they can be excluded from
/// CI runs if Yahoo Finance is unreachable.
/// </summary>
[Trait("Category", "Integration")]
public class PriceProxyTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PriceProxyTests()
    {
        _factory = new PriceProxyWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetChart_ValidSymbol_Returns200WithJsonPayload()
    {
        var response = await _client.GetAsync("/api/prices/AAPL/chart?range=5d&interval=1h");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("chart", out var chart).Should().BeTrue(
            because: "Yahoo Finance chart API returns a root 'chart' object");
        chart.TryGetProperty("result", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetChart_ValidSymbol_ContainsPriceData()
    {
        var response = await _client.GetAsync("/api/prices/MSFT/chart?range=1d&interval=15m");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement
            .GetProperty("chart")
            .GetProperty("result");

        result.GetArrayLength().Should().BeGreaterThan(0,
            because: "MSFT is a valid symbol and should return at least one result");

        var first = result[0];
        first.TryGetProperty("meta", out var meta).Should().BeTrue();
        meta.TryGetProperty("regularMarketPrice", out _).Should().BeTrue(
            because: "the response should include the current market price");
    }

    [Fact]
    public async Task GetChart_DefaultRangeAndInterval_Returns200()
    {
        // No query params — should use defaults (range=5d, interval=1h)
        var response = await _client.GetAsync("/api/prices/GOOGL/chart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetChart_InvalidRange_FallsBackToDefault()
    {
        // "99y" is not in the whitelist — should silently fall back to "5d"
        var response = await _client.GetAsync("/api/prices/AAPL/chart?range=99y&interval=1h");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "invalid range should fall back to the default '5d', not error");
    }

    [Fact]
    public async Task GetChart_InvalidInterval_FallsBackToDefault()
    {
        // "2m" is not in the whitelist — should silently fall back to "1h"
        var response = await _client.GetAsync("/api/prices/AAPL/chart?range=5d&interval=2m");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "invalid interval should fall back to the default '1h', not error");
    }

    [Fact]
    public async Task GetChart_CryptoSymbol_Returns200()
    {
        var response = await _client.GetAsync("/api/prices/BTC-USD/chart?range=5d&interval=1h");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "Yahoo Finance supports crypto symbols like BTC-USD");
    }

    [Theory]
    [InlineData("1d", "5m")]
    [InlineData("5d", "15m")]
    [InlineData("1mo", "1d")]
    [InlineData("3mo", "1d")]
    public async Task GetChart_AllWhitelistedCombinations_Return200(string range, string interval)
    {
        var response = await _client.GetAsync($"/api/prices/AAPL/chart?range={range}&interval={interval}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"range={range} and interval={interval} are both whitelisted");
    }

    [Fact]
    public async Task GetChart_InvalidSymbol_Returns502()
    {
        // A completely bogus symbol should cause Yahoo to return an error
        var response = await _client.GetAsync("/api/prices/ZZZZZZZZZ123/chart");

        // Yahoo returns 404 or an error for invalid symbols; our proxy maps that to 502
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway,
            because: "an invalid symbol should result in an upstream error");
    }
}

public class PriceProxyWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
        });
    }
}
