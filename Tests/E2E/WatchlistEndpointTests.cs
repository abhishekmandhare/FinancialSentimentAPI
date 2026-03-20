using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Tests.E2E;

[Trait("Category", "E2E")]
public class WatchlistEndpointTests : IAsyncLifetime
{
    private readonly E2EWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public WatchlistEndpointTests()
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

    private static StringContent JsonBody(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    [Fact]
    public async Task GetWatchlist_Empty_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/watchlist");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AddWatchlist_EmptySymbol_Returns422()
    {
        var body = JsonBody(new { symbol = "" });
        var response = await _client.PostAsync("/api/watchlist", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AddWatchlist_SpecialCharsSymbol_Returns422()
    {
        var body = JsonBody(new { symbol = "!!@@##" });
        var response = await _client.PostAsync("/api/watchlist", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task DeleteWatchlist_NonExistent_Returns404()
    {
        var response = await _client.DeleteAsync("/api/watchlist/ZZZZZ");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
