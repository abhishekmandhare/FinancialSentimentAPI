using Domain.Entities;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class TrackedSymbolRepository(AppDbContext db) : ITrackedSymbolRepository
{
    public async Task<IReadOnlyList<TrackedSymbol>> GetAllAsync(CancellationToken ct = default) =>
        await db.TrackedSymbols
            .OrderBy(s => s.Symbol)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TrackedSymbol>> GetBySourceAsync(string source, CancellationToken ct = default) =>
        await db.TrackedSymbols
            .Where(s => s.Source == source)
            .OrderBy(s => s.Symbol)
            .ToListAsync(ct);

    public async Task<TrackedSymbol?> GetBySymbolAsync(string symbol, CancellationToken ct = default) =>
        await db.TrackedSymbols
            .FirstOrDefaultAsync(s => s.Symbol == symbol.ToUpperInvariant(), ct);

    public Task<bool> ExistsAsync(string symbol, CancellationToken ct = default) =>
        db.TrackedSymbols.AnyAsync(s => s.Symbol == symbol, ct);

    public async Task AddAsync(TrackedSymbol symbol, CancellationToken ct = default)
    {
        db.TrackedSymbols.Add(symbol);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TrackedSymbol symbol, CancellationToken ct = default)
    {
        db.TrackedSymbols.Update(symbol);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> RemoveAsync(string symbol, CancellationToken ct = default)
    {
        var entity = await db.TrackedSymbols
            .FirstOrDefaultAsync(s => s.Symbol == symbol, ct);

        if (entity is null)
            return false;

        db.TrackedSymbols.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
