namespace Application.Features.Sentiment.Queries.GetSentimentHistory;

public record SentimentHistoryDto(
    Guid Id,
    double Score,
    string Label,
    double Confidence,
    IReadOnlyList<string> KeyReasons,
    string? SourceUrl,
    string ModelVersion,
    DateTime AnalyzedAt);
