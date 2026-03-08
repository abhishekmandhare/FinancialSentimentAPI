using Application.Features.Sentiment.Commands.AnalyzeSentiment;
using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Tests.Application;

public class AnalyzeSentimentHandlerTests
{
    private readonly IAiSentimentService _aiService = Substitute.For<IAiSentimentService>();
    private readonly ISentimentRepository _repository = Substitute.For<ISentimentRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();

    private AnalyzeSentimentCommandHandler CreateHandler() =>
        new(_aiService, _repository, _publisher);

    private static readonly AiSentimentResult FakeAiResult = new(
        Score:        0.75,
        Confidence:   0.9,
        KeyReasons:   ["Strong earnings", "Beat guidance"],
        ModelVersion: "claude-haiku-4-5-20251001");

    [Fact]
    public async Task Handle_ValidCommand_ReturnsMappedResponse()
    {
        _aiService.AnalyzeAsync(Arg.Any<string>(), Arg.Any<StockSymbol>(), Arg.Any<CancellationToken>())
            .Returns(FakeAiResult);

        var command = new AnalyzeSentimentCommand("AAPL", "Apple reported strong earnings.", null);
        var response = await CreateHandler().Handle(command, CancellationToken.None);

        response.Symbol.Should().Be("AAPL");
        response.Score.Should().Be(0.75);
        response.Label.Should().Be("Positive");
        response.Confidence.Should().Be(0.9);
        response.KeyReasons.Should().HaveCount(2);
        response.DurationMs.Should().NotBeNull("AI analysis should record elapsed time");
        response.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Handle_PersistsAnalysis()
    {
        _aiService.AnalyzeAsync(Arg.Any<string>(), Arg.Any<StockSymbol>(), Arg.Any<CancellationToken>())
            .Returns(FakeAiResult);

        var command = new AnalyzeSentimentCommand("AAPL", "Apple reported strong earnings.", null);
        await CreateHandler().Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(Arg.Any<SentimentAnalysis>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PublishesNotification()
    {
        _aiService.AnalyzeAsync(Arg.Any<string>(), Arg.Any<StockSymbol>(), Arg.Any<CancellationToken>())
            .Returns(FakeAiResult);

        var command = new AnalyzeSentimentCommand("AAPL", "Apple reported strong earnings.", null);
        await CreateHandler().Handle(command, CancellationToken.None);

        await _publisher.Received(1).Publish(
            Arg.Any<SentimentAnalysisCreatedNotification>(),
            Arg.Any<CancellationToken>());
    }
}
