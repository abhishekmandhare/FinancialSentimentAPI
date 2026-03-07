using Application.Features.Sentiment.Commands.AnalyzeSentiment;
using Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Tests.Application;

public class LogAnalysisCompletedHandlerTests
{
    private readonly ILogger<LogAnalysisCompletedHandler> _logger =
        Substitute.For<ILogger<LogAnalysisCompletedHandler>>();

    private LogAnalysisCompletedHandler CreateHandler() => new(_logger);

    [Fact]
    public async Task Handle_LogsAnalysisCompletion()
    {
        var notification = new SentimentAnalysisCreatedNotification(
            Guid.NewGuid(), new StockSymbol("AAPL"));

        var act = async () => await CreateHandler().Handle(notification, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_CompletesSuccessfully()
    {
        var notification = new SentimentAnalysisCreatedNotification(
            Guid.NewGuid(), new StockSymbol("TSLA"));

        var handler = CreateHandler();
        var result = handler.Handle(notification, CancellationToken.None);

        await result;
        result.IsCompletedSuccessfully.Should().BeTrue();
    }
}
