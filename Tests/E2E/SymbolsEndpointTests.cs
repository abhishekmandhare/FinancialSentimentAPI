using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Tests.E2E;

/// <summary>
/// End-to-end tests for the tracked symbols management endpoints.
/// Exercises add, list, and remove operations against an in-memory SQLite database.
/// </summary>
[Trait("Category", "E2E")]
public class SymbolsEndpointTests : IAsyncLifetime
{
    private readonly E2EWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SymbolsEndpointTests()
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

    // ── GET /api/symbols ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Empty_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/symbols");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // ── POST /api/symbols ───────────────────────────────────────────────────

    [Fact]
    public async Task Add_ValidSymbol_Returns201()
    {
        var body = JsonBody(new { symbol = "NVDA" });

        var response = await _client.PostAsync("/api/symbols", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("symbol", out var sym).Should().BeTrue();
        sym.GetString().Should().Be("NVDA");
        doc.RootElement.TryGetProperty("addedAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Add_ThenGetAll_SymbolAppearsInList()
    {
        var body = JsonBody(new { symbol = "META" });
        var postResponse = await _client.PostAsync("/api/symbols", body);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await _client.GetAsync("/api/symbols");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await getResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var symbols = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("symbol").GetString())
            .ToList();

        symbols.Should().Contain("META");
    }

    [Fact]
    public async Task Add_DuplicateSymbol_Returns400()
    {
        var body = JsonBody(new { symbol = "INTC" });

        // First add succeeds
        var first = await _client.PostAsync("/api/symbols", body);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second add for same symbol should fail
        var second = await _client.PostAsync("/api/symbols", body);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_EmptySymbol_Returns422()
    {
        var body = JsonBody(new { symbol = "" });

        var response = await _client.PostAsync("/api/symbols", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Add_InvalidCharacters_Returns422()
    {
        var body = JsonBody(new { symbol = "BAD!@#" });

        var response = await _client.PostAsync("/api/symbols", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── DELETE /api/symbols/{symbol} ────────────────────────────────────────

    [Fact]
    public async Task Remove_ExistingSymbol_Returns204()
    {
        // Add first
        var body = JsonBody(new { symbol = "AMD" });
        await _client.PostAsync("/api/symbols", body);

        // Remove
        var response = await _client.DeleteAsync("/api/symbols/AMD");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Remove_NonExistentSymbol_Returns404()
    {
        var response = await _client.DeleteAsync("/api/symbols/DOESNOTEXIST");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Remove_ThenGetAll_SymbolNoLongerInList()
    {
        // Add
        var body = JsonBody(new { symbol = "QCOM" });
        await _client.PostAsync("/api/symbols", body);

        // Remove
        await _client.DeleteAsync("/api/symbols/QCOM");

        // Verify gone
        var getResponse = await _client.GetAsync("/api/symbols");
        var json = await getResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var symbols = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("symbol").GetString())
            .ToList();

        symbols.Should().NotContain("QCOM");
    }
}
