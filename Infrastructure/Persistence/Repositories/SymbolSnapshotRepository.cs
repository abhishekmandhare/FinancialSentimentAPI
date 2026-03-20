using Domain.Entities;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class SymbolSnapshotRepository(AppDbContext db) : ISymbolSnapshotRepository
{
    public async Task<SymbolSnapshot?> GetBySymbolAsync(string symbol, CancellationToken ct = default) =>
        await db.SymbolSnapshots.FirstOrDefaultAsync(s => s.Symbol == symbol, ct);

    public async Task<IReadOnlyList<SymbolSnapshot>> GetAllAsync(CancellationToken ct = default) =>
        await db.SymbolSnapshots.OrderBy(s => s.Symbol).ToListAsync(ct);

    public async Task UpsertAsync(SymbolSnapshot snapshot, CancellationToken ct = default)
    {
        var existing = await db.SymbolSnapshots
            .FirstOrDefaultAsync(s => s.Symbol == snapshot.Symbol, ct);

        if (existing is null)
            db.SymbolSnapshots.Add(snapshot);
        else
            db.Entry(existing).CurrentValues.SetValues(new
            {
                snapshot.Score,
                snapshot.PreviousScore,
                snapshot.Delta,
                snapshot.Direction,
                snapshot.Trend,
                snapshot.Dispersion,
                snapshot.ArticleCount,
                snapshot.UpdatedAt
            });

        await db.SaveChangesAsync(ct);
    }
}
