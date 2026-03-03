namespace Domain.Events;

/// <summary>
/// Marker interface for domain events.
/// Domain events represent facts that happened in the domain.
/// Kept in Domain with zero external dependencies — the Application
/// layer is responsible for dispatching them after persistence.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
