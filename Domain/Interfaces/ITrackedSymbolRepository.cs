using Domain.Entities;

namespace Domain.Interfaces;

/// <summary>
/// Persistence contract for tracked symbols.
/// Domain defines what it needs — Infrastructure decides how to deliver it.
/// </summary>
public interface ITrackedSymbolRepository
{
    Task<IReadOnlyList<TrackedSymbol>> GetAllAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(string symbol, CancellationToken ct = default);
    Task AddAsync(TrackedSymbol symbol, CancellationToken ct = default);

    /// <returns>true if the symbol was found and removed; false if it did not exist.</returns>
    Task<bool> RemoveAsync(string symbol, CancellationToken ct = default);
}
