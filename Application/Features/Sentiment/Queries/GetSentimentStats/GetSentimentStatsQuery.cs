using MediatR;

namespace Application.Features.Sentiment.Queries.GetSentimentStats;

/// <summary>
/// Returns aggregated sentiment statistics for a symbol over a rolling window.
/// Trend is calculated via linear regression over the period — slope tells you
/// direction of travel, not just the current value.
/// </summary>
public record GetSentimentStatsQuery(
    string Symbol,
    int Days = 30) : IRequest<SentimentStatsDto>;
