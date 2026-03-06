using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Sentiment.Queries.GetTrendingSymbols;

public class GetTrendingSymbolsQueryHandler(ISentimentRepository repository)
    : IRequestHandler<GetTrendingSymbolsQuery, IReadOnlyList<TrendingSymbolDto>>
{
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

        var results = grouped
            .Select(g => ComputeTrend(g.Key, g.ToList(), midpoint))
            .OrderByDescending(t => Math.Abs(t.Delta))
            .Take(query.Limit)
            .ToList();

        return results;
    }

    private static TrendingSymbolDto ComputeTrend(
        string symbol,
        IReadOnlyList<SentimentAnalysis> analyses,
        DateTime midpoint)
    {
        var current  = analyses.Where(a => a.AnalyzedAt >= midpoint).ToList();
        var previous = analyses.Where(a => a.AnalyzedAt <  midpoint).ToList();

        var currentAvg  = current.Count  > 0 ? current.Average(a => a.Score.Value)  : 0.0;
        var previousAvg = previous.Count > 0 ? previous.Average(a => a.Score.Value) : 0.0;

        var delta = Math.Round(currentAvg - previousAvg, 4);
        currentAvg  = Math.Round(currentAvg,  4);
        previousAvg = Math.Round(previousAvg, 4);

        var direction = delta switch
        {
            > 0  => "up",
            < 0  => "down",
            _    => "flat"
        };

        return new TrendingSymbolDto(symbol, currentAvg, previousAvg, delta, direction);
    }
}
