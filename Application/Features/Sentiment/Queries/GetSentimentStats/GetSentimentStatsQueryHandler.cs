using Application.Exceptions;
using Domain.Enums;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Sentiment.Queries.GetSentimentStats;

public class GetSentimentStatsQueryHandler(ISentimentRepository repository)
    : IRequestHandler<GetSentimentStatsQuery, SentimentStatsDto>
{
    public async Task<SentimentStatsDto> Handle(GetSentimentStatsQuery query, CancellationToken ct)
    {
        var symbol = new StockSymbol(query.Symbol);

        var analyses = await repository.GetForStatsAsync(symbol, query.Days, ct);

        if (analyses.Count == 0)
            throw new NotFoundException("SentimentAnalysis", $"symbol={query.Symbol}, days={query.Days}");

        var to   = DateTime.UtcNow;
        var from = to.AddDays(-query.Days);

        var scores    = analyses.Select(a => a.Score.Value).ToList();
        var total     = analyses.Count;
        var avgScore  = Math.Round(scores.Average(), 4);
        var avgConf   = Math.Round(analyses.Average(a => a.Confidence), 4);

        var positiveCount = analyses.Count(a => a.Label == SentimentLabel.Positive);
        var neutralCount  = analyses.Count(a => a.Label == SentimentLabel.Neutral);
        var negativeCount = analyses.Count(a => a.Label == SentimentLabel.Negative);

        var distribution = new SentimentDistribution(
            PositivePercent: Math.Round((double)positiveCount / total * 100, 1),
            NeutralPercent:  Math.Round((double)neutralCount  / total * 100, 1),
            NegativePercent: Math.Round((double)negativeCount / total * 100, 1));

        var trend = CalculateTrend(analyses);

        var highest = analyses.MaxBy(a => a.Score.Value)!;
        var lowest  = analyses.MinBy(a => a.Score.Value)!;
        var latest  = analyses.MaxBy(a => a.AnalyzedAt)!;

        return new SentimentStatsDto(
            Symbol:          symbol.Value,
            Period:          new StatsPeriod(from, to, query.Days),
            TotalAnalyses:   total,
            AverageScore:    avgScore,
            AverageConfidence: avgConf,
            Distribution:    distribution,
            Trend:           trend,
            HighestScore:    new ScoreDataPoint(highest.Score.Value, highest.AnalyzedAt),
            LowestScore:     new ScoreDataPoint(lowest.Score.Value,  lowest.AnalyzedAt),
            LatestScore:     latest.Score.Value);
    }

    /// <summary>
    /// Linear regression over (day-offset, score) pairs.
    /// Slope > 0 = improving sentiment over time; slope &lt; 0 = deteriorating.
    /// Thresholds (±0.005/day) are intentionally small — financial sentiment shifts gradually.
    /// </summary>
    private static SentimentTrend CalculateTrend(IReadOnlyList<SentimentAnalysis> analyses)
    {
        if (analyses.Count < 2)
            return new SentimentTrend("Stable", 0);

        var origin = analyses.Min(a => a.AnalyzedAt);
        var points = analyses
            .Select(a => ((a.AnalyzedAt - origin).TotalDays, a.Score.Value))
            .ToList();

        var n    = points.Count;
        var sumX = points.Sum(p => p.Item1);
        var sumY = points.Sum(p => p.Item2);
        var sumXY = points.Sum(p => p.Item1 * p.Item2);
        var sumX2 = points.Sum(p => p.Item1 * p.Item1);

        var denominator = n * sumX2 - sumX * sumX;
        if (Math.Abs(denominator) < double.Epsilon)
            return new SentimentTrend("Stable", 0);

        var slope = (n * sumXY - sumX * sumY) / denominator;
        var roundedSlope = Math.Round(slope, 4);

        var direction = roundedSlope switch
        {
            > 0.005  => "Improving",
            < -0.005 => "Deteriorating",
            _        => "Stable"
        };

        return new SentimentTrend(direction, roundedSlope);
    }
}
