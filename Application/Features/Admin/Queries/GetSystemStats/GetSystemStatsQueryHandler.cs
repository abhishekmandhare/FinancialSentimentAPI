using Application.Services;
using MediatR;

namespace Application.Features.Admin.Queries.GetSystemStats;

public class GetSystemStatsQueryHandler(
    ISystemStatsRepository statsRepository)
    : IRequestHandler<GetSystemStatsQuery, SystemStatsDto>
{
    /// <summary>
    /// Estimated average row size in bytes for SentimentAnalyses table.
    /// Based on: GUID (16) + symbol (10) + text (avg 500) + score/confidence (16)
    ///           + reasons (avg 200) + model (20) + timestamp (8) + overhead (~30).
    /// </summary>
    private const int EstimatedAvgRowSizeBytes = 800;

    public async Task<SystemStatsDto> Handle(
        GetSystemStatsQuery request,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var totalCount = await statsRepository.GetTotalAnalysesCountAsync(ct);
        var lastHourCount = await statsRepository.GetAnalysesCountSinceAsync(now.AddHours(-1), ct);
        var last24HCount = await statsRepository.GetAnalysesCountSinceAsync(now.AddHours(-24), ct);
        var trackedSymbolCount = await statsRepository.GetTrackedSymbolCountAsync(ct);
        var analyzedSymbols = await statsRepository.GetDistinctAnalyzedSymbolsAsync(ct);

        var counts = new AnalysisCounts(totalCount, lastHourCount, last24HCount);

        var throughput = new ThroughputStats(
            AnalysesPerHour: last24HCount > 0 ? Math.Round(last24HCount / 24.0, 2) : 0,
            AnalysesPerDay: last24HCount);

        var symbols = new SymbolStats(
            TrackedSymbols: trackedSymbolCount,
            DistinctAnalyzedSymbols: analyzedSymbols.Count,
            AnalyzedSymbols: analyzedSymbols);

        var ingestion = new IngestionConfig(
            PollingIntervalMinutes: 0,
            MaxConcurrentAnalyses: 0,
            QueueDepth: 0);

        var rowsPerDay = last24HCount > 0 ? (double)last24HCount : 0;
        var rowsPerMonth = rowsPerDay * 30;
        var bytesPerMonth = rowsPerMonth * EstimatedAvgRowSizeBytes;
        var mbPerMonth = bytesPerMonth / (1024.0 * 1024.0);

        var projection = new CapacityProjection(
            EstimatedDbGrowthPerMonth: $"{Math.Round(mbPerMonth, 2)} MB",
            EstimatedRowsPerMonth: rowsPerMonth,
            AnalysisLatencyNote: totalCount > 0
                ? "See Ollama throughput -- ~62s per analysis on llama3 (8B)"
                : "No analyses yet -- latency data unavailable");

        return new SystemStatsDto(counts, throughput, symbols, ingestion, projection);
    }
}
