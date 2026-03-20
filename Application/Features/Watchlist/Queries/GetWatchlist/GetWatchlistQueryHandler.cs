using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Watchlist.Queries.GetWatchlist;

public class GetWatchlistQueryHandler(
    ITrackedSymbolRepository trackedSymbolRepository,
    ISentimentRepository sentimentRepository)
    : IRequestHandler<GetWatchlistQuery, IReadOnlyList<WatchlistSymbolDto>>
{
    private const int DefaultHalfLifeHours = 72;
    private const int DataWindowDays = 7;

    public async Task<IReadOnlyList<WatchlistSymbolDto>> Handle(
        GetWatchlistQuery request, CancellationToken ct)
    {
        var watchlistSymbols = await trackedSymbolRepository.GetBySourceAsync("watchlist", ct);

        if (watchlistSymbols.Count == 0)
            return [];

        var now = DateTime.UtcNow;
        var results = new List<WatchlistSymbolDto>();

        foreach (var tracked in watchlistSymbols)
        {
            var analyses = await sentimentRepository.GetForStatsAsync(
                new StockSymbol(tracked.Symbol), DataWindowDays, ct);

            var score = DecayWeightedAverage(analyses, now);
            var trend = CalculateTrendDirection(analyses);
            var dispersion = CalculateDispersion(analyses, now);

            results.Add(new WatchlistSymbolDto(
                tracked.Symbol,
                tracked.AddedAt,
                Math.Round(score, 4),
                trend,
                dispersion,
                analyses.Count));
        }

        return results;
    }

    private static double DecayWeightedAverage(
        IReadOnlyList<SentimentAnalysis> analyses, DateTime now)
    {
        if (analyses.Count == 0) return 0.0;

        var totalWeight = 0.0;
        var weightedSum = 0.0;

        foreach (var a in analyses)
        {
            var ageHours = Math.Max(0, (now - a.AnalyzedAt).TotalHours);
            var weight = Math.Exp(-Math.Log(2) / DefaultHalfLifeHours * ageHours);
            totalWeight += weight;
            weightedSum += weight * a.Score.Value;
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0.0;
    }

    private static string CalculateTrendDirection(IReadOnlyList<SentimentAnalysis> analyses)
    {
        if (analyses.Count < 2) return "Stable";

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
        if (Math.Abs(denominator) < double.Epsilon) return "Stable";

        var slope = (n * sumXY - sumX * sumY) / denominator;

        return slope switch
        {
            > 0.002  => "Improving",
            < -0.002 => "Deteriorating",
            _        => "Stable"
        };
    }

    private static double CalculateDispersion(
        IReadOnlyList<SentimentAnalysis> analyses, DateTime now)
    {
        if (analyses.Count < 2) return 0.0;

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
}
