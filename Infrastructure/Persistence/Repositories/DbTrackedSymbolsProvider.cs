using Domain.Interfaces;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// DB-backed implementation of ITrackedSymbolsProvider.
/// Replaces ConfigTrackedSymbolsProvider — no restart required to pick up new symbols.
/// The ingestion workers call this on every poll cycle, so changes take effect immediately.
/// </summary>
public class DbTrackedSymbolsProvider(AppDbContext db) : ITrackedSymbolsProvider
{
    public async Task<IReadOnlyList<StockSymbol>> GetActiveSymbolsAsync(CancellationToken ct = default)
    {
        var symbols = await db.TrackedSymbols
            .Select(s => s.Symbol)
            .ToListAsync(ct);

        return symbols.Select(s => new StockSymbol(s)).ToList();
    }
}
