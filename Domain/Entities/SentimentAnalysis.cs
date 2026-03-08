using Domain.Enums;
using Domain.Events;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities;

/// <summary>
/// The core aggregate root of this bounded context.
///
/// Key design decisions:
///   - Private constructor + static Create() factory enforces invariants at creation time.
///     Invalid state is unrepresentable — you cannot construct a broken SentimentAnalysis.
///   - Domain events are collected here and dispatched by the Application layer
///     after persistence, keeping the domain free of infrastructure concerns.
///   - Label is derived by the domain from Score — the AI returns a raw number,
///     the domain decides what that number means.
/// </summary>
public class SentimentAnalysis
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public StockSymbol Symbol { get; private set; } = null!;
    public string OriginalText { get; private set; } = null!;
    public string? SourceUrl { get; private set; }
    public SentimentScore Score { get; private set; } = null!;
    public SentimentLabel Label { get; private set; }
    public double Confidence { get; private set; }
    public List<string> KeyReasons { get; private set; } = [];
    public string ModelVersion { get; private set; } = null!;
    public DateTime AnalyzedAt { get; private set; }
    public long? DurationMs { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Required by EF Core for materialisation. Not for application use.
    /// </summary>
    private SentimentAnalysis() { }

    public static SentimentAnalysis Create(
        StockSymbol symbol,
        string text,
        string? sourceUrl,
        double aiScore,
        double confidence,
        IReadOnlyList<string> keyReasons,
        string modelVersion,
        long? durationMs = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Analysis text cannot be empty.");

        if (text.Length > 5000)
            throw new DomainException("Analysis text cannot exceed 5000 characters.");

        if (confidence is < 0.0 or > 1.0)
            throw new DomainException($"Confidence must be between 0.0 and 1.0. Got: {confidence}.");

        if (string.IsNullOrWhiteSpace(modelVersion))
            throw new DomainException("Model version must be specified.");

        var score = new SentimentScore(aiScore); // validates range

        var analysis = new SentimentAnalysis
        {
            Id           = Guid.NewGuid(),
            Symbol       = symbol,
            OriginalText = text,
            SourceUrl    = sourceUrl,
            Score        = score,
            Label        = score.DeriveLabel(),  // domain owns this derivation
            Confidence   = confidence,
            KeyReasons   = [.. keyReasons],
            ModelVersion = modelVersion,
            AnalyzedAt   = DateTime.UtcNow,
            DurationMs   = durationMs
        };

        analysis._domainEvents.Add(new SentimentAnalysisCreatedEvent(analysis.Id, symbol));

        return analysis;
    }

    /// <summary>
    /// Called by the Application layer after domain events have been dispatched.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
