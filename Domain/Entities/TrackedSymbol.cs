using Domain.Exceptions;

namespace Domain.Entities;

/// <summary>
/// Represents a stock symbol actively tracked by the ingestion pipeline.
/// Persisted to DB so symbols survive restarts and can be managed at runtime
/// via the admin API without redeployment.
/// </summary>
public class TrackedSymbol
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; } = null!;
    public string Source { get; private set; } = "seed";
    public DateTime AddedAt { get; private set; }

    /// <summary>
    /// Required by EF Core for materialisation. Not for application use.
    /// </summary>
    private TrackedSymbol() { }

    public static TrackedSymbol Create(string symbol, string source = "seed")
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new DomainException("Symbol cannot be empty.");

        if (symbol.Length > 10)
            throw new DomainException("Symbol cannot exceed 10 characters.");

        if (source is not ("seed" or "watchlist"))
            throw new DomainException("Source must be 'seed' or 'watchlist'.");

        return new TrackedSymbol
        {
            Id       = Guid.NewGuid(),
            Symbol   = symbol.ToUpperInvariant(),
            Source   = source,
            AddedAt  = DateTime.UtcNow
        };
    }

    public void UpdateSource(string source)
    {
        if (source is not ("seed" or "watchlist"))
            throw new DomainException("Source must be 'seed' or 'watchlist'.");

        Source = source;
    }
}
