using System.Text.Json;
using Application.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class YahooSymbolValidationService(
    HttpClient httpClient,
    ILogger<YahooSymbolValidationService> logger) : ISymbolValidationService
{
    public async Task<bool> IsValidSymbolAsync(string symbol, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(symbol.ToUpperInvariant());
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{encoded}?range=1d&interval=1d";

        try
        {
            var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            // Yahoo returns a valid chart result with a non-null "meta" section for real symbols.
            // For invalid symbols, the result array is empty or contains an error.
            var result = doc.RootElement
                .GetProperty("chart")
                .GetProperty("result");

            return result.GetArrayLength() > 0;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Yahoo Finance HTTP error validating symbol {Symbol}", symbol);
            return false;
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, "Yahoo Finance request timed out for symbol {Symbol}", symbol);
            return false;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Yahoo Finance returned invalid JSON for symbol {Symbol}", symbol);
            return false;
        }
    }
}
