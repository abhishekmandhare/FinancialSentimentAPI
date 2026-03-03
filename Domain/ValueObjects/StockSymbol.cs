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

    public override string ToString() => Value;

    public static implicit operator string(StockSymbol symbol) => symbol.Value;
}