using Application.Features.Sentiment.Queries.GetSentimentHistory;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace Tests.Application;

public class GetSentimentHistoryHandlerTests
{
    private readonly ISentimentRepository _repository = Substitute.For<ISentimentRepository>();

    private GetSentimentHistoryQueryHandler CreateHandler() => new(_repository);

    private static SentimentAnalysis MakeAnalysis(string symbol, double score)
    {
        return SentimentAnalysis.Create(
            new StockSymbol(symbol),
            "Test headline text.",
            null,
            score,
            0.9,
            [],
            "test-model");
    }

    [Fact]
    public async Task Handle_ExcludeNeutralTrue_PassesExcludeNeutralToRepository()
    {
        _repository.GetHistoryAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<DateTime?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((new List<SentimentAnalysis>().AsReadOnly() as IReadOnlyList<SentimentAnalysis>, 0));

        var query = new GetSentimentHistoryQuery("AAPL", ExcludeNeutral: true);
        await CreateHandler().Handle(query, CancellationToken.None);

        await _repository.Received(1).GetHistoryAsync(
            Arg.Is<StockSymbol>(s => s.Value == "AAPL"),
            1, 20, null, null,
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExcludeNeutralFalse_PassesFalseToRepository()
    {
        _repository.GetHistoryAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<DateTime?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((new List<SentimentAnalysis>().AsReadOnly() as IReadOnlyList<SentimentAnalysis>, 0));

        var query = new GetSentimentHistoryQuery("AAPL", ExcludeNeutral: false);
        await CreateHandler().Handle(query, CancellationToken.None);

        await _repository.Received(1).GetHistoryAsync(
            Arg.Is<StockSymbol>(s => s.Value == "AAPL"),
            1, 20, null, null,
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DefaultQuery_ExcludesNeutralByDefault()
    {
        _repository.GetHistoryAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<DateTime?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((new List<SentimentAnalysis>().AsReadOnly() as IReadOnlyList<SentimentAnalysis>, 0));

        var query = new GetSentimentHistoryQuery("AAPL");
        await CreateHandler().Handle(query, CancellationToken.None);

        await _repository.Received(1).GetHistoryAsync(
            Arg.Any<StockSymbol>(),
            Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<DateTime?>(), Arg.Any<DateTime?>(),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithResults_MapsDtosCorrectly()
    {
        var analysis = MakeAnalysis("AAPL", 0.8);

        _repository.GetHistoryAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<DateTime?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(([analysis], 1));

        var query = new GetSentimentHistoryQuery("AAPL");
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Items[0].Score.Should().Be(0.8);
        result.Items[0].Label.Should().Be("Positive");
    }
}
