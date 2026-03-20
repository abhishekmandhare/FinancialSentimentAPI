using Application.Configuration;
using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using FluentAssertions;
using Infrastructure.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Tests.Infrastructure;

public class SnapshotBackfillWorkerTests
{
    private readonly ISystemStatsRepository _statsRepo = Substitute.For<ISystemStatsRepository>();
    private readonly ISymbolSnapshotRepository _snapshotRepo = Substitute.For<ISymbolSnapshotRepository>();
    private readonly ISentimentRepository _sentimentRepo = Substitute.For<ISentimentRepository>();
    private readonly SentimentScoringOptions _scoringOptions = new();

    private SnapshotBackfillWorker CreateWorker()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_statsRepo);
        services.AddSingleton(_snapshotRepo);
        services.AddSingleton(_sentimentRepo);
        services.AddSingleton(_scoringOptions);
        var sp = services.BuildServiceProvider();

        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new SnapshotBackfillWorker(scopeFactory, NullLogger<SnapshotBackfillWorker>.Instance);
    }

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
    public async Task ExecuteAsync_BackfillsMissingSnapshots()
    {
        // Arrange: AAPL has analyses but no snapshot, TSLA has both
        _statsRepo.GetDistinctAnalyzedSymbolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string> { "AAPL", "TSLA" } as IReadOnlyList<string>);

        var tslaSnapshot = SymbolSnapshot.Create("TSLA", 0.5, 0.3, 0.2, "up", "Improving", 0.1, 3);
        _snapshotRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<SymbolSnapshot> { tslaSnapshot } as IReadOnlyList<SymbolSnapshot>);

        var now = DateTime.UtcNow;
        var aaplAnalyses = new List<SentimentAnalysis>
        {
            MakeAnalysis("AAPL", 0.8, now.AddHours(-2)),
            MakeAnalysis("AAPL", 0.6, now.AddHours(-5)),
        };
        _sentimentRepo.GetForStatsAsync(
                Arg.Is<StockSymbol>(s => s.Value == "AAPL"), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(aaplAnalyses as IReadOnlyList<SentimentAnalysis>);

        // Act
        var worker = CreateWorker();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // Assert: AAPL snapshot created, TSLA not touched
        await _snapshotRepo.Received(1).UpsertAsync(
            Arg.Is<SymbolSnapshot>(s => s.Symbol == "AAPL" && s.ArticleCount == 2),
            Arg.Any<CancellationToken>());

        await _sentimentRepo.DidNotReceive().GetForStatsAsync(
            Arg.Is<StockSymbol>(s => s.Value == "TSLA"), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SkipsWhenAllSymbolsHaveSnapshots()
    {
        _statsRepo.GetDistinctAnalyzedSymbolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string> { "GOOG" } as IReadOnlyList<string>);

        var googSnapshot = SymbolSnapshot.Create("GOOG", 0.4, 0.3, 0.1, "up", "Stable", 0.05, 5);
        _snapshotRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<SymbolSnapshot> { googSnapshot } as IReadOnlyList<SymbolSnapshot>);

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // Should not fetch analyses or create any snapshots
        await _sentimentRepo.DidNotReceive().GetForStatsAsync(
            Arg.Any<StockSymbol>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _snapshotRepo.DidNotReceive().UpsertAsync(
            Arg.Any<SymbolSnapshot>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_HandlesEmptyAnalysesGracefully()
    {
        _statsRepo.GetDistinctAnalyzedSymbolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string> { "MSFT" } as IReadOnlyList<string>);

        _snapshotRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<SymbolSnapshot>() as IReadOnlyList<SymbolSnapshot>);

        _sentimentRepo.GetForStatsAsync(
                Arg.Is<StockSymbol>(s => s.Value == "MSFT"), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SentimentAnalysis>() as IReadOnlyList<SentimentAnalysis>);

        var worker = CreateWorker();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StartAsync(cts.Token);
        await worker.ExecuteTask!;

        // Should still create a snapshot with zero values
        await _snapshotRepo.Received(1).UpsertAsync(
            Arg.Is<SymbolSnapshot>(s => s.Symbol == "MSFT" && s.Score == 0 && s.ArticleCount == 0),
            Arg.Any<CancellationToken>());
    }
}
