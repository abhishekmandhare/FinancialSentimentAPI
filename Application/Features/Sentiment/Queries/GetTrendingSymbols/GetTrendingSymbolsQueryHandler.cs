using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Sentiment.Queries.GetTrendingSymbols;

public class GetTrendingSymbolsQueryHandler(
    ISentimentRepository repository,
    ISymbolSnapshotRepository snapshotRepository)
    : IRequestHandler<GetTrendingSymbolsQuery, IReadOnlyList<TrendingSymbolDto>>
{
    public async Task<IReadOnlyList<TrendingSymbolDto>> Handle(
        GetTrendingSymbolsQuery query,
        CancellationToken ct)
    {
        // Fast path: read precomputed snapshots
        var snapshots = await snapshotRepository.GetAllAsync(ct);

        if (snapshots.Count > 0)
        {
            var dtos = snapshots.Select(s => new TrendingSymbolDto(
                s.Symbol, s.Score, s.PreviousScore, s.Delta, s.Direction,
                s.Trend, s.Dispersion, s.ArticleCount));

            return ApplySort(dtos, query.SortBy, query.SortDirection)
                .Take(query.Limit)
                .ToList();
        }

        // Fallback: compute on-the-fly (cold start, no snapshots yet)
        return await ComputeLive(query, ct);
    }

    private async Task<IReadOnlyList<TrendingSymbolDto>> ComputeLive(
        GetTrendingSymbolsQuery query, CancellationToken ct)
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

        return ApplySort(unordered, query.SortBy, query.SortDirection)
            .Take(query.Limit)
            .ToList();
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

        var currentAvg  = SentimentMath.DecayWeightedAverage(current, now);
        var previousAvg = SentimentMath.DecayWeightedAverage(previous, midpoint);

        var delta = Math.Round(currentAvg - previousAvg, 4);
        currentAvg  = Math.Round(currentAvg,  4);
        previousAvg = Math.Round(previousAvg, 4);

        var direction = delta switch
        {
            > 0  => "up",
            < 0  => "down",
            _    => "flat"
        };

        var trend = SentimentMath.CalculateTrendDirection(analyses);
        var dispersion = SentimentMath.CalculateDispersion(analyses, now);

        return new TrendingSymbolDto(symbol, currentAvg, previousAvg, delta, direction,
            trend, dispersion, analyses.Count);
    }
}
