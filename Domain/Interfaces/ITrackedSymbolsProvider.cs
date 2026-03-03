using Domain.ValueObjects;

namespace Domain.Interfaces;

/// <summary>
/// Abstracts the source of tracked symbols.
/// Simple version: reads from appsettings (IOptions).
/// Future version: reads from a TrackedSymbols DB table via admin endpoint.
/// Swap implementations without changing any business logic.
/// </summary>
public interface ITrackedSymbolsProvider
{
    Task<IReadOnlyList<StockSymbol>> GetActiveSymbolsAsync(CancellationToken ct = default);
}
