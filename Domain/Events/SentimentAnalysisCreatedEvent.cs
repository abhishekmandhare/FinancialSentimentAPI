using Domain.ValueObjects;

namespace Domain.Events;

public record SentimentAnalysisCreatedEvent(
    Guid AnalysisId,
    StockSymbol Symbol) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
