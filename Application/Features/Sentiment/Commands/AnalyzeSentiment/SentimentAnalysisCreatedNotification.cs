using MediatR;

namespace Application.Features.Sentiment.Commands.AnalyzeSentiment;

/// <summary>
/// Application-level notification published after a SentimentAnalysis is persisted.
///
/// Note: this is distinct from the domain event (SentimentAnalysisCreatedEvent).
/// Domain events are raised inside the entity and represent domain facts.
/// This notification is the Application layer's broadcast to any interested handlers.
///
/// Current handlers: LogAnalysisCompletedHandler (no-op placeholder).
/// Future handlers: WebhookNotificationHandler, CacheInvalidationHandler —
/// add them without touching this file or the command handler.
///
/// Important: MediatR's Publish() is awaited by default (sequential handlers).
/// If handlers become slow (webhooks, email), move to IBackgroundTaskQueue.
/// </summary>
public record SentimentAnalysisCreatedNotification(Guid AnalysisId, string Symbol) : INotification;
