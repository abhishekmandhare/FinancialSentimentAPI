using Domain.ValueObjects;

namespace Application.Services;

/// <summary>
/// The AI sentiment result returned from any AI provider.
/// The domain converts Score → Label; the AI returns raw numbers only.
/// </summary>
public record AiSentimentResult(
    double Score,
    double Confidence,
    IReadOnlyList<string> KeyReasons,
    string ModelVersion);

/// <summary>
/// Abstraction over any AI provider (Anthropic, OpenAI, Mock).
/// Application depends on this interface — Infrastructure implements it.
/// Switching providers = swap one registration in DI. Zero other changes.
/// </summary>
public interface IAiSentimentService
{
    Task<AiSentimentResult> AnalyzeAsync(string text, StockSymbol symbol, CancellationToken ct = default);
}
