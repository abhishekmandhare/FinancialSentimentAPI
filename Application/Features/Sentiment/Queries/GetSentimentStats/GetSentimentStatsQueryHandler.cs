using Application.Configuration;
using Application.Exceptions;
using Domain.Enums;
using Domain.Entities;
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

        var weightedScore = Math.Round(SentimentMath.DecayWeightedAverage(analyses, now, halfLifeHours), 4);
        var dispersion = SentimentMath.CalculateDispersion(analyses, now, halfLifeHours);
        var totalWeight = SentimentMath.TotalDecayWeight(analyses, now, halfLifeHours);
        var signalStrength = SentimentMath.ClassifySignalStrength(totalWeight);
        var trend = SentimentMath.CalculateTrend(analyses);
        var shift = SentimentMath.ComputeSentimentShift(analyses, now, halfLifeHours);

        var distribution = SentimentMath.ComputeDistribution(analyses);

        var highest = analyses.MaxBy(a => a.Score.Value)!;
        var lowest = analyses.MinBy(a => a.Score.Value)!;
        var latest = analyses.MaxBy(a => a.AnalyzedAt)!;

        var mostRecent = new ArticleContext(
            Headline: TruncateToHeadline(latest.OriginalText),
            Score: latest.Score.Value,
            AnalyzedAt: latest.AnalyzedAt);

        var impactful = SentimentMath.MostImpactful(analyses, now, halfLifeHours);
        var impactfulDto = impactful is not null
            ? new ImpactfulArticle(
                Headline: TruncateToHeadline(impactful.Value.Analysis.OriginalText),
                Score: impactful.Value.Analysis.Score.Value,
                Weight: Math.Round(impactful.Value.Weight, 4),
                AnalyzedAt: impactful.Value.Analysis.AnalyzedAt)
            : null;

        return new SentimentStatsDto(
            Symbol: symbol.Value,
            Period: new StatsPeriod(now.AddDays(-days), now, days),
            TotalAnalyses: analyses.Count,
            WeightedScore: weightedScore,
            AverageConfidence: Math.Round(analyses.Average(a => a.Confidence), 4),
            SignalStrength: signalStrength,
            Dispersion: dispersion,
            HalfLifeHours: halfLifeHours,
            Distribution: distribution,
            Trend: new SentimentTrend(trend.Direction, trend.Slope),
            HighestScore: new ScoreDataPoint(highest.Score.Value, highest.AnalyzedAt),
            LowestScore: new ScoreDataPoint(lowest.Score.Value, lowest.AnalyzedAt),
            LatestScore: latest.Score.Value,
            MostRecentArticle: mostRecent,
            MostImpactfulArticle: impactfulDto,
            SentimentShift: new SentimentShift(shift.Vs24h, shift.Vs7d));
    }

    private static SentimentDistribution ComputeDistribution(IReadOnlyList<SentimentAnalysis> analyses)
    {
        var total = analyses.Count;
        var positive = analyses.Count(a => a.Label == SentimentLabel.Positive);
        var neutral = analyses.Count(a => a.Label == SentimentLabel.Neutral);
        var negative = analyses.Count(a => a.Label == SentimentLabel.Negative);

        return new SentimentDistribution(
            PositivePercent: Math.Round((double)positive / total * 100, 1),
            NeutralPercent: Math.Round((double)neutral / total * 100, 1),
            NegativePercent: Math.Round((double)negative / total * 100, 1));
    }

    private static string TruncateToHeadline(string text)
    {
        var firstSentenceEnd = text.IndexOfAny(['.', '!', '?']);
        if (firstSentenceEnd > 0 && firstSentenceEnd < 150)
            return text[..(firstSentenceEnd + 1)];

        return text.Length <= 100 ? text : text[..100] + "...";
    }
}
