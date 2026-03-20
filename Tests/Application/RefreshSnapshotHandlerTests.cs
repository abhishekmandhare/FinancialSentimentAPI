using Application.Configuration;
using Application.Features.Sentiment.Commands.AnalyzeSentiment;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Tests.Application;

public class RefreshSnapshotHandlerTests
{
    private readonly ISentimentRepository _sentimentRepo = Substitute.For<ISentimentRepository>();
    private readonly ISymbolSnapshotRepository _snapshotRepo = Substitute.For<ISymbolSnapshotRepository>();

    private RefreshSnapshotHandler CreateHandler() =>
        new(_sentimentRepo, _snapshotRepo, new SentimentScoringOptions(), NullLogger<RefreshSnapshotHandler>.Instance);

    private static SentimentAnalysis MakeAnalysis(string symbol, double score, DateTime analyzedAt)
    {
        var analysis = SentimentAnalysis.Create(
            new StockSymbol(symbol), "Test text.", null, score, 0.9, [], "test-model");

        typeof(SentimentAnalysis)
            .GetProperty(nameof(SentimentAnalysis.AnalyzedAt))!
            .SetValue(analysis, analyzedAt);

        return analysis;
    }

    [Fact]
    public async Task Handle_NewSymbol_CreatesSnapshot()
    {
        var now = DateTime.UtcNow;
        var analyses = new List<SentimentAnalysis>
        {
            MakeAnalysis("AAPL", 0.7, now.AddHours(-1))
        };

        _sentimentRepo.GetForStatsAsync(
                Arg.Is<StockSymbol>(s => s.Value == "AAPL"), 7, Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        _snapshotRepo.GetBySymbolAsync("AAPL", Arg.Any<CancellationToken>())
            .Returns((SymbolSnapshot?)null);

        var notification = new SentimentAnalysisCreatedNotification(Guid.NewGuid(), "AAPL");
        await CreateHandler().Handle(notification, CancellationToken.None);

        await _snapshotRepo.Received(1).UpsertAsync(
            Arg.Is<SymbolSnapshot>(s => s.Symbol == "AAPL" && s.ArticleCount == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingSymbol_UpdatesSnapshot()
    {
        var now = DateTime.UtcNow;
        var analyses = new List<SentimentAnalysis>
        {
            MakeAnalysis("TSLA", 0.5, now.AddHours(-1)),
            MakeAnalysis("TSLA", 0.3, now.AddHours(-2)),
        };

        _sentimentRepo.GetForStatsAsync(
                Arg.Is<StockSymbol>(s => s.Value == "TSLA"), 7, Arg.Any<CancellationToken>())
            .Returns(analyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        var existing = SymbolSnapshot.Create("TSLA", 0.3, 0.0, 0.3, "up", "Stable", 0.0, 1);
        _snapshotRepo.GetBySymbolAsync("TSLA", Arg.Any<CancellationToken>())
            .Returns(existing);

        var notification = new SentimentAnalysisCreatedNotification(Guid.NewGuid(), "TSLA");
        await CreateHandler().Handle(notification, CancellationToken.None);

        existing.ArticleCount.Should().Be(2);
        await _snapshotRepo.Received(1).UpsertAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoAnalyses_CreatesSnapshotWithZeros()
    {
        _sentimentRepo.GetForStatsAsync(
                Arg.Any<StockSymbol>(), 7, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SentimentAnalysis>() as IReadOnlyList<SentimentAnalysis>);

        _snapshotRepo.GetBySymbolAsync("GOOG", Arg.Any<CancellationToken>())
            .Returns((SymbolSnapshot?)null);

        var notification = new SentimentAnalysisCreatedNotification(Guid.NewGuid(), "GOOG");
        await CreateHandler().Handle(notification, CancellationToken.None);

        await _snapshotRepo.Received(1).UpsertAsync(
            Arg.Is<SymbolSnapshot>(s => s.Symbol == "GOOG" && s.Score == 0 && s.ArticleCount == 0),
            Arg.Any<CancellationToken>());
    }
}
