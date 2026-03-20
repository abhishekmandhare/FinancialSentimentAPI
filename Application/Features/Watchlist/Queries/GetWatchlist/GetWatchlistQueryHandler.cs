using Application.Features.Sentiment;
using Domain.Interfaces;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Watchlist.Queries.GetWatchlist;

public class GetWatchlistQueryHandler(
    ITrackedSymbolRepository trackedSymbolRepository,
    ISentimentRepository sentimentRepository,
    ISymbolSnapshotRepository snapshotRepository)
    : IRequestHandler<GetWatchlistQuery, IReadOnlyList<WatchlistSymbolDto>>
{
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
                new StockSymbol(tracked.Symbol), DataWindowDays, ct);

            var stats = SentimentMath.ComputeSymbolStats(analyses, now, DataWindowDays * 24);

            results.Add(new WatchlistSymbolDto(
                tracked.Symbol, tracked.AddedAt,
                stats.Score, stats.Trend, stats.Dispersion, stats.ArticleCount));
        }

        return results;
    }
}
