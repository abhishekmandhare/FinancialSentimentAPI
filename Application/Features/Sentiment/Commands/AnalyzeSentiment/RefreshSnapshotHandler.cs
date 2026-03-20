using Application.Configuration;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Sentiment.Commands.AnalyzeSentiment;

/// <summary>
/// Recomputes the SymbolSnapshot after each new analysis is persisted.
/// This materializes decay-weighted stats so GET endpoints read precomputed data.
/// </summary>
public class RefreshSnapshotHandler(
    ISentimentRepository sentimentRepository,
    ISymbolSnapshotRepository snapshotRepository,
    SentimentScoringOptions scoringOptions,
    ILogger<RefreshSnapshotHandler> logger)
    : INotificationHandler<SentimentAnalysisCreatedNotification>
{

    public async Task Handle(SentimentAnalysisCreatedNotification notification, CancellationToken ct)
    {
        try
        {
            var symbol = notification.Symbol;
            var now = DateTime.UtcNow;

            var config = scoringOptions;
            var analyses = await sentimentRepository.GetForStatsAsync(
                new StockSymbol(symbol), config.DefaultWindowDays, ct);

            var stats = SentimentMath.ComputeSymbolStats(
                analyses, now, config.DefaultWindowDays * 24.0, config.HalfLifeHours);

            var existing = await snapshotRepository.GetBySymbolAsync(symbol, ct);

            if (existing is not null)
            {
                existing.Update(stats.Score, stats.PreviousScore, stats.Delta,
                    stats.Direction, stats.Trend, stats.Dispersion, stats.ArticleCount);
                await snapshotRepository.UpsertAsync(existing, ct);
            }
            else
            {
                var snapshot = SymbolSnapshot.Create(
                    symbol, stats.Score, stats.PreviousScore, stats.Delta,
                    stats.Direction, stats.Trend, stats.Dispersion, stats.ArticleCount);
                await snapshotRepository.UpsertAsync(snapshot, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down — don't log as warning
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Failed to refresh snapshot for {Symbol}", notification.Symbol);
        }
    }
}
