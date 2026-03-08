using Application.Common.Models;
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

    private static SentimentAnalysis CreateAnalysis(string symbol = "AAPL", double score = 0.5) =>
        SentimentAnalysis.Create(
            new StockSymbol(symbol),
            "Apple reported strong earnings.",
            "https://example.com/article",
            score,
            0.85,
            ["Strong revenue"],
            "test-model-v1");

    private void SetupRepository(IReadOnlyList<SentimentAnalysis>? items = null, int totalCount = 0)
    {
        _repository.GetHistoryAsync(
                Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<DateTime?>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((items ?? Array.Empty<SentimentAnalysis>(), totalCount));
    }

    [Fact]
    public async Task Handle_ExcludeNeutralTrue_PassesExcludeNeutralToRepository()
    {
        SetupRepository();

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
        SetupRepository();

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
        SetupRepository();

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
    public async Task Handle_ReturnsPagedResult_WithCorrectMapping()
    {
        var analysis = CreateAnalysis(score: 0.75);
        SetupRepository([analysis], 1);

        var query = new GetSentimentHistoryQuery("AAPL");
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);

        var dto = result.Items[0];
        dto.Score.Should().Be(0.75);
        dto.Label.Should().Be("Positive");
        dto.Confidence.Should().Be(0.85);
        dto.KeyReasons.Should().Contain("Strong revenue");
        dto.ModelVersion.Should().Be("test-model-v1");
        dto.SourceUrl.Should().Be("https://example.com/article");
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsEmptyPage()
    {
        SetupRepository();

        var query = new GetSentimentHistoryQuery("MSFT");
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PassesSymbolToRepository()
    {
        SetupRepository();

        var query = new GetSentimentHistoryQuery("aapl");
        await CreateHandler().Handle(query, CancellationToken.None);

        await _repository.Received(1).GetHistoryAsync(
            Arg.Is<StockSymbol>(s => s.Value == "AAPL"),
            Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<DateTime?>(), Arg.Any<DateTime?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesPaginationParameters()
    {
        SetupRepository();

        var query = new GetSentimentHistoryQuery("AAPL", Page: 3, PageSize: 10);
        await CreateHandler().Handle(query, CancellationToken.None);

        await _repository.Received(1).GetHistoryAsync(
            Arg.Any<StockSymbol>(),
            3, 10,
            Arg.Any<DateTime?>(), Arg.Any<DateTime?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesDateFilters()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        SetupRepository();

        var query = new GetSentimentHistoryQuery("AAPL", From: from, To: to);
        await CreateHandler().Handle(query, CancellationToken.None);

        await _repository.Received(1).GetHistoryAsync(
            Arg.Any<StockSymbol>(),
            Arg.Any<int>(), Arg.Any<int>(),
            from, to,
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MultipleItems_MapsAllCorrectly()
    {
        var analyses = new List<SentimentAnalysis>
        {
            CreateAnalysis(score: 0.8),
            CreateAnalysis(score: -0.5),
            CreateAnalysis(score: 0.0)
        };

        SetupRepository(analyses.AsReadOnly(), 3);

        var query = new GetSentimentHistoryQuery("AAPL");
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.Items.Should().HaveCount(3);
        result.Items[0].Label.Should().Be("Positive");
        result.Items[1].Label.Should().Be("Negative");
        result.Items[2].Label.Should().Be("Neutral");
    }
}
