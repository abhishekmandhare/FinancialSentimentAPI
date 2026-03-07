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

    private static SentimentAnalysis CreateAnalysis(double score, string symbol = "AAPL") =>
        SentimentAnalysis.Create(
            new StockSymbol(symbol),
            "Test article text for analysis.",
            "https://example.com/article",
            score,
            0.85,
            ["Reason"],
            "test-model-v1");

    [Fact]
    public async Task Handle_NoAnalyses_ThrowsNotFoundException()
    {
        _repository.GetForStatsAsync(Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<SentimentAnalysis>());

        var query = new GetSentimentStatsQuery("AAPL", 30);
        var act = async () => await CreateHandler().Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SingleAnalysis_ReturnsCorrectStats()
    {
        var analyses = new List<SentimentAnalysis> { CreateAnalysis(0.6) };
        _repository.GetForStatsAsync(Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var query = new GetSentimentStatsQuery("AAPL", 30);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Symbol.Should().Be("AAPL");
        result.TotalAnalyses.Should().Be(1);
        result.AverageScore.Should().Be(0.6);
        result.AverageConfidence.Should().Be(0.85);
        result.LatestScore.Should().Be(0.6);
    }

    [Fact]
    public async Task Handle_MultipleAnalyses_CalculatesAverageScore()
    {
        var analyses = new List<SentimentAnalysis>
        {
            CreateAnalysis(0.8),
            CreateAnalysis(0.4),
            CreateAnalysis(0.6)
        };
        _repository.GetForStatsAsync(Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var query = new GetSentimentStatsQuery("AAPL", 30);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.AverageScore.Should().Be(0.6);
        result.TotalAnalyses.Should().Be(3);
    }

    [Fact]
    public async Task Handle_AllPositive_ReturnsFullPositiveDistribution()
    {
        var analyses = new List<SentimentAnalysis>
        {
            CreateAnalysis(0.5),
            CreateAnalysis(0.8)
        };
        _repository.GetForStatsAsync(Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var query = new GetSentimentStatsQuery("AAPL", 30);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Distribution.PositivePercent.Should().Be(100.0);
        result.Distribution.NeutralPercent.Should().Be(0.0);
        result.Distribution.NegativePercent.Should().Be(0.0);
    }

    [Fact]
    public async Task Handle_MixedSentiment_CalculatesDistributionCorrectly()
    {
        var analyses = new List<SentimentAnalysis>
        {
            CreateAnalysis(0.5),   // Positive
            CreateAnalysis(0.0),   // Neutral
            CreateAnalysis(-0.5),  // Negative
        };
        _repository.GetForStatsAsync(Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var query = new GetSentimentStatsQuery("AAPL", 30);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Distribution.PositivePercent.Should().BeApproximately(33.3, 0.1);
        result.Distribution.NeutralPercent.Should().BeApproximately(33.3, 0.1);
        result.Distribution.NegativePercent.Should().BeApproximately(33.3, 0.1);
    }

    [Fact]
    public async Task Handle_IdentifiesHighestAndLowestScores()
    {
        var analyses = new List<SentimentAnalysis>
        {
            CreateAnalysis(0.2),
            CreateAnalysis(0.9),
            CreateAnalysis(-0.3)
        };
        _repository.GetForStatsAsync(Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var query = new GetSentimentStatsQuery("AAPL", 30);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.HighestScore.Score.Should().Be(0.9);
        result.LowestScore.Score.Should().Be(-0.3);
    }

    [Fact]
    public async Task Handle_SingleAnalysis_TrendIsStable()
    {
        var analyses = new List<SentimentAnalysis> { CreateAnalysis(0.5) };
        _repository.GetForStatsAsync(Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var query = new GetSentimentStatsQuery("AAPL", 30);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Trend.Direction.Should().Be("Stable");
        result.Trend.Slope.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UppercasesSymbolInput()
    {
        _repository.GetForStatsAsync(Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<SentimentAnalysis> { CreateAnalysis(0.5) });

        var query = new GetSentimentStatsQuery("aapl", 7);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task Handle_PeriodReflectsRequestedDays()
    {
        _repository.GetForStatsAsync(Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<SentimentAnalysis> { CreateAnalysis(0.5) });

        var query = new GetSentimentStatsQuery("AAPL", 7);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Period.Days.Should().Be(7);
    }
}
