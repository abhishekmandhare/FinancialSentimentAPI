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

    private static SentimentAnalysis CreateAnalysis(double score = 0.5) =>
        SentimentAnalysis.Create(
            new StockSymbol("AAPL"),
            "Test article text for analysis.",
            "https://example.com/article",
            score,
            0.85,
            ["Reason one"],
            "test-model-v1");

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
        result.AverageScore.Should().Be(0.6);
        result.AverageConfidence.Should().Be(0.85);
        result.Distribution.PositivePercent.Should().Be(100.0);
        result.Distribution.NeutralPercent.Should().Be(0.0);
        result.Distribution.NegativePercent.Should().Be(0.0);
        result.LatestScore.Should().Be(0.6);
        result.Trend.Direction.Should().Be("Stable");
        result.Trend.Slope.Should().Be(0);
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
    public async Task Handle_CalculatesAverageScore()
    {
        var analyses = new List<SentimentAnalysis>
        {
            CreateAnalysis(0.8),
            CreateAnalysis(0.2),
        };

        _repository.GetForStatsAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        var query = new GetSentimentStatsQuery("AAPL");
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.AverageScore.Should().Be(0.5);
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
}
