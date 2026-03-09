using API.Controllers.DTOs;
using Application.Features.Sentiment.Commands.AnalyzeSentiment;
using Application.Features.Sentiment.Queries.GetSentimentHistory;
using Application.Features.Sentiment.Queries.GetSentimentStats;
using Application.Features.Sentiment.Queries.GetTrendingSymbols;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;

/// <summary>
/// Thin controller — translates HTTP ↔ MediatR. Zero business logic here.
/// Every real decision is made in a handler. Controllers orchestrate, not compute.
/// </summary>
[ApiController]
[Route("api/sentiment")]
public class SentimentController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Analyze sentiment for a piece of financial text.
    /// Returns 201 Created with the analysis result.
    /// Rate-limited: see RateLimiting:AnalyzePermitLimit in appsettings.json.
    /// </summary>
    [HttpPost("analyze")]
    [EnableRateLimiting(RateLimitPolicies.AnalyzeEndpoint)]
    [ProducesResponseType(typeof(AnalyzeSentimentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Analyze(
        [FromBody] AnalyzeSentimentRequest request,
        CancellationToken ct)
    {
        var response = await sender.Send(
            new AnalyzeSentimentCommand(request.Symbol, request.Text, request.SourceUrl), ct);

        return CreatedAtAction(
            nameof(GetHistory),
            new { symbol = response.Symbol },
            response);
    }

    /// <summary>
    /// Paginated sentiment history for a symbol.
    /// </summary>
    [HttpGet("{symbol}/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(
        string symbol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] bool includeNeutral = false,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetSentimentHistoryQuery(symbol, page, pageSize, from, to, ExcludeNeutral: !includeNeutral), ct);

        return Ok(result);
    }

    /// <summary>
    /// Top symbols ranked by largest sentiment score shift in the given rolling window.
    /// </summary>
    [HttpGet("trending")]
    [ProducesResponseType(typeof(IReadOnlyList<TrendingSymbolDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrending(
        [FromQuery] int hours = 24,
        [FromQuery] int limit = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetTrendingSymbolsQuery(hours, limit, sortBy, sortDirection), ct);
        return Ok(result);
    }

    /// <summary>
    /// Aggregated sentiment statistics and trend for a symbol over a rolling window.
    /// </summary>
    [HttpGet("{symbol}/stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStats(
        string symbol,
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetSentimentStatsQuery(symbol, days), ct);
        return Ok(result);
    }
}
