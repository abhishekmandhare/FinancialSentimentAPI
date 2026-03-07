using Application.Common.Models;
using Application.Features.Sentiment.Queries.GetSentimentHistory;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace Tests.Application;

public class GetSentimentHistoryHandlerTests
{
    private readonly ISentimentRepository _repository = Substitute.For<ISentimentRepository>();

    private GetSentimentHistoryQueryHandler CreateHandler() => new(_repository);

    private static SentimentAnalysis CreateAnalysis(double score = 0.5, string symbol = "AAPL") =>
        SentimentAnalysis.Create(
            new StockSymbol(symbol),
            "Test article text for analysis.",
            "https://example.com/article",
            score,
            0.85,
            ["Reason 1", "Reason 2"],
            "test-model-v1");

    [Fact]
    public async Task Handle_ValidSymbol_ReturnsMappedDtos()
    {
        var analyses = new List<SentimentAnalysis> { CreateAnalysis(0.6), CreateAnalysis(0.3) };
        _repository.GetHistoryAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns((analyses, 2));

        var query = new GetSentimentHistoryQuery("AAPL");
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_MapsScoreCorrectly()
    {
        var analysis = CreateAnalysis(0.75);
        _repository.GetHistoryAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns((new List<SentimentAnalysis> { analysis }, 1));

        var query = new GetSentimentHistoryQuery("AAPL");
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        var dto = result.Items.Single();
        dto.Score.Should().Be(0.75);
        dto.Label.Should().Be("Positive");
        dto.Confidence.Should().Be(0.85);
        dto.KeyReasons.Should().BeEquivalentTo(["Reason 1", "Reason 2"]);
        dto.ModelVersion.Should().Be("test-model-v1");
        dto.SourceUrl.Should().Be("https://example.com/article");
    }

    [Fact]
    public async Task Handle_EmptyHistory_ReturnsEmptyPagedResult()
    {
        _repository.GetHistoryAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns((new List<SentimentAnalysis>(), 0));

        var query = new GetSentimentHistoryQuery("AAPL");
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PassesSymbolToRepository()
    {
        _repository.GetHistoryAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns((new List<SentimentAnalysis>(), 0));

        var query = new GetSentimentHistoryQuery("msft");
        await CreateHandler().Handle(query, CancellationToken.None);

        await _repository.Received(1).GetHistoryAsync(
            Arg.Is<StockSymbol>(s => s.Value == "MSFT"),
            Arg.Is(1), Arg.Is(20),
            Arg.Is<DateTime?>(d => d == null), Arg.Is<DateTime?>(d => d == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDateFilters_PassesDatesToRepository()
    {
        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        _repository.GetHistoryAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns((new List<SentimentAnalysis>(), 0));

        var query = new GetSentimentHistoryQuery("AAPL", 2, 10, from, to);
        await CreateHandler().Handle(query, CancellationToken.None);

        await _repository.Received(1).GetHistoryAsync(
            Arg.Any<StockSymbol>(),
            Arg.Is(2), Arg.Is(10),
            Arg.Is(from), Arg.Is(to),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CustomPageSize_PassesToRepository()
    {
        _repository.GetHistoryAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns((new List<SentimentAnalysis>(), 0));

        var query = new GetSentimentHistoryQuery("AAPL", Page: 3, PageSize: 5);
        await CreateHandler().Handle(query, CancellationToken.None);

        await _repository.Received(1).GetHistoryAsync(
            Arg.Any<StockSymbol>(),
            Arg.Is(3), Arg.Is(5),
            Arg.Any<DateTime?>(), Arg.Any<DateTime?>(),
            Arg.Any<CancellationToken>());
    }
}
