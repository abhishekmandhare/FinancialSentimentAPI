using Application.Features.Admin.Queries.GetSystemStats;
using Application.Services;
using FluentAssertions;
using NSubstitute;

namespace Tests.Application;

public class GetSystemStatsHandlerTests
{
    private readonly ISystemStatsRepository _statsRepository = Substitute.For<ISystemStatsRepository>();

    private GetSystemStatsQueryHandler CreateHandler() => new(_statsRepository);

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsZeroCounts()
    {
        _statsRepository.GetTotalAnalysesCountAsync(Arg.Any<CancellationToken>()).Returns(0);
        _statsRepository.GetAnalysesCountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(0);
        _statsRepository.GetTrackedSymbolCountAsync(Arg.Any<CancellationToken>()).Returns(0);
        _statsRepository.GetDistinctAnalyzedSymbolsAsync(Arg.Any<CancellationToken>()).Returns(new List<string>());

        var result = await CreateHandler().Handle(new GetSystemStatsQuery(), CancellationToken.None);

        result.Counts.Total.Should().Be(0);
        result.Counts.LastHour.Should().Be(0);
        result.Counts.Last24Hours.Should().Be(0);
        result.Symbols.TrackedSymbols.Should().Be(0);
        result.Symbols.DistinctAnalyzedSymbols.Should().Be(0);
        result.Throughput.AnalysesPerHour.Should().Be(0);
        result.Throughput.AnalysesPerDay.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithData_ReturnsCorrectCounts()
    {
        _statsRepository.GetTotalAnalysesCountAsync(Arg.Any<CancellationToken>()).Returns(1000);
        _statsRepository.GetAnalysesCountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var since = callInfo.ArgAt<DateTime>(0);
                var hoursAgo = (DateTime.UtcNow - since).TotalHours;
                return hoursAgo < 2 ? 10 : 240;
            });
        _statsRepository.GetTrackedSymbolCountAsync(Arg.Any<CancellationToken>()).Returns(15);
        _statsRepository.GetDistinctAnalyzedSymbolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string> { "AAPL", "GOOG", "MSFT" });

        var result = await CreateHandler().Handle(new GetSystemStatsQuery(), CancellationToken.None);

        result.Counts.Total.Should().Be(1000);
        result.Counts.LastHour.Should().Be(10);
        result.Counts.Last24Hours.Should().Be(240);
        result.Symbols.TrackedSymbols.Should().Be(15);
        result.Symbols.DistinctAnalyzedSymbols.Should().Be(3);
        result.Symbols.AnalyzedSymbols.Should().ContainInOrder("AAPL", "GOOG", "MSFT");
    }

    [Fact]
    public async Task Handle_WithData_CalculatesCorrectThroughput()
    {
        _statsRepository.GetTotalAnalysesCountAsync(Arg.Any<CancellationToken>()).Returns(500);
        _statsRepository.GetAnalysesCountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var since = callInfo.ArgAt<DateTime>(0);
                var hoursAgo = (DateTime.UtcNow - since).TotalHours;
                return hoursAgo < 2 ? 5 : 120;
            });
        _statsRepository.GetTrackedSymbolCountAsync(Arg.Any<CancellationToken>()).Returns(10);
        _statsRepository.GetDistinctAnalyzedSymbolsAsync(Arg.Any<CancellationToken>()).Returns(new List<string>());

        var result = await CreateHandler().Handle(new GetSystemStatsQuery(), CancellationToken.None);

        // 120 analyses in 24h = 5 per hour
        result.Throughput.AnalysesPerHour.Should().Be(5.0);
        result.Throughput.AnalysesPerDay.Should().Be(120);
    }

    [Fact]
    public async Task Handle_WithData_CalculatesDbGrowthProjection()
    {
        _statsRepository.GetTotalAnalysesCountAsync(Arg.Any<CancellationToken>()).Returns(100);
        _statsRepository.GetAnalysesCountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var since = callInfo.ArgAt<DateTime>(0);
                var hoursAgo = (DateTime.UtcNow - since).TotalHours;
                return hoursAgo < 2 ? 1 : 48;
            });
        _statsRepository.GetTrackedSymbolCountAsync(Arg.Any<CancellationToken>()).Returns(5);
        _statsRepository.GetDistinctAnalyzedSymbolsAsync(Arg.Any<CancellationToken>()).Returns(new List<string>());

        var result = await CreateHandler().Handle(new GetSystemStatsQuery(), CancellationToken.None);

        // 48 analyses/day * 30 days = 1440 rows/month
        result.Projection.EstimatedRowsPerMonth.Should().Be(1440);
        result.Projection.EstimatedDbGrowthPerMonth.Should().Contain("MB");
    }

    [Fact]
    public async Task Handle_NoAnalyses_ReturnsUnavailableLatencyNote()
    {
        _statsRepository.GetTotalAnalysesCountAsync(Arg.Any<CancellationToken>()).Returns(0);
        _statsRepository.GetAnalysesCountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(0);
        _statsRepository.GetTrackedSymbolCountAsync(Arg.Any<CancellationToken>()).Returns(0);
        _statsRepository.GetDistinctAnalyzedSymbolsAsync(Arg.Any<CancellationToken>()).Returns(new List<string>());

        var result = await CreateHandler().Handle(new GetSystemStatsQuery(), CancellationToken.None);

        result.Projection.AnalysisLatencyNote.Should().Contain("unavailable");
    }

    [Fact]
    public async Task Handle_WithAnalyses_ReturnsOllamaLatencyNote()
    {
        _statsRepository.GetTotalAnalysesCountAsync(Arg.Any<CancellationToken>()).Returns(50);
        _statsRepository.GetAnalysesCountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(10);
        _statsRepository.GetTrackedSymbolCountAsync(Arg.Any<CancellationToken>()).Returns(5);
        _statsRepository.GetDistinctAnalyzedSymbolsAsync(Arg.Any<CancellationToken>()).Returns(new List<string> { "AAPL" });

        var result = await CreateHandler().Handle(new GetSystemStatsQuery(), CancellationToken.None);

        result.Projection.AnalysisLatencyNote.Should().Contain("Ollama");
    }

    [Fact]
    public async Task Handle_CallsRepositoryMethodsExactlyOnce()
    {
        _statsRepository.GetTotalAnalysesCountAsync(Arg.Any<CancellationToken>()).Returns(0);
        _statsRepository.GetAnalysesCountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(0);
        _statsRepository.GetTrackedSymbolCountAsync(Arg.Any<CancellationToken>()).Returns(0);
        _statsRepository.GetDistinctAnalyzedSymbolsAsync(Arg.Any<CancellationToken>()).Returns(new List<string>());

        await CreateHandler().Handle(new GetSystemStatsQuery(), CancellationToken.None);

        await _statsRepository.Received(1).GetTotalAnalysesCountAsync(Arg.Any<CancellationToken>());
        // Called twice: once for last hour, once for last 24 hours
        await _statsRepository.Received(2).GetAnalysesCountSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _statsRepository.Received(1).GetTrackedSymbolCountAsync(Arg.Any<CancellationToken>());
        await _statsRepository.Received(1).GetDistinctAnalyzedSymbolsAsync(Arg.Any<CancellationToken>());
    }
}
