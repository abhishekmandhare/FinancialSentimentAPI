using Application.Services;
using Domain.ValueObjects;

namespace Infrastructure.Services;

/// <summary>
/// Deterministic mock — same text always produces the same score.
/// Uses text hash so results are stable across runs (useful for tests and demos).
/// Switchable via appsettings: "AI": { "Provider": "Mock" }
/// </summary>
public class MockSentimentService : IAiSentimentService
{
    public Task<AiSentimentResult> AnalyzeAsync(
        string text,
        StockSymbol symbol,
        CancellationToken ct = default)
    {
        // Hash-based score: deterministic, spans the full -1.0 → 1.0 range
        var hash  = Math.Abs(text.GetHashCode());
        var score = Math.Round(((hash % 200) / 100.0) - 1.0, 2);
        score = Math.Clamp(score, -1.0, 1.0);

        var result = new AiSentimentResult(
            Score:        score,
            Confidence:   0.85,
            KeyReasons:
            [
                $"Mock analysis for {symbol.Value}",
                "Simulated sentiment result — set AI:Provider to 'Anthropic' for real analysis",
                $"Text hash: {hash}"
            ],
            ModelVersion: "mock-v1");

        return Task.FromResult(result);
    }
}
