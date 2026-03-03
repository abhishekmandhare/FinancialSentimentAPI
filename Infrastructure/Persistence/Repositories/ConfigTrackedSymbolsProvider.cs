using Domain.Interfaces;
using Domain.ValueObjects;
using Infrastructure.Ingestion;
using Microsoft.Extensions.Options;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Simple version: reads tracked symbols from appsettings.json.
/// IOptionsMonitor reloads automatically when the config file changes — no restart needed.
///
/// Future version: replace with a DB-backed provider that reads from a TrackedSymbols table,
/// managed via an admin endpoint. Zero changes to ITrackedSymbolsProvider consumers.
/// </summary>
public class ConfigTrackedSymbolsProvider(IOptionsMonitor<IngestionOptions> options)
    : ITrackedSymbolsProvider
{
    public Task<IReadOnlyList<StockSymbol>> GetActiveSymbolsAsync(CancellationToken ct = default)
    {
        var symbols = options.CurrentValue.TrackedSymbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => new StockSymbol(s))
            .ToList();

        return Task.FromResult<IReadOnlyList<StockSymbol>>(symbols);
    }
}
