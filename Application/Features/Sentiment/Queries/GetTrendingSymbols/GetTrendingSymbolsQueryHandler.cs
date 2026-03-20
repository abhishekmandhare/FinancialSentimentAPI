using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Sentiment.Queries.GetTrendingSymbols;

public class GetTrendingSymbolsQueryHandler(ISentimentRepository repository)
    : IRequestHandler<GetTrendingSymbolsQuery, IReadOnlyList<TrendingSymbolDto>>
{
    private const int DefaultHalfLifeHours = 72;

    public async Task<IReadOnlyList<TrendingSymbolDto>> Handle(
        GetTrendingSymbolsQuery query,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddHours(-query.Hours);
        var midpoint = now.AddHours(-query.Hours / 2.0);

        var analyses = await repository.GetRecentAsync(windowStart, ct);

        if (analyses.Count == 0)
            return [];

        var grouped = analyses.GroupBy(a => a.Symbol.Value);

        var unordered = grouped
            .Select(g => ComputeTrend(g.Key, g.ToList(), midpoint, now));

        var sorted = ApplySort(unordered, query.SortBy, query.SortDirection);

        var results = sorted
            .Take(query.Limit)
            .ToList();

        return results;
    }

    private static IOrderedEnumerable<TrendingSymbolDto> ApplySort(
        IEnumerable<TrendingSymbolDto> items,
        string? sortBy,
        string? sortDirection)
    {
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        Func<TrendingSymbolDto, object> keySelector = sortBy?.ToLowerInvariant() switch
        {
            "symbol"     => t => t.Symbol,
            "score"      => t => t.CurrentAvgScore,
            "dispersion" => t => t.Dispersion,
            "articles"   => t => t.ArticleCount,
            _            => t => Math.Abs(t.Delta)
        };

        return descending
            ? items.OrderByDescending(keySelector)
            : items.OrderBy(keySelector);
    }

    private static TrendingSymbolDto ComputeTrend(
        string symbol,
        IReadOnlyList<SentimentAnalysis> analyses,
        DateTime midpoint,
        DateTime now)
    {
        var current  = analyses.Where(a => a.AnalyzedAt >= midpoint).ToList();
        var previous = analyses.Where(a => a.AnalyzedAt <  midpoint).ToList();

        var currentAvg  = DecayWeightedAverage(current, now);
        var previousAvg = DecayWeightedAverage(previous, midpoint);

        var delta = Math.Round(currentAvg - previousAvg, 4);
        currentAvg  = Math.Round(currentAvg,  4);
        previousAvg = Math.Round(previousAvg, 4);

        var direction = delta switch
        {
            > 0  => "up",
            < 0  => "down",
            _    => "flat"
        };

        // Trend via linear regression over the full window
        var trend = CalculateTrendDirection(analyses);

        // Dispersion: weighted std dev over all analyses
        var dispersion = CalculateDispersion(analyses, now);

        return new TrendingSymbolDto(symbol, currentAvg, previousAvg, delta, direction,
            trend, dispersion, analyses.Count);
    }

    private static string CalculateTrendDirection(IReadOnlyList<SentimentAnalysis> analyses)
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

    private static double CalculateDispersion(IReadOnlyList<SentimentAnalysis> analyses, DateTime now)
    {
        if (analyses.Count < 2)
            return 0.0;

        var totalWeight = 0.0;
        var weightedSum = 0.0;

        foreach (var a in analyses)
        {
            var ageHours = Math.Max(0, (now - a.AnalyzedAt).TotalHours);
            var weight = Math.Exp(-Math.Log(2) / DefaultHalfLifeHours * ageHours);
            totalWeight += weight;
            weightedSum += weight * a.Score.Value;
        }

        if (totalWeight <= 0) return 0.0;
        var mean = weightedSum / totalWeight;

        var varianceSum = 0.0;
        foreach (var a in analyses)
        {
            var ageHours = Math.Max(0, (now - a.AnalyzedAt).TotalHours);
            var weight = Math.Exp(-Math.Log(2) / DefaultHalfLifeHours * ageHours);
            varianceSum += weight * Math.Pow(a.Score.Value - mean, 2);
        }

        return Math.Round(Math.Sqrt(varianceSum / totalWeight), 4);
    }

    private static double DecayWeightedAverage(
        IReadOnlyList<SentimentAnalysis> analyses,
        DateTime referenceTime)
    {
        if (analyses.Count == 0)
            return 0.0;

        var totalWeight = 0.0;
        var weightedSum = 0.0;

        foreach (var a in analyses)
        {
            var ageHours = (referenceTime - a.AnalyzedAt).TotalHours;
            if (ageHours < 0) ageHours = 0;
            var weight = Math.Exp(-Math.Log(2) / DefaultHalfLifeHours * ageHours);
            totalWeight += weight;
            weightedSum += weight * a.Score.Value;
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0.0;
    }
}
