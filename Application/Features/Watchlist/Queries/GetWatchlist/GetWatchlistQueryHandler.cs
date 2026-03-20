using Application.Configuration;
using Application.Features.Sentiment;
using Domain.Interfaces;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Watchlist.Queries.GetWatchlist;

public class GetWatchlistQueryHandler(
    ITrackedSymbolRepository trackedSymbolRepository,
    ISentimentRepository sentimentRepository,
    ISymbolSnapshotRepository snapshotRepository,
    SentimentScoringOptions scoringOptions)
    : IRequestHandler<GetWatchlistQuery, IReadOnlyList<WatchlistSymbolDto>>
{

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
            // Fast path: read precomputed snapshot
            var snapshot = await snapshotRepository.GetBySymbolAsync(tracked.Symbol, ct);

            if (snapshot is not null)
            {
                results.Add(new WatchlistSymbolDto(
                    tracked.Symbol, tracked.AddedAt,
                    snapshot.Score, snapshot.Trend, snapshot.Dispersion, snapshot.ArticleCount));
                continue;
            }

            // Fallback: compute on-the-fly (new symbol, no snapshot yet)
            var analyses = await sentimentRepository.GetForStatsAsync(
                new StockSymbol(tracked.Symbol), scoringOptions.DefaultWindowDays, ct);

            var stats = SentimentMath.ComputeSymbolStats(
                analyses, now, scoringOptions.DefaultWindowDays * 24.0, scoringOptions.HalfLifeHours);

            results.Add(new WatchlistSymbolDto(
                tracked.Symbol, tracked.AddedAt,
                stats.Score, stats.Trend.Direction, stats.Dispersion, stats.ArticleCount));
        }

        return results;
    }
}
