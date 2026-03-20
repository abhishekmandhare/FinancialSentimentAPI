namespace Domain.Entities;

/// <summary>
/// Precomputed sentiment statistics for a symbol, materialized after each analysis.
/// Read by trending and watchlist endpoints for instant responses.
/// </summary>
public class SymbolSnapshot
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; } = null!;
    public double Score { get; private set; }
    public double PreviousScore { get; private set; }
    public double Delta { get; private set; }
    public string Direction { get; private set; } = "flat";
    public string Trend { get; private set; } = "Stable";
    public double Dispersion { get; private set; }
    public int ArticleCount { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SymbolSnapshot() { }

    public static SymbolSnapshot Create(
        string symbol,
        double score,
        double previousScore,
        double delta,
        string direction,
        string trend,
        double dispersion,
        int articleCount)
    {
        return new SymbolSnapshot
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            Score = score,
            PreviousScore = previousScore,
            Delta = delta,
            Direction = direction,
            Trend = trend,
            Dispersion = dispersion,
            ArticleCount = articleCount,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        double score,
        double previousScore,
        double delta,
        string direction,
        string trend,
        double dispersion,
        int articleCount)
    {
        Score = score;
        PreviousScore = previousScore;
        Delta = delta;
        Direction = direction;
        Trend = trend;
        Dispersion = dispersion;
        ArticleCount = articleCount;
        UpdatedAt = DateTime.UtcNow;
    }
}
