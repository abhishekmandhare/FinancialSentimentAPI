using Application.Features.Sentiment;

namespace Application.Features.Sentiment.Queries.GetSentimentStats;

public record SentimentStatsDto(
    string Symbol,
    StatsPeriod Period,
    int TotalAnalyses,
    double WeightedScore,
    double AverageConfidence,
    string SignalStrength,
    double Dispersion,
    int HalfLifeHours,
    SentimentDistribution Distribution,
    SentimentTrend Trend,
    ScoreDataPoint HighestScore,
    ScoreDataPoint LowestScore,
    double LatestScore,
    ArticleContext? MostRecentArticle,
    ImpactfulArticle? MostImpactfulArticle,
    SentimentShift SentimentShift);

public record StatsPeriod(DateTime From, DateTime To, int Days);

public record SentimentTrend(
    string Direction,   // "Improving" | "Deteriorating" | "Stable"
    double Slope);      // score change per day (linear regression)

public record ScoreDataPoint(double Score, DateTime Date);

public record ArticleContext(
    string Headline,
    double Score,
    DateTime AnalyzedAt);

public record ImpactfulArticle(
    string Headline,
    double Score,
    double Weight,
    DateTime AnalyzedAt);

public record SentimentShift(
    double? Vs24h,
    double? Vs7d);
