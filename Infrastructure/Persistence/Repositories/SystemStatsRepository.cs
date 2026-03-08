using Application.Services;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class SystemStatsRepository(AppDbContext context) : ISystemStatsRepository
{
    public async Task<int> GetTotalAnalysesCountAsync(CancellationToken ct = default)
        => await context.SentimentAnalyses.CountAsync(ct);

    public async Task<int> GetAnalysesCountSinceAsync(DateTime since, CancellationToken ct = default)
        => await context.SentimentAnalyses.CountAsync(a => a.AnalyzedAt >= since, ct);

    public async Task<double> GetAverageAnalysisLatencySecondsAsync(int recentCount, CancellationToken ct = default)
    {
        var avg = await context.SentimentAnalyses
            .Where(a => a.DurationMs != null)
            .OrderByDescending(a => a.AnalyzedAt)
            .Take(recentCount)
            .AverageAsync(a => (double?)a.DurationMs, ct);

        return avg.HasValue ? Math.Round(avg.Value / 1000.0, 2) : 0.0;
    }

    public async Task<int> GetTrackedSymbolCountAsync(CancellationToken ct = default)
        => await context.TrackedSymbols.CountAsync(ct);

    public async Task<IReadOnlyList<string>> GetDistinctAnalyzedSymbolsAsync(CancellationToken ct = default)
        => await context.SentimentAnalyses
            .Select(a => (string)a.Symbol)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);
}
