using Domain.Entities;
using Domain.ValueObjects;

namespace Domain.Interfaces;

/// <summary>
/// Domain defines what it needs — Infrastructure decides how to deliver it.
/// The repository interface lives here so the domain and application layers
/// have no dependency on EF Core or any persistence technology.
/// </summary>
public interface ISentimentRepository
{
    Task AddAsync(SentimentAnalysis analysis, CancellationToken ct = default);

    Task<(IReadOnlyList<SentimentAnalysis> Items, int TotalCount)> GetHistoryAsync(
        StockSymbol symbol,
        int page,
        int pageSize,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default);

    Task<IReadOnlyList<SentimentAnalysis>> GetForStatsAsync(
        StockSymbol symbol,
        int days,
        CancellationToken ct = default);
}
