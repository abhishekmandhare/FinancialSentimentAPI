using Application.Configuration;
using Application.Features.Sentiment;
using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ingestion;

/// <summary>
/// Runs once on startup: backfills missing SymbolSnapshots for symbols that were
/// analyzed before the snapshot feature was deployed. Ensures the trending endpoint
/// (which reads from snapshots) includes all historically analyzed symbols.
/// </summary>
public class SnapshotBackfillWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SnapshotBackfillWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var statsRepo = sp.GetRequiredService<ISystemStatsRepository>();
            var snapshotRepo = sp.GetRequiredService<ISymbolSnapshotRepository>();
            var sentimentRepo = sp.GetRequiredService<ISentimentRepository>();
            var scoringOptions = sp.GetRequiredService<SentimentScoringOptions>();

            var analyzedSymbols = await statsRepo.GetDistinctAnalyzedSymbolsAsync(stoppingToken);
            var existingSnapshots = await snapshotRepo.GetAllAsync(stoppingToken);
            var existingSymbols = existingSnapshots.Select(s => s.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = analyzedSymbols.Where(s => !existingSymbols.Contains(s)).ToList();

            if (missing.Count == 0)
            {
                logger.LogInformation("Snapshot backfill: all {Count} analyzed symbols already have snapshots.", analyzedSymbols.Count);
                return;
            }

            logger.LogInformation("Snapshot backfill: {Missing} of {Total} analyzed symbols need snapshots.",
                missing.Count, analyzedSymbols.Count);

            var now = DateTime.UtcNow;
            var backfilled = 0;

            foreach (var symbol in missing)
            {
                stoppingToken.ThrowIfCancellationRequested();

                var analyses = await sentimentRepo.GetForStatsAsync(
                    new StockSymbol(symbol), scoringOptions.DefaultWindowDays, stoppingToken);

                var stats = SentimentMath.ComputeSymbolStats(
                    analyses, now,
                    scoringOptions.DefaultWindowDays * 24.0,
                    scoringOptions.HalfLifeHours);

                var snapshot = SymbolSnapshot.Create(
                    symbol, stats.Score, stats.PreviousScore, stats.Delta,
                    stats.Direction, stats.Trend.Direction, stats.Dispersion, stats.ArticleCount);

                await snapshotRepo.UpsertAsync(snapshot, stoppingToken);
                backfilled++;
            }

            logger.LogInformation("Snapshot backfill complete. Created {Count} snapshots.", backfilled);
        }
        catch (OperationCanceledException)
        {
            // Shutting down — expected during graceful shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Snapshot backfill failed.");
        }
    }
}
