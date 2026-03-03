using MediatR;

namespace Application.Features.Sentiment.Commands.AnalyzeSentiment;

/// <summary>
/// Command: express intent to analyze sentiment for a piece of financial text.
/// Records are ideal for commands — immutable, value-equality, concise.
/// CQRS: commands change state. Queries never do.
/// </summary>
public record AnalyzeSentimentCommand(
    string Symbol,
    string Text,
    string? SourceUrl) : IRequest<AnalyzeSentimentResponse>;
