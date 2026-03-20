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
    ILogger<RefreshSnapshotHandler> logger)
    : INotificationHandler<SentimentAnalysisCreatedNotification>
{
    private const int DataWindowDays = 7;

    public async Task Handle(SentimentAnalysisCreatedNotification notification, CancellationToken ct)
    {
        try
        {
            var symbol = notification.Symbol;
            var now = DateTime.UtcNow;
            var midpoint = now.AddDays(-DataWindowDays / 2.0);

            var analyses = await sentimentRepository.GetForStatsAsync(
                new StockSymbol(symbol), DataWindowDays, ct);

            var current = analyses.Where(a => a.AnalyzedAt >= midpoint).ToList();
            var previous = analyses.Where(a => a.AnalyzedAt < midpoint).ToList();

            var currentAvg = Math.Round(SentimentMath.DecayWeightedAverage(current, now), 4);
            var previousAvg = Math.Round(SentimentMath.DecayWeightedAverage(previous, midpoint), 4);
            var delta = Math.Round(currentAvg - previousAvg, 4);

            var direction = delta switch
            {
                > 0 => "up",
                < 0 => "down",
                _ => "flat"
            };

            var trend = SentimentMath.CalculateTrendDirection(analyses);
            var dispersion = SentimentMath.CalculateDispersion(analyses, now);

            var existing = await snapshotRepository.GetBySymbolAsync(symbol, ct);

            if (existing is not null)
            {
                existing.Update(currentAvg, previousAvg, delta, direction, trend, dispersion, analyses.Count);
                await snapshotRepository.UpsertAsync(existing, ct);
            }
            else
            {
                var snapshot = SymbolSnapshot.Create(
                    symbol, currentAvg, previousAvg, delta, direction, trend, dispersion, analyses.Count);
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
