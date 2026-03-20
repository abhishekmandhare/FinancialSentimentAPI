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
