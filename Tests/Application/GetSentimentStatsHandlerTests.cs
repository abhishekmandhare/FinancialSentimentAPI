using Application.Exceptions;
using Application.Features.Sentiment.Queries.GetSentimentStats;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace Tests.Application;

public class GetSentimentStatsHandlerTests
{
    private readonly ISentimentRepository _repository = Substitute.For<ISentimentRepository>();

    private GetSentimentStatsQueryHandler CreateHandler() => new(_repository);

    private static SentimentAnalysis CreateAnalysis(double score = 0.5, DateTime? analyzedAt = null)
    {
        var analysis = SentimentAnalysis.Create(
            new StockSymbol("AAPL"),
            "Test article text for analysis.",
            "https://example.com/article",
            score,
            0.85,
            ["Reason one"],
            "test-model-v1");

        if (analyzedAt.HasValue)
        {
            typeof(SentimentAnalysis)
                .GetProperty(nameof(SentimentAnalysis.AnalyzedAt))!
                .SetValue(analysis, analyzedAt.Value);
        }

        return analysis;
    }

    [Fact]
    public async Task Handle_NoAnalyses_ThrowsNotFoundException()
    {
        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SentimentAnalysis>() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("AAPL", 30);
        var act = () => CreateHandler().Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SingleAnalysis_ReturnsCorrectStats()
    {
        var analyses = new List<SentimentAnalysis> { CreateAnalysis(0.6) };

        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("AAPL", 7);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Symbol.Should().Be("AAPL");
        result.TotalAnalyses.Should().Be(1);
        result.WeightedScore.Should().BeApproximately(0.6, 0.01);
        result.AverageConfidence.Should().Be(0.85);
        result.Distribution.PositivePercent.Should().Be(100.0);
        result.Distribution.NeutralPercent.Should().Be(0.0);
        result.Distribution.NegativePercent.Should().Be(0.0);
        result.LatestScore.Should().Be(0.6);
        result.Trend.Direction.Should().Be("Stable");
        result.Trend.Slope.Should().Be(0);
        result.SignalStrength.Should().NotBeNullOrEmpty();
        result.MostRecentArticle.Should().NotBeNull();
        result.MostImpactfulArticle.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_MixedSentiments_CalculatesDistribution()
    {
        var analyses = new List<SentimentAnalysis>
        {
            CreateAnalysis(0.8),   // Positive
            CreateAnalysis(0.5),   // Positive
            CreateAnalysis(0.0),   // Neutral
            CreateAnalysis(-0.5),  // Negative
        };

        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("AAPL");
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.TotalAnalyses.Should().Be(4);
        result.Distribution.PositivePercent.Should().Be(50.0);
        result.Distribution.NeutralPercent.Should().Be(25.0);
        result.Distribution.NegativePercent.Should().Be(25.0);
    }

    [Fact]
    public async Task Handle_CalculatesHighestAndLowestScores()
    {
        var analyses = new List<SentimentAnalysis>
        {
            CreateAnalysis(0.9),
            CreateAnalysis(-0.3),
            CreateAnalysis(0.1),
        };

        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("AAPL");
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.HighestScore.Score.Should().Be(0.9);
        result.LowestScore.Score.Should().Be(-0.3);
    }

    [Fact]
    public async Task Handle_WeightedScore_WeightsRecentMoreHeavily()
    {
        var now = DateTime.UtcNow;
        // Recent positive article should dominate over old negative one
        var analyses = new List<SentimentAnalysis>
        {
            CreateAnalysis(0.8, now.AddHours(-1)),   // Very recent, positive
            CreateAnalysis(-0.8, now.AddDays(-10)),   // Old, negative
        };

        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("AAPL", 30, HalfLifeHours: 24);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        // With 24h half-life, the 10-day-old article has negligible weight
        // So weighted score should be close to 0.8 (the recent article)
        result.WeightedScore.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task Handle_Dispersion_HighWhenConflicting()
    {
        var now = DateTime.UtcNow;
        var analyses = new List<SentimentAnalysis>
        {
            CreateAnalysis(0.9, now.AddHours(-1)),
            CreateAnalysis(-0.9, now.AddHours(-2)),
        };

        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("AAPL", 30, HalfLifeHours: 72);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        // High dispersion when articles strongly disagree
        result.Dispersion.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task Handle_SignalStrength_StrongWithManyRecentArticles()
    {
        var now = DateTime.UtcNow;
        var analyses = Enumerable.Range(0, 5)
            .Select(i => CreateAnalysis(0.5, now.AddHours(-i)))
            .ToList();

        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("AAPL", 30, HalfLifeHours: 72);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.SignalStrength.Should().Be("strong");
    }

    [Fact]
    public async Task Handle_SentimentShift_ReturnsShiftValues()
    {
        var now = DateTime.UtcNow;
        var analyses = new List<SentimentAnalysis>
        {
            CreateAnalysis(0.8, now.AddHours(-1)),
            CreateAnalysis(-0.5, now.AddDays(-2)),
        };

        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("AAPL", 30, HalfLifeHours: 72);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.SentimentShift.Should().NotBeNull();
        // Vs7d should be non-null since we have data older than 7 days ago? No, data is only 2 days old.
        // Vs24h should show a positive shift (recent is positive, 24h-ago snapshot was dominated by negative)
    }

    [Fact]
    public async Task Handle_PassesCorrectDaysToRepository()
    {
        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SentimentAnalysis>() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("MSFT", 14);

        // Will throw NotFoundException since empty, but repository should still be called
        try { await CreateHandler().Handle(query, CancellationToken.None); } catch { }

        await _repository.Received(1).GetForStatsAsync(
            Arg.Is<StockSymbol>(s => s.Value == "MSFT"),
            14,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DaysCappedAt30()
    {
        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SentimentAnalysis>() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("MSFT", 90);

        try { await CreateHandler().Handle(query, CancellationToken.None); } catch { }

        await _repository.Received(1).GetForStatsAsync(
            Arg.Any<StockSymbol>(),
            30,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Period_ContainsCorrectDays()
    {
        var analyses = new List<SentimentAnalysis> { CreateAnalysis(0.5) };

        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("AAPL", 7);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Period.Days.Should().Be(7);
    }

    [Fact]
    public async Task Handle_HalfLifeHours_IncludedInResponse()
    {
        var analyses = new List<SentimentAnalysis> { CreateAnalysis(0.5) };

        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("AAPL", 7, HalfLifeHours: 48);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.HalfLifeHours.Should().Be(48);
    }

    [Fact]
    public async Task Handle_MostImpactfulArticle_HasHighestWeightTimesScore()
    {
        var now = DateTime.UtcNow;
        var analyses = new List<SentimentAnalysis>
        {
            CreateAnalysis(0.1, now.AddHours(-1)),    // Low score, high weight
            CreateAnalysis(0.9, now.AddHours(-2)),    // High score, high weight → most impactful
            CreateAnalysis(0.5, now.AddDays(-5)),     // Medium score, low weight
        };

        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("AAPL", 30, HalfLifeHours: 72);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.MostImpactfulArticle.Should().NotBeNull();
        result.MostImpactfulArticle!.Score.Should().Be(0.9);
    }
}
