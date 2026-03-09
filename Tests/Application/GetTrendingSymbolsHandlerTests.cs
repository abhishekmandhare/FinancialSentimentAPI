using Application.Features.Sentiment.Queries.GetTrendingSymbols;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace Tests.Application;

public class GetTrendingSymbolsHandlerTests
{
    private readonly ISentimentRepository _repository = Substitute.For<ISentimentRepository>();

    private GetTrendingSymbolsQueryHandler CreateHandler() => new(_repository);

    /// <summary>
    /// Creates a SentimentAnalysis and back-dates its AnalyzedAt via reflection
    /// so handler window-split logic can be tested without a clock abstraction.
    /// </summary>
    private static SentimentAnalysis MakeAnalysis(string symbol, double score, DateTime analyzedAt)
    {
        var analysis = SentimentAnalysis.Create(
            new StockSymbol(symbol),
            "Test headline text.",
            null,
            score,
            0.9,
            [],
            "test-model");

        typeof(SentimentAnalysis)
            .GetProperty(nameof(SentimentAnalysis.AnalyzedAt))!
            .SetValue(analysis, analyzedAt);

        return analysis;
    }

    [Fact]
    public async Task Handle_NoData_ReturnsEmptyList()
    {
        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await CreateHandler().Handle(new GetTrendingSymbolsQuery(24, 10), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SingleDataPoint_ReturnsSymbolWithPreviousAvgZero()
    {
        var now = DateTime.UtcNow;
        // Place the single point in the current half (within last 12h)
        var analysis = MakeAnalysis("AAPL", 0.8, now.AddHours(-6));

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([analysis]);

        var result = await CreateHandler().Handle(new GetTrendingSymbolsQuery(24, 10), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("AAPL");
        result[0].CurrentAvgScore.Should().Be(0.8);
        result[0].PreviousAvgScore.Should().Be(0.0);
        result[0].Delta.Should().Be(0.8);
        result[0].Direction.Should().Be("up");
    }

    [Fact]
    public async Task Handle_PositiveShift_ReturnsDirectionUp()
    {
        var now = DateTime.UtcNow;
        var previous = MakeAnalysis("TSLA", -0.4, now.AddHours(-20));
        var current  = MakeAnalysis("TSLA",  0.6, now.AddHours(-2));

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([previous, current]);

        var result = await CreateHandler().Handle(new GetTrendingSymbolsQuery(24, 10), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Direction.Should().Be("up");
        result[0].Delta.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public async Task Handle_NegativeShift_ReturnsDirectionDown()
    {
        var now = DateTime.UtcNow;
        var previous = MakeAnalysis("MSFT", 0.6, now.AddHours(-20));
        var current  = MakeAnalysis("MSFT", -0.4, now.AddHours(-2));

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([previous, current]);

        var result = await CreateHandler().Handle(new GetTrendingSymbolsQuery(24, 10), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Direction.Should().Be("down");
        result[0].Delta.Should().BeApproximately(-1.0, 0.001);
    }

    [Fact]
    public async Task Handle_AllSymbolsEqualShift_ReturnsFlatDirection()
    {
        var now = DateTime.UtcNow;
        var a1 = MakeAnalysis("AMZN", 0.5, now.AddHours(-20));
        var a2 = MakeAnalysis("AMZN", 0.5, now.AddHours(-2));

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([a1, a2]);

        var result = await CreateHandler().Handle(new GetTrendingSymbolsQuery(24, 10), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Direction.Should().Be("flat");
        result[0].Delta.Should().Be(0.0);
    }

    [Fact]
    public async Task Handle_MultipleSymbols_OrderedByAbsoluteDeltaDescending()
    {
        var now = DateTime.UtcNow;

        // AAPL: small positive shift (delta = +0.1)
        var aaplPrev    = MakeAnalysis("AAPL", 0.4, now.AddHours(-20));
        var aaplCurrent = MakeAnalysis("AAPL", 0.5, now.AddHours(-2));

        // TSLA: large positive shift (delta = +0.9)
        var tslaPrev    = MakeAnalysis("TSLA", -0.4, now.AddHours(-20));
        var tslaCurrent = MakeAnalysis("TSLA",  0.5, now.AddHours(-2));

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([aaplPrev, aaplCurrent, tslaPrev, tslaCurrent]);

        var result = await CreateHandler().Handle(new GetTrendingSymbolsQuery(24, 10), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Symbol.Should().Be("TSLA");
        result[1].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task Handle_MixedPositiveAndNegativeDeltas_OrderedByAbsoluteDeltaDescending()
    {
        var now = DateTime.UtcNow;

        // GOOG: large negative shift (delta = -0.9), |delta| = 0.9
        var googPrev    = MakeAnalysis("GOOG", 0.5, now.AddHours(-20));
        var googCurrent = MakeAnalysis("GOOG", -0.4, now.AddHours(-2));

        // MSFT: medium positive shift (delta = +0.5), |delta| = 0.5
        var msftPrev    = MakeAnalysis("MSFT", 0.0, now.AddHours(-20));
        var msftCurrent = MakeAnalysis("MSFT", 0.5, now.AddHours(-2));

        // AAPL: small negative shift (delta = -0.2), |delta| = 0.2
        var aaplPrev    = MakeAnalysis("AAPL", 0.3, now.AddHours(-20));
        var aaplCurrent = MakeAnalysis("AAPL", 0.1, now.AddHours(-2));

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([googPrev, googCurrent, msftPrev, msftCurrent, aaplPrev, aaplCurrent]);

        var result = await CreateHandler().Handle(new GetTrendingSymbolsQuery(24, 10), CancellationToken.None);

        result.Should().HaveCount(3);
        // GOOG first: |delta| = 0.9 (negative delta, but largest absolute move)
        result[0].Symbol.Should().Be("GOOG");
        result[0].Direction.Should().Be("down");
        result[0].Delta.Should().BeApproximately(-0.9, 0.001);
        // MSFT second: |delta| = 0.5
        result[1].Symbol.Should().Be("MSFT");
        result[1].Direction.Should().Be("up");
        result[1].Delta.Should().BeApproximately(0.5, 0.001);
        // AAPL third: |delta| = 0.2
        result[2].Symbol.Should().Be("AAPL");
        result[2].Direction.Should().Be("down");
        result[2].Delta.Should().BeApproximately(-0.2, 0.001);
    }

    [Fact]
    public async Task Handle_LimitEnforced_ReturnsOnlyTopN()
    {
        var now = DateTime.UtcNow;
        var analyses = Enumerable.Range(1, 15)
            .SelectMany(i => new[]
            {
                MakeAnalysis($"SYM{i:D2}", -0.5, now.AddHours(-20)),
                MakeAnalysis($"SYM{i:D2}",  0.5, now.AddHours(-2))
            })
            .ToList();

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var result = await CreateHandler().Handle(new GetTrendingSymbolsQuery(24, 5), CancellationToken.None);

        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task Handle_QueriesCorrectWindowFromRepository()
    {
        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var before = DateTime.UtcNow.AddHours(-24);
        await CreateHandler().Handle(new GetTrendingSymbolsQuery(24, 10), CancellationToken.None);
        var after = DateTime.UtcNow.AddHours(-24);

        await _repository.Received(1).GetRecentAsync(
            Arg.Is<DateTime>(d => d >= before && d <= after),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SortBySymbolAsc_ReturnsSortedAlphabetically()
    {
        var now = DateTime.UtcNow;
        var analyses = new[]
        {
            MakeAnalysis("TSLA", 0.5, now.AddHours(-2)),
            MakeAnalysis("AAPL", 0.3, now.AddHours(-2)),
            MakeAnalysis("MSFT", 0.7, now.AddHours(-2)),
        };

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var result = await CreateHandler().Handle(
            new GetTrendingSymbolsQuery(24, 10, SortBy: "symbol", SortDirection: "asc"),
            CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].Symbol.Should().Be("AAPL");
        result[1].Symbol.Should().Be("MSFT");
        result[2].Symbol.Should().Be("TSLA");
    }

    [Fact]
    public async Task Handle_SortBySymbolDesc_ReturnsSortedReverseAlphabetically()
    {
        var now = DateTime.UtcNow;
        var analyses = new[]
        {
            MakeAnalysis("AAPL", 0.3, now.AddHours(-2)),
            MakeAnalysis("TSLA", 0.5, now.AddHours(-2)),
            MakeAnalysis("MSFT", 0.7, now.AddHours(-2)),
        };

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var result = await CreateHandler().Handle(
            new GetTrendingSymbolsQuery(24, 10, SortBy: "symbol", SortDirection: "desc"),
            CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].Symbol.Should().Be("TSLA");
        result[1].Symbol.Should().Be("MSFT");
        result[2].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task Handle_SortByCurrentAvgScoreDesc_ReturnsHighestScoreFirst()
    {
        var now = DateTime.UtcNow;
        var analyses = new[]
        {
            MakeAnalysis("AAPL", 0.3, now.AddHours(-2)),
            MakeAnalysis("TSLA", 0.9, now.AddHours(-2)),
            MakeAnalysis("MSFT", 0.6, now.AddHours(-2)),
        };

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var result = await CreateHandler().Handle(
            new GetTrendingSymbolsQuery(24, 10, SortBy: "currentAvgScore", SortDirection: "desc"),
            CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].Symbol.Should().Be("TSLA");
        result[1].Symbol.Should().Be("MSFT");
        result[2].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task Handle_SortByCurrentAvgScoreAsc_ReturnsLowestScoreFirst()
    {
        var now = DateTime.UtcNow;
        var analyses = new[]
        {
            MakeAnalysis("TSLA", 0.9, now.AddHours(-2)),
            MakeAnalysis("AAPL", 0.3, now.AddHours(-2)),
            MakeAnalysis("MSFT", 0.6, now.AddHours(-2)),
        };

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var result = await CreateHandler().Handle(
            new GetTrendingSymbolsQuery(24, 10, SortBy: "currentAvgScore", SortDirection: "asc"),
            CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].Symbol.Should().Be("AAPL");
        result[1].Symbol.Should().Be("MSFT");
        result[2].Symbol.Should().Be("TSLA");
    }

    [Fact]
    public async Task Handle_SortByPreviousAvgScoreDesc_ReturnsHighestPrevFirst()
    {
        var now = DateTime.UtcNow;
        var analyses = new[]
        {
            MakeAnalysis("AAPL", 0.2, now.AddHours(-20)),
            MakeAnalysis("AAPL", 0.5, now.AddHours(-2)),
            MakeAnalysis("TSLA", 0.8, now.AddHours(-20)),
            MakeAnalysis("TSLA", 0.5, now.AddHours(-2)),
            MakeAnalysis("MSFT", 0.5, now.AddHours(-20)),
            MakeAnalysis("MSFT", 0.5, now.AddHours(-2)),
        };

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var result = await CreateHandler().Handle(
            new GetTrendingSymbolsQuery(24, 10, SortBy: "previousAvgScore", SortDirection: "desc"),
            CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].Symbol.Should().Be("TSLA");
        result[1].Symbol.Should().Be("MSFT");
        result[2].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task Handle_SortByDeltaAsc_ReturnsSmallestAbsDeltaFirst()
    {
        var now = DateTime.UtcNow;

        // AAPL: delta = +0.1, |delta| = 0.1
        var aaplPrev    = MakeAnalysis("AAPL", 0.4, now.AddHours(-20));
        var aaplCurrent = MakeAnalysis("AAPL", 0.5, now.AddHours(-2));

        // TSLA: delta = +0.9, |delta| = 0.9
        var tslaPrev    = MakeAnalysis("TSLA", -0.4, now.AddHours(-20));
        var tslaCurrent = MakeAnalysis("TSLA",  0.5, now.AddHours(-2));

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([aaplPrev, aaplCurrent, tslaPrev, tslaCurrent]);

        var result = await CreateHandler().Handle(
            new GetTrendingSymbolsQuery(24, 10, SortBy: "delta", SortDirection: "asc"),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Symbol.Should().Be("AAPL");
        result[1].Symbol.Should().Be("TSLA");
    }

    [Fact]
    public async Task Handle_DefaultSortWithNullParams_SameAsAbsDeltaDesc()
    {
        var now = DateTime.UtcNow;

        var aaplPrev    = MakeAnalysis("AAPL", 0.4, now.AddHours(-20));
        var aaplCurrent = MakeAnalysis("AAPL", 0.5, now.AddHours(-2));

        var tslaPrev    = MakeAnalysis("TSLA", -0.4, now.AddHours(-20));
        var tslaCurrent = MakeAnalysis("TSLA",  0.5, now.AddHours(-2));

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([aaplPrev, aaplCurrent, tslaPrev, tslaCurrent]);

        var result = await CreateHandler().Handle(
            new GetTrendingSymbolsQuery(24, 10, SortBy: null, SortDirection: null),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Symbol.Should().Be("TSLA");  // |0.9| > |0.1|
        result[1].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task Handle_SortByCaseInsensitive_WorksCorrectly()
    {
        var now = DateTime.UtcNow;
        var analyses = new[]
        {
            MakeAnalysis("TSLA", 0.9, now.AddHours(-2)),
            MakeAnalysis("AAPL", 0.3, now.AddHours(-2)),
        };

        _repository.GetRecentAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(analyses);

        var result = await CreateHandler().Handle(
            new GetTrendingSymbolsQuery(24, 10, SortBy: "SYMBOL", SortDirection: "ASC"),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Symbol.Should().Be("AAPL");
        result[1].Symbol.Should().Be("TSLA");
    }
}
