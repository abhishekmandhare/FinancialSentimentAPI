using Domain.Entities;

namespace Domain.Interfaces;

public interface ISymbolSnapshotRepository
{
    Task<SymbolSnapshot?> GetBySymbolAsync(string symbol, CancellationToken ct = default);
    Task<IReadOnlyList<SymbolSnapshot>> GetAllAsync(CancellationToken ct = default);
    Task UpsertAsync(SymbolSnapshot snapshot, CancellationToken ct = default);
}
