using Microsoft.AspNetCore.Mvc;
using System.Net.Http;

namespace API.Controllers;

/// <summary>
/// Proxies stock price requests to Yahoo Finance to avoid browser CORS restrictions.
/// The dashboard calls this instead of Yahoo directly.
/// </summary>
[ApiController]
[Route("api/prices")]
public class PriceProxyController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet("{symbol}/chart")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ResponseCache(Duration = 300)] // Cache 5 min — prices don't need second-level freshness
    public async Task<IActionResult> GetChart(
        string symbol,
        [FromQuery] string range = "5d",
        [FromQuery] string interval = "1h",
        CancellationToken ct = default)
    {
        // Whitelist allowed ranges/intervals to prevent abuse
        var allowedRanges = new HashSet<string> { "1d", "5d", "1mo", "3mo" };
        var allowedIntervals = new HashSet<string> { "5m", "15m", "1h", "1d" };

        if (!allowedRanges.Contains(range)) range = "5d";
        if (!allowedIntervals.Contains(interval)) interval = "1h";

        var encoded = Uri.EscapeDataString(symbol);
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{encoded}?range={range}&interval={interval}";

        var client = httpClientFactory.CreateClient("YahooFinance");
        try
        {
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return StatusCode(502, new ProblemDetails
                {
                    Title = "Upstream error",
                    Detail = $"Yahoo Finance returned {(int)response.StatusCode}",
                    Status = 502
                });

            var json = await response.Content.ReadAsStringAsync(ct);
            return Content(json, "application/json");
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new ProblemDetails
            {
                Title = "Upstream error",
                Detail = "Failed to reach Yahoo Finance",
                Status = 502
            });
        }
    }
}
