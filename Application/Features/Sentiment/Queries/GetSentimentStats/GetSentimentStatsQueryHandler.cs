using Application.Configuration;
using Application.Exceptions;
using Domain.Interfaces;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Sentiment.Queries.GetSentimentStats;

public class GetSentimentStatsQueryHandler(
    ISentimentRepository repository,
    SentimentScoringOptions scoringOptions)
    : IRequestHandler<GetSentimentStatsQuery, SentimentStatsDto>
{
    public async Task<SentimentStatsDto> Handle(GetSentimentStatsQuery query, CancellationToken ct)
    {
        var symbol = new StockSymbol(query.Symbol);
        var days = Math.Min(query.Days, scoringOptions.MaxDataAgeDays);
        var analyses = await repository.GetForStatsAsync(symbol, days, ct);

        if (analyses.Count == 0)
            throw new NotFoundException("SentimentAnalysis", $"symbol={query.Symbol}, days={days}");

        var now = DateTime.UtcNow;
        var halfLifeHours = query.HalfLifeHours > 0 ? query.HalfLifeHours : scoringOptions.HalfLifeHours;

        var stats = SentimentMath.ComputeSymbolStats(analyses, now, days * 24.0, halfLifeHours);

        var highest = analyses.MaxBy(a => a.Score.Value)!;
        var lowest = analyses.MinBy(a => a.Score.Value)!;
        var latest = analyses.MaxBy(a => a.AnalyzedAt)!;

        var mostRecent = new ArticleContext(
            Headline: TruncateToHeadline(latest.OriginalText),
            Score: latest.Score.Value,
            AnalyzedAt: latest.AnalyzedAt);

        var impactfulDto = stats.MostImpactful is not null
            ? new ImpactfulArticle(
                Headline: TruncateToHeadline(stats.MostImpactful.Value.Analysis.OriginalText),
                Score: stats.MostImpactful.Value.Analysis.Score.Value,
                Weight: Math.Round(stats.MostImpactful.Value.Weight, 4),
                AnalyzedAt: stats.MostImpactful.Value.Analysis.AnalyzedAt)
            : null;

        return new SentimentStatsDto(
            Symbol: symbol.Value,
            Period: new StatsPeriod(now.AddDays(-days), now, days),
            TotalAnalyses: stats.ArticleCount,
            WeightedScore: stats.Score,
            AverageConfidence: Math.Round(analyses.Average(a => a.Confidence), 4),
            SignalStrength: stats.SignalStrength,
            Dispersion: stats.Dispersion,
            HalfLifeHours: halfLifeHours,
            Distribution: stats.Distribution,
            Trend: new SentimentTrend(stats.Trend.Direction, stats.Trend.Slope),
            HighestScore: new ScoreDataPoint(highest.Score.Value, highest.AnalyzedAt),
            LowestScore: new ScoreDataPoint(lowest.Score.Value, lowest.AnalyzedAt),
            LatestScore: latest.Score.Value,
            MostRecentArticle: mostRecent,
            MostImpactfulArticle: impactfulDto,
            SentimentShift: new SentimentShift(stats.Shift.Vs24h, stats.Shift.Vs7d));
    }

    private static string TruncateToHeadline(string text)
    {
        var firstSentenceEnd = text.IndexOfAny(['.', '!', '?']);
        if (firstSentenceEnd > 0 && firstSentenceEnd < 150)
            return text[..(firstSentenceEnd + 1)];

        return text.Length <= 100 ? text : text[..100] + "...";
    }
}
