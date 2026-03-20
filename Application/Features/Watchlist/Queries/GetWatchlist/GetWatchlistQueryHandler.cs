using Application.Features.Sentiment;
using Domain.Interfaces;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Watchlist.Queries.GetWatchlist;

public class GetWatchlistQueryHandler(
    ITrackedSymbolRepository trackedSymbolRepository,
    ISentimentRepository sentimentRepository)
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
            var analyses = await sentimentRepository.GetForStatsAsync(
                new StockSymbol(tracked.Symbol), DataWindowDays, ct);

            var score = SentimentMath.DecayWeightedAverage(analyses, now);
            var trend = SentimentMath.CalculateTrendDirection(analyses);
            var dispersion = SentimentMath.CalculateDispersion(analyses, now);

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
}
