namespace Infrastructure.Ingestion;

/// <summary>
/// Configuration for the background ingestion pipeline.
/// TrackedSymbols: simple version — edit appsettings.json, reloads without restart.
/// Future: replace with DB-backed admin endpoint.
/// </summary>
public class IngestionOptions
{
    public const string SectionName = "Ingestion";

    public List<string> TrackedSymbols { get; init; } = [];
    public List<string> SeedGroups { get; init; } = [];
    public int PollingIntervalMinutes { get; init; } = 15;
    public int MaxConcurrentAnalyses { get; init; } = 3;
}
