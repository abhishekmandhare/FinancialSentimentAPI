using Domain.Entities;

namespace Application.Features.Sentiment;

/// <summary>
/// Shared decay-weighted statistics used by trending, watchlist, and stats handlers.
/// Half-life = 72 hours: an article's influence halves every 3 days.
/// </summary>
public static class SentimentMath
{
    public const int DefaultHalfLifeHours = 72;

    public static double DecayWeightedAverage(
        IReadOnlyList<SentimentAnalysis> analyses,
        DateTime referenceTime,
        int halfLifeHours = DefaultHalfLifeHours)
    {
        if (analyses.Count == 0)
            return 0.0;

        var totalWeight = 0.0;
        var weightedSum = 0.0;

        foreach (var a in analyses)
        {
            var ageHours = Math.Max(0, (referenceTime - a.AnalyzedAt).TotalHours);
            var weight = Math.Exp(-Math.Log(2) / halfLifeHours * ageHours);
            totalWeight += weight;
            weightedSum += weight * a.Score.Value;
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0.0;
    }

    public static string CalculateTrendDirection(IReadOnlyList<SentimentAnalysis> analyses)
    {
        if (analyses.Count < 2)
            return "Stable";

        var origin = analyses.Min(a => a.AnalyzedAt);
        var points = analyses
            .Select(a => ((a.AnalyzedAt - origin).TotalHours, a.Score.Value))
            .ToList();

        var n = points.Count;
        var sumX = points.Sum(p => p.Item1);
        var sumY = points.Sum(p => p.Item2);
        var sumXY = points.Sum(p => p.Item1 * p.Item2);
        var sumX2 = points.Sum(p => p.Item1 * p.Item1);

        var denominator = n * sumX2 - sumX * sumX;
        if (Math.Abs(denominator) < double.Epsilon)
            return "Stable";

        var slope = (n * sumXY - sumX * sumY) / denominator;

        return slope switch
        {
            > 0.002  => "Improving",
            < -0.002 => "Deteriorating",
            _        => "Stable"
        };
    }

    /// <summary>
    /// Computes all symbol stats in one call: score, previousScore, delta, direction, trend, dispersion, count.
    /// Used by ingestion snapshots, trending fallback, and watchlist fallback.
    /// </summary>
    public static SymbolStats ComputeSymbolStats(
        IReadOnlyList<SentimentAnalysis> analyses,
        DateTime now,
        double windowHours = 7 * 24)
    {
        var midpoint = now.AddHours(-windowHours / 2.0);

        var current = analyses.Where(a => a.AnalyzedAt >= midpoint).ToList();
        var previous = analyses.Where(a => a.AnalyzedAt < midpoint).ToList();

        var score = Math.Round(DecayWeightedAverage(current, now), 4);
        var previousScore = Math.Round(DecayWeightedAverage(previous, midpoint), 4);
        var delta = Math.Round(score - previousScore, 4);

        var direction = delta switch
        {
            > 0 => "up",
            < 0 => "down",
            _ => "flat"
        };

        var trend = CalculateTrendDirection(analyses);
        var dispersion = CalculateDispersion(analyses, now);

        return new SymbolStats(score, previousScore, delta, direction, trend, dispersion, analyses.Count);
    }

    public static double CalculateDispersion(
        IReadOnlyList<SentimentAnalysis> analyses,
        DateTime now,
        int halfLifeHours = DefaultHalfLifeHours)
    {
        if (analyses.Count < 2)
            return 0.0;

        var totalWeight = 0.0;
        var weightedSum = 0.0;

        foreach (var a in analyses)
        {
            var ageHours = Math.Max(0, (now - a.AnalyzedAt).TotalHours);
            var weight = Math.Exp(-Math.Log(2) / halfLifeHours * ageHours);
            totalWeight += weight;
            weightedSum += weight * a.Score.Value;
        }

        if (totalWeight <= 0) return 0.0;
        var mean = weightedSum / totalWeight;

        var varianceSum = 0.0;
        foreach (var a in analyses)
        {
            var ageHours = Math.Max(0, (now - a.AnalyzedAt).TotalHours);
            var weight = Math.Exp(-Math.Log(2) / halfLifeHours * ageHours);
            varianceSum += weight * Math.Pow(a.Score.Value - mean, 2);
        }

        return Math.Round(Math.Sqrt(varianceSum / totalWeight), 4);
    }
}

public record SymbolStats(
    double Score,
    double PreviousScore,
    double Delta,
    string Direction,
    string Trend,
    double Dispersion,
    int ArticleCount);
