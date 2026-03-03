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
///   3. Create domain entity (validates all invariants, raises domain event)
///   4. Persist (infrastructure, via interface)
///   5. Dispatch domain events collected by the entity
///   6. Publish application notification (for side effects: logging, webhooks, etc.)
///   7. Return response DTO
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

        var aiResult = await aiService.AnalyzeAsync(command.Text, symbol, ct);

        var analysis = SentimentAnalysis.Create(
            symbol,
            command.Text,
            command.SourceUrl,
            aiResult.Score,
            aiResult.Confidence,
            aiResult.KeyReasons,
            aiResult.ModelVersion);

        await repository.AddAsync(analysis, ct);

        // Dispatch domain events raised inside the entity during Create().
        // Done after persistence so events reflect committed state.
        foreach (var domainEvent in analysis.DomainEvents)
            await publisher.Publish(domainEvent, ct);

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
            analysis.AnalyzedAt);
    }
}
