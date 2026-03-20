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
        var from = now.AddDays(-days);
        var halfLifeHours = query.HalfLifeHours > 0 ? query.HalfLifeHours : scoringOptions.HalfLifeHours;

        // Compute time-decay weights for each analysis
        var weighted = analyses.Select(a =>
        {
            var ageHours = (now - a.AnalyzedAt).TotalHours;
            var weight = Math.Exp(-Math.Log(2) / halfLifeHours * ageHours);
            return (Analysis: a, Weight: weight);
        }).ToList();

        var totalWeight = weighted.Sum(w => w.Weight);

        // Exponential decay weighted average score
        var weightedScore = totalWeight > 0
            ? Math.Round(weighted.Sum(w => w.Weight * w.Analysis.Score.Value) / totalWeight, 4)
            : 0.0;

        // Dispersion: weighted standard deviation
        var dispersion = totalWeight > 0
            ? Math.Round(Math.Sqrt(
                weighted.Sum(w => w.Weight * Math.Pow(w.Analysis.Score.Value - weightedScore, 2)) / totalWeight), 4)
            : 0.0;

        // Signal strength based on total weight
        var signalStrength = totalWeight switch
        {
            >= 3.0 => "strong",
            >= 1.0 => "moderate",
            _      => "no signal"
        };

        var avgConf = Math.Round(analyses.Average(a => a.Confidence), 4);

        // Distribution
        var total = analyses.Count;
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

        // Most recent article
        var mostRecent = new ArticleContext(
            Headline: TruncateToHeadline(latest.OriginalText),
            Score: latest.Score.Value,
            AnalyzedAt: latest.AnalyzedAt);

        // Most impactful article (highest weight × |score|)
        var mostImpactful = weighted
            .OrderByDescending(w => w.Weight * Math.Abs(w.Analysis.Score.Value))
            .First();
        var impactfulDto = new ImpactfulArticle(
            Headline: TruncateToHeadline(mostImpactful.Analysis.OriginalText),
            Score: mostImpactful.Analysis.Score.Value,
            Weight: Math.Round(mostImpactful.Weight, 4),
            AnalyzedAt: mostImpactful.Analysis.AnalyzedAt);

        // Sentiment shift: compare current weighted score to 24h ago and 7d ago
        var shift = ComputeSentimentShift(weighted, now, halfLifeHours);

        return new SentimentStatsDto(
            Symbol:              symbol.Value,
            Period:              new StatsPeriod(from, now, days),
            TotalAnalyses:       total,
            WeightedScore:       weightedScore,
            AverageConfidence:   avgConf,
            SignalStrength:      signalStrength,
            Dispersion:          dispersion,
            HalfLifeHours:       halfLifeHours,
            Distribution:        distribution,
            Trend:               trend,
            HighestScore:        new ScoreDataPoint(highest.Score.Value, highest.AnalyzedAt),
            LowestScore:         new ScoreDataPoint(lowest.Score.Value,  lowest.AnalyzedAt),
            LatestScore:         latest.Score.Value,
            MostRecentArticle:   mostRecent,
            MostImpactfulArticle: impactfulDto,
            SentimentShift:      shift);
    }

    private static SentimentShift ComputeSentimentShift(
        List<(SentimentAnalysis Analysis, double Weight)> weighted,
        DateTime now,
        int halfLifeHours)
    {
        var vs24h = ComputeWeightedScoreAt(weighted, now.AddHours(-24), halfLifeHours);
        var vs7d  = ComputeWeightedScoreAt(weighted, now.AddDays(-7), halfLifeHours);

        var currentScore = weighted.Sum(w => w.Weight) > 0
            ? weighted.Sum(w => w.Weight * w.Analysis.Score.Value) / weighted.Sum(w => w.Weight)
            : (double?)null;

        return new SentimentShift(
            Vs24h: currentScore.HasValue && vs24h.HasValue
                ? Math.Round(currentScore.Value - vs24h.Value, 4)
                : null,
            Vs7d: currentScore.HasValue && vs7d.HasValue
                ? Math.Round(currentScore.Value - vs7d.Value, 4)
                : null);
    }

    private static double? ComputeWeightedScoreAt(
        List<(SentimentAnalysis Analysis, double Weight)> allWeighted,
        DateTime asOf,
        int halfLifeHours)
    {
        // Only include articles that existed at the reference time
        var subset = allWeighted
            .Where(w => w.Analysis.AnalyzedAt <= asOf)
            .Select(w =>
            {
                var ageHours = (asOf - w.Analysis.AnalyzedAt).TotalHours;
                var weight = Math.Exp(-Math.Log(2) / halfLifeHours * ageHours);
                return (w.Analysis, Weight: weight);
            })
            .ToList();

        if (subset.Count == 0)
            return null;

        var totalWeight = subset.Sum(w => w.Weight);
        return totalWeight > 0
            ? subset.Sum(w => w.Weight * w.Analysis.Score.Value) / totalWeight
            : null;
    }

    private static string TruncateToHeadline(string text)
    {
        // Take first sentence or first 100 chars, whichever is shorter
        var firstSentenceEnd = text.IndexOfAny(['.', '!', '?']);
        if (firstSentenceEnd > 0 && firstSentenceEnd < 150)
            return text[..(firstSentenceEnd + 1)];

        return text.Length <= 100 ? text : text[..100] + "...";
    }

    /// <summary>
    /// Linear regression over (day-offset, score) pairs.
    /// Slope > 0 = improving sentiment over time; slope &lt; 0 = deteriorating.
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
