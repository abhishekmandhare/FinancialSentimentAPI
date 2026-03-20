using Domain.Entities;
using Domain.Enums;

namespace Application.Features.Sentiment;

/// <summary>
/// Shared decay-weighted statistics used by trending, watchlist, and stats handlers.
/// Half-life = 72 hours: an article's influence halves every 3 days.
/// </summary>
public static class SentimentMath
{
    public const int DefaultHalfLifeHours = 36; // fallback if no config injected

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
        => CalculateTrend(analyses).Direction;

    /// <summary>
    /// Computes all symbol stats in one call: score, previousScore, delta, direction, trend, dispersion, count.
    /// Used by ingestion snapshots, trending fallback, and watchlist fallback.
    /// </summary>
    public static SymbolStats ComputeSymbolStats(
        IReadOnlyList<SentimentAnalysis> analyses,
        DateTime now,
        double windowHours = 14 * 24,
        int halfLifeHours = DefaultHalfLifeHours)
    {
        var midpoint = now.AddHours(-windowHours / 2.0);

        var current = analyses.Where(a => a.AnalyzedAt >= midpoint).ToList();
        var previous = analyses.Where(a => a.AnalyzedAt < midpoint).ToList();

        var score = Math.Round(DecayWeightedAverage(current, now, halfLifeHours), 4);
        var previousScore = Math.Round(DecayWeightedAverage(previous, midpoint, halfLifeHours), 4);
        var delta = Math.Round(score - previousScore, 4);

        var direction = delta switch
        {
            > 0 => "up",
            < 0 => "down",
            _ => "flat"
        };

        var trend = CalculateTrend(analyses);
        var dispersion = CalculateDispersion(analyses, now, halfLifeHours);
        var totalWeight = TotalDecayWeight(analyses, now, halfLifeHours);
        var signalStrength = ClassifySignalStrength(totalWeight);
        var distribution = ComputeDistribution(analyses);
        var shift = ComputeSentimentShift(analyses, now, halfLifeHours);
        var impactful = MostImpactful(analyses, now, halfLifeHours);

        return new SymbolStats(
            score, previousScore, delta, direction,
            trend, dispersion, analyses.Count,
            totalWeight, signalStrength, distribution,
            shift, impactful);
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

    /// <summary>
    /// Linear regression over (day-offset, score) pairs.
    /// Returns both direction label and slope value.
    /// </summary>
    public static TrendResult CalculateTrend(IReadOnlyList<SentimentAnalysis> analyses)
    {
        if (analyses.Count < 2)
            return new TrendResult("Stable", 0);

        var origin = analyses.Min(a => a.AnalyzedAt);
        var points = analyses
            .Select(a => ((a.AnalyzedAt - origin).TotalDays, a.Score.Value))
            .ToList();

        var n = points.Count;
        var sumX = points.Sum(p => p.Item1);
        var sumY = points.Sum(p => p.Item2);
        var sumXY = points.Sum(p => p.Item1 * p.Item2);
        var sumX2 = points.Sum(p => p.Item1 * p.Item1);

        var denominator = n * sumX2 - sumX * sumX;
        if (Math.Abs(denominator) < double.Epsilon)
            return new TrendResult("Stable", 0);

        var slope = Math.Round((n * sumXY - sumX * sumY) / denominator, 4);

        var direction = slope switch
        {
            > 0.005 => "Improving",
            < -0.005 => "Deteriorating",
            _ => "Stable"
        };

        return new TrendResult(direction, slope);
    }

    /// <summary>
    /// Computes the total decay weight across all analyses — used for signal strength.
    /// </summary>
    public static double TotalDecayWeight(
        IReadOnlyList<SentimentAnalysis> analyses,
        DateTime referenceTime,
        int halfLifeHours = DefaultHalfLifeHours)
    {
        var total = 0.0;
        foreach (var a in analyses)
        {
            var ageHours = Math.Max(0, (referenceTime - a.AnalyzedAt).TotalHours);
            total += Math.Exp(-Math.Log(2) / halfLifeHours * ageHours);
        }
        return total;
    }

    public static string ClassifySignalStrength(double totalWeight) => totalWeight switch
    {
        >= 3.0 => "strong",
        >= 1.0 => "moderate",
        _ => "no signal"
    };

    /// <summary>
    /// Computes sentiment shift by comparing current weighted score to snapshots at 24h and 7d ago.
    /// </summary>
    public static (double? Vs24h, double? Vs7d) ComputeSentimentShift(
        IReadOnlyList<SentimentAnalysis> analyses,
        DateTime now,
        int halfLifeHours)
    {
        var currentScore = DecayWeightedAverage(analyses, now, halfLifeHours);
        if (analyses.Count == 0)
            return (null, null);

        var vs24h = ScoreAsOf(analyses, now.AddHours(-24), halfLifeHours);
        var vs7d = ScoreAsOf(analyses, now.AddDays(-7), halfLifeHours);

        return (
            Vs24h: vs24h.HasValue ? Math.Round(currentScore - vs24h.Value, 4) : null,
            Vs7d: vs7d.HasValue ? Math.Round(currentScore - vs7d.Value, 4) : null);
    }

    /// <summary>
    /// Computes decay-weighted average using only articles that existed at the given point in time.
    /// </summary>
    public static double? ScoreAsOf(
        IReadOnlyList<SentimentAnalysis> analyses,
        DateTime asOf,
        int halfLifeHours = DefaultHalfLifeHours)
    {
        var subset = analyses.Where(a => a.AnalyzedAt <= asOf).ToList();
        if (subset.Count == 0)
            return null;

        var totalWeight = 0.0;
        var weightedSum = 0.0;
        foreach (var a in subset)
        {
            var ageHours = (asOf - a.AnalyzedAt).TotalHours;
            var weight = Math.Exp(-Math.Log(2) / halfLifeHours * ageHours);
            totalWeight += weight;
            weightedSum += weight * a.Score.Value;
        }

        return totalWeight > 0 ? weightedSum / totalWeight : null;
    }

    /// <summary>
    /// Finds the analysis with the highest impact: weight × |score|.
    /// Returns the analysis and its weight, or null if empty.
    /// </summary>
    public static (SentimentAnalysis Analysis, double Weight)? MostImpactful(
        IReadOnlyList<SentimentAnalysis> analyses,
        DateTime referenceTime,
        int halfLifeHours = DefaultHalfLifeHours)
    {
        if (analyses.Count == 0) return null;

        SentimentAnalysis? best = null;
        var bestImpact = -1.0;
        var bestWeight = 0.0;

        foreach (var a in analyses)
        {
            var ageHours = Math.Max(0, (referenceTime - a.AnalyzedAt).TotalHours);
            var weight = Math.Exp(-Math.Log(2) / halfLifeHours * ageHours);
            var impact = weight * Math.Abs(a.Score.Value);
            if (impact > bestImpact)
            {
                bestImpact = impact;
                bestWeight = weight;
                best = a;
            }
        }

        return best is not null ? (best, bestWeight) : null;
    }

    public static SentimentDistribution ComputeDistribution(IReadOnlyList<SentimentAnalysis> analyses)
    {
        var total = analyses.Count;
        if (total == 0)
            return new SentimentDistribution(0, 0, 0);

        var positive = analyses.Count(a => a.Label == SentimentLabel.Positive);
        var neutral = analyses.Count(a => a.Label == SentimentLabel.Neutral);
        var negative = analyses.Count(a => a.Label == SentimentLabel.Negative);

        return new SentimentDistribution(
            PositivePercent: Math.Round((double)positive / total * 100, 1),
            NeutralPercent: Math.Round((double)neutral / total * 100, 1),
            NegativePercent: Math.Round((double)negative / total * 100, 1));
    }
}

public record SentimentDistribution(
    double PositivePercent,
    double NeutralPercent,
    double NegativePercent);

public record SymbolStats(
    double Score,
    double PreviousScore,
    double Delta,
    string Direction,
    TrendResult Trend,
    double Dispersion,
    int ArticleCount,
    double TotalWeight,
    string SignalStrength,
    SentimentDistribution Distribution,
    (double? Vs24h, double? Vs7d) Shift,
    (SentimentAnalysis Analysis, double Weight)? MostImpactful);

public record TrendResult(string Direction, double Slope);
