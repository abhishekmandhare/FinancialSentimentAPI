using System.Diagnostics;
using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Sentiment.Commands.AnalyzeSentiment;

/// <summary>
/// Orchestrates the AnalyzeSentiment use case:
///   1. Build domain value objects (fail fast on invalid input)
///   2. Call AI service (infrastructure, via interface)
///   3. Create domain entity (validates all invariants, raises domain event internally)
///   4. Persist (infrastructure, via interface)
///   5. Clear domain events (collected by entity; future: dispatch via dedicated dispatcher)
///   6. Publish application notification (for side effects: logging, webhooks, etc.)
///   7. Return response DTO
///
/// Note on domain events: IDomainEvent lives in Domain (no MediatR dependency).
/// To dispatch domain events through MediatR they need a wrapper that implements INotification.
/// For now we clear them and use the application-level SentimentAnalysisCreatedNotification.
/// Future: add a DomainEventDispatcher that wraps domain events in INotification adapters.
///
/// The handler knows WHAT to do. It has no idea HOW persistence or AI works.
/// That is Dependency Inversion in practice.
/// </summary>
public class AnalyzeSentimentCommandHandler(
    IAiSentimentService aiService,
    ISentimentRepository repository,
    IPublisher publisher)
    : IRequestHandler<AnalyzeSentimentCommand, AnalyzeSentimentResponse>
{
    public async Task<AnalyzeSentimentResponse> Handle(
        AnalyzeSentimentCommand command,
        CancellationToken ct)
    {
        var symbol = new StockSymbol(command.Symbol);

        var sw = Stopwatch.StartNew();
        var aiResult = await aiService.AnalyzeAsync(command.Text, symbol, ct);
        sw.Stop();

        var analysis = SentimentAnalysis.Create(
            symbol,
            command.Text,
            command.SourceUrl,
            aiResult.Score,
            aiResult.Confidence,
            aiResult.KeyReasons,
            aiResult.ModelVersion,
            durationMs: sw.ElapsedMilliseconds);

        await repository.AddAsync(analysis, ct);

        // Domain events are collected by the entity but not dispatched through MediatR directly
        // because IDomainEvent doesn't implement INotification (Domain has no MediatR dependency).
        // The application notification below handles cross-cutting side effects instead.
        analysis.ClearDomainEvents();

        // Application-level notification for cross-cutting side effects.
        await publisher.Publish(
            new SentimentAnalysisCreatedNotification(analysis.Id, analysis.Symbol.Value), ct);

        return new AnalyzeSentimentResponse(
            analysis.Id,
            analysis.Symbol.Value,
            analysis.Score.Value,
            analysis.Label.ToString(),
            analysis.Confidence,
            analysis.KeyReasons,
            analysis.ModelVersion,
            analysis.AnalyzedAt,
            analysis.DurationMs);
    }
}
