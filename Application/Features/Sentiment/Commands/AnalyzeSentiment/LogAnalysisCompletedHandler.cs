using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Sentiment.Commands.AnalyzeSentiment;

/// <summary>
/// Placeholder notification handler — proves the pipeline wiring works end-to-end.
/// Future handlers (webhooks, alerts, cache invalidation) follow this exact pattern:
/// implement INotificationHandler, register in DI — zero other changes needed.
/// </summary>
public class LogAnalysisCompletedHandler(ILogger<LogAnalysisCompletedHandler> logger)
    : INotificationHandler<SentimentAnalysisCreatedNotification>
{
    public Task Handle(SentimentAnalysisCreatedNotification notification, CancellationToken ct)
    {
        logger.LogInformation(
            "Sentiment analysis completed for {Symbol} (Id: {AnalysisId})",
            notification.Symbol,
            notification.AnalysisId);

        return Task.CompletedTask;
    }
}
