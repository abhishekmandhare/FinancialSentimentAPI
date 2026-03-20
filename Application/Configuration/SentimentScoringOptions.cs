namespace Application.Configuration;

/// <summary>
/// Configurable parameters for decay-weighted sentiment scoring.
/// Bound from appsettings "SentimentScoring" section.
/// </summary>
public class SentimentScoringOptions
{
    public const string SectionName = "SentimentScoring";

    /// <summary>Hours for an article's weight to halve (default 36 = 1.5 days).</summary>
    public int HalfLifeHours { get; init; } = 36;

    /// <summary>Default data window in days for trending/watchlist/snapshots.</summary>
    public int DefaultWindowDays { get; init; } = 14;

    /// <summary>Maximum data age in days for stats queries.</summary>
    public int MaxDataAgeDays { get; init; } = 30;
}
