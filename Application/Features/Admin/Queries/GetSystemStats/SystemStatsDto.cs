namespace Application.Features.Admin.Queries.GetSystemStats;

public record SystemStatsDto(
    AnalysisCounts Counts,
    ThroughputStats Throughput,
    SymbolStats Symbols,
    IngestionConfig Ingestion,
    CapacityProjection Projection);

public record AnalysisCounts(
    int Total,
    int LastHour,
    int Last24Hours);

public record ThroughputStats(
    double AnalysesPerHour,
    double AnalysesPerDay);

public record SymbolStats(
    int TrackedSymbols,
    int DistinctAnalyzedSymbols,
    IReadOnlyList<string> AnalyzedSymbols);

public record IngestionConfig(
    int PollingIntervalMinutes,
    int MaxConcurrentAnalyses,
    int QueueDepth);

public record CapacityProjection(
    string EstimatedDbGrowthPerMonth,
    double EstimatedRowsPerMonth,
    double AverageLatencySeconds,
    string AnalysisLatencyNote);
