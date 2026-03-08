using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Tests.E2E;

/// <summary>
/// End-to-end tests for the sentiment endpoints exercising the full stack:
/// API controller -> MediatR -> handler -> EF Core (SQLite) -> Mock AI provider.
///
/// Validates request/response shapes, status codes, persistence, and error handling.
/// </summary>
[Trait("Category", "E2E")]
public class SentimentEndpointTests : IAsyncLifetime
{
    private readonly E2EWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SentimentEndpointTests()
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

    // ── POST /api/sentiment/analyze ─────────────────────────────────────────

    [Fact]
    public async Task Analyze_ValidRequest_Returns201WithResponseShape()
    {
        var body = JsonBody(new
        {
            symbol = "AAPL",
            text = "Apple reported strong quarterly earnings beating all analyst estimates.",
            sourceUrl = "https://example.com/article"
        });

        var response = await _client.PostAsync("/api/sentiment/analyze", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("id", out _).Should().BeTrue("response must include an id");
        root.TryGetProperty("symbol", out var symbol).Should().BeTrue();
        symbol.GetString().Should().Be("AAPL");
        root.TryGetProperty("score", out _).Should().BeTrue("response must include a score");
        root.TryGetProperty("label", out _).Should().BeTrue("response must include a label");
        root.TryGetProperty("confidence", out _).Should().BeTrue("response must include confidence");
        root.TryGetProperty("keyReasons", out var reasons).Should().BeTrue();
        reasons.GetArrayLength().Should().BeGreaterThan(0);
        root.TryGetProperty("modelVersion", out _).Should().BeTrue();
        root.TryGetProperty("analyzedAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Analyze_ValidRequest_ReturnsLocationHeader()
    {
        var body = JsonBody(new
        {
            symbol = "MSFT",
            text = "Microsoft cloud revenue growth exceeded expectations this quarter."
        });

        var response = await _client.PostAsync("/api/sentiment/analyze", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull(
            "CreatedAtAction should set a Location header pointing to the history endpoint");
    }

    [Fact]
    public async Task Analyze_EmptySymbol_Returns422()
    {
        var body = JsonBody(new
        {
            symbol = "",
            text = "Some valid text that is long enough to pass validation."
        });

        var response = await _client.PostAsync("/api/sentiment/analyze", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Analyze_TextTooShort_Returns422()
    {
        var body = JsonBody(new
        {
            symbol = "AAPL",
            text = "Short"
        });

        var response = await _client.PostAsync("/api/sentiment/analyze", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Analyze_InvalidSourceUrl_Returns422()
    {
        var body = JsonBody(new
        {
            symbol = "AAPL",
            text = "Apple reported strong quarterly earnings beating all analyst estimates.",
            sourceUrl = "not-a-url"
        });

        var response = await _client.PostAsync("/api/sentiment/analyze", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Analyze_SpecialCharactersInSymbol_Returns422()
    {
        var body = JsonBody(new
        {
            symbol = "AA!@#",
            text = "Some valid text that is long enough to pass validation rules."
        });

        var response = await _client.PostAsync("/api/sentiment/analyze", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Analyze_MissingBody_Returns422OrBadRequest()
    {
        var response = await _client.PostAsync("/api/sentiment/analyze",
            new StringContent("", Encoding.UTF8, "application/json"));

        // Empty body may return 400 (model binding) or 422 (validation)
        var status = (int)response.StatusCode;
        status.Should().BeOneOf(400, 422);
    }

    // ── POST then GET — full lifecycle ──────────────────────────────────────

    [Fact]
    public async Task Analyze_ThenGetHistory_AnalysisAppearsInHistory()
    {
        // Arrange: submit an analysis
        var body = JsonBody(new
        {
            symbol = "GOOG",
            text = "Google advertising revenue surpassed expectations for the quarter."
        });
        var postResponse = await _client.PostAsync("/api/sentiment/analyze", body);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: fetch history for that symbol (includeNeutral=true to avoid filtering by label)
        var historyResponse = await _client.GetAsync("/api/sentiment/GOOG/history?includeNeutral=true");

        // Assert
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await historyResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("items", out var items).Should().BeTrue();
        items.GetArrayLength().Should().BeGreaterOrEqualTo(1,
            "the analysis we just submitted should appear in the history");
        root.TryGetProperty("totalCount", out var total).Should().BeTrue();
        total.GetInt32().Should().BeGreaterOrEqualTo(1);
        root.TryGetProperty("page", out _).Should().BeTrue();
        root.TryGetProperty("pageSize", out _).Should().BeTrue();
    }

    // ── GET /api/sentiment/{symbol}/history ─────────────────────────────────

    [Fact]
    public async Task GetHistory_NoData_Returns200WithEmptyItems()
    {
        var response = await _client.GetAsync("/api/sentiment/ZZZZ/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
        doc.RootElement.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetHistory_WithPagination_RespectsPageSize()
    {
        // Submit multiple analyses for AMZN
        for (var i = 0; i < 3; i++)
        {
            var body = JsonBody(new
            {
                symbol = "AMZN",
                text = $"Amazon quarterly report number {i} showed growth in cloud services segment."
            });
            await _client.PostAsync("/api/sentiment/analyze", body);
        }

        var response = await _client.GetAsync("/api/sentiment/AMZN/history?page=1&pageSize=2&includeNeutral=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().BeLessThanOrEqualTo(2);
        doc.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterOrEqualTo(3);
    }

    // ── GET /api/sentiment/{symbol}/stats ───────────────────────────────────

    [Fact]
    public async Task GetStats_WithData_Returns200WithCorrectShape()
    {
        // Ensure at least one analysis exists
        var body = JsonBody(new
        {
            symbol = "TSLA",
            text = "Tesla delivered record number of vehicles this quarter exceeding estimates."
        });
        await _client.PostAsync("/api/sentiment/analyze", body);

        var response = await _client.GetAsync("/api/sentiment/TSLA/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("symbol", out var sym).Should().BeTrue();
        sym.GetString().Should().Be("TSLA");
        root.TryGetProperty("period", out _).Should().BeTrue();
        root.TryGetProperty("totalAnalyses", out var total).Should().BeTrue();
        total.GetInt32().Should().BeGreaterOrEqualTo(1);
        root.TryGetProperty("averageScore", out _).Should().BeTrue();
        root.TryGetProperty("averageConfidence", out _).Should().BeTrue();
        root.TryGetProperty("distribution", out _).Should().BeTrue();
        root.TryGetProperty("trend", out _).Should().BeTrue();
        root.TryGetProperty("highestScore", out _).Should().BeTrue();
        root.TryGetProperty("lowestScore", out _).Should().BeTrue();
        root.TryGetProperty("latestScore", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetStats_NoData_Returns404()
    {
        var response = await _client.GetAsync("/api/sentiment/XXXX/stats");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/sentiment/trending ─────────────────────────────────────────

    [Fact]
    public async Task GetTrending_Returns200WithArrayShape()
    {
        var response = await _client.GetAsync("/api/sentiment/trending");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array,
            "trending endpoint returns an array of trending symbols");
    }

    [Fact]
    public async Task GetTrending_WithQueryParams_Returns200()
    {
        var response = await _client.GetAsync("/api/sentiment/trending?hours=48&limit=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
