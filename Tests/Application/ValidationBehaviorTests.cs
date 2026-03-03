using Application.Behaviors;
using Application.Features.Sentiment.Commands.AnalyzeSentiment;
using FluentAssertions;
using FluentValidation;
using MediatR;
using ValidationException = Application.Exceptions.ValidationException;

namespace Tests.Application;

public class ValidationBehaviorTests
{
    private readonly IValidator<AnalyzeSentimentCommand>[] _validators =
        [new AnalyzeSentimentCommandValidator()];

    private Task<AnalyzeSentimentResponse> FakeNext() =>
        Task.FromResult(new AnalyzeSentimentResponse(
            Guid.NewGuid(), "AAPL", 0.5, "Positive", 0.9, [], "model", DateTime.UtcNow));

    [Fact]
    public async Task Handle_ValidCommand_CallsNext()
    {
        var behavior = new ValidationBehavior<AnalyzeSentimentCommand, AnalyzeSentimentResponse>(_validators);
        var command  = new AnalyzeSentimentCommand("AAPL", "Valid text content here.", null);

        var nextCalled = false;
        var act = () => behavior.Handle(command, () =>
        {
            nextCalled = true;
            return FakeNext();
        }, CancellationToken.None);

        await act.Should().NotThrowAsync();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmptySymbol_ThrowsValidationException()
    {
        var behavior = new ValidationBehavior<AnalyzeSentimentCommand, AnalyzeSentimentResponse>(_validators);
        var command  = new AnalyzeSentimentCommand("", "Valid text content here.", null);

        var act = () => behavior.Handle(command, FakeNext, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey("Symbol"));
    }

    [Fact]
    public async Task Handle_ShortText_ThrowsValidationException()
    {
        var behavior = new ValidationBehavior<AnalyzeSentimentCommand, AnalyzeSentimentResponse>(_validators);
        var command  = new AnalyzeSentimentCommand("AAPL", "Short", null);

        var act = () => behavior.Handle(command, FakeNext, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.ContainsKey("Text"));
    }
}
