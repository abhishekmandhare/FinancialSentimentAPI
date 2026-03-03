namespace Application.Features.Sentiment.Queries.GetSentimentStats;

public record SentimentStatsDto(
    string Symbol,
    StatsPeriod Period,
    int TotalAnalyses,
    double AverageScore,
    double AverageConfidence,
    SentimentDistribution Distribution,
    SentimentTrend Trend,
    ScoreDataPoint HighestScore,
    ScoreDataPoint LowestScore,
    double LatestScore);

public record StatsPeriod(DateTime From, DateTime To, int Days);

public record SentimentDistribution(
    double PositivePercent,
    double NeutralPercent,
    double NegativePercent);

public record SentimentTrend(
    string Direction,   // "Improving" | "Deteriorating" | "Stable"
    double Slope);      // score change per day (linear regression)

public record ScoreDataPoint(double Score, DateTime Date);
