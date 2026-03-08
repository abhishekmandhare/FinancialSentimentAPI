namespace Application.Services;

/// <summary>
/// Provides system-level statistics for observability and capacity planning.
/// Application defines the interface — Infrastructure implements it against the real DB.
/// </summary>
public interface ISystemStatsRepository
{
    Task<int> GetTotalAnalysesCountAsync(CancellationToken ct = default);
    Task<int> GetAnalysesCountSinceAsync(DateTime since, CancellationToken ct = default);
    Task<double> GetAverageAnalysisLatencySecondsAsync(int recentCount, CancellationToken ct = default);
    Task<int> GetTrackedSymbolCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDistinctAnalyzedSymbolsAsync(CancellationToken ct = default);
}
