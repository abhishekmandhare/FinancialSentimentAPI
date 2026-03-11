namespace Domain.ValueObjects;

public record StockSymbol
{
    public string Value { get; }

    public StockSymbol(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Stock symbol cannot be empty.", nameof(value));

        if (value.Length > 10)
            throw new ArgumentException("Stock symbol cannot exceed 10 characters.", nameof(value));

        Value = value.ToUpperInvariant();
    }

    /// <summary>
    /// Returns true for cryptocurrency symbols (e.g. BTC-USD, ETH-USD).
    /// </summary>
    public bool IsCrypto => Value.EndsWith("-USD", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the base ticker for crypto symbols (e.g. "BTC" from "BTC-USD"),
    /// or the full symbol for non-crypto symbols.
    /// </summary>
    public string BaseTicker => IsCrypto ? Value[..Value.IndexOf('-')] : Value;

    public override string ToString() => Value;

    public static implicit operator string(StockSymbol symbol) => symbol.Value;
}