namespace Application.Features.Sentiment.Commands.AnalyzeSentiment;

/// <summary>
/// The use case result — lives in Application, not API.
/// The controller returns this directly; no extra mapping needed.
/// </summary>
public record AnalyzeSentimentResponse(
    Guid Id,
    string Symbol,
    double Score,
    string Label,
    double Confidence,
    IReadOnlyList<string> KeyReasons,
    string ModelVersion,
    DateTime AnalyzedAt);
