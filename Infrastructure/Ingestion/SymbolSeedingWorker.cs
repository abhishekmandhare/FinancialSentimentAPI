using Domain;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ingestion;

/// <summary>
/// Runs once on startup: seeds tracked symbols from configured groups.
/// Skips symbols that already exist. Completes before ingestion workers start polling.
/// </summary>
public class SymbolSeedingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IngestionOptions> options,
    ILogger<SymbolSeedingWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var groups = options.Value.SeedGroups;

        if (groups.Count == 0)
        {
            logger.LogDebug("No seed groups configured. Skipping symbol seeding.");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITrackedSymbolRepository>();

        var totalAdded = 0;
        var totalSkipped = 0;

        foreach (var groupName in groups)
        {
            if (!Enum.TryParse<SymbolGroup>(NormaliseGroupName(groupName), ignoreCase: true, out var group))
            {
                logger.LogWarning("Unknown seed group '{Group}'. Skipping.", groupName);
                continue;
            }

            var symbols = SymbolGroupDefinitions.GetSymbols(group);
            if (symbols is null) continue;

            var added = 0;
            foreach (var symbol in symbols)
            {
                if (await repository.ExistsAsync(symbol, stoppingToken))
                {
                    totalSkipped++;
                    continue;
                }

                await repository.AddAsync(TrackedSymbol.Create(symbol), stoppingToken);
                added++;
                totalAdded++;
            }

            if (added > 0)
                logger.LogInformation("Seeded {Count} symbols from group '{Group}'.", added, groupName);
        }

        if (totalAdded > 0)
            logger.LogInformation("Symbol seeding complete. Added: {Added}, Skipped (already exist): {Skipped}.",
                totalAdded, totalSkipped);
        else
            logger.LogInformation("Symbol seeding complete. All {Skipped} symbols already exist.", totalSkipped);
    }

    /// <summary>
    /// Converts kebab-case (e.g. "us-bluechip") to PascalCase (e.g. "UsBluechip").
    /// </summary>
    internal static string NormaliseGroupName(string input) =>
        string.Concat(input.Split('-').Select(part =>
            part.Length == 0 ? "" : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
}
