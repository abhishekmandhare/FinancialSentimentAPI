using Application.Behaviors;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Tests.Application;

public class LoggingBehaviorTests
{
    private readonly ILogger<LoggingBehavior<TestRequest, TestResponse>> _logger =
        Substitute.For<ILogger<LoggingBehavior<TestRequest, TestResponse>>>();

    public record TestRequest : IRequest<TestResponse>;
    public record TestResponse(string Value);

    private LoggingBehavior<TestRequest, TestResponse> CreateBehavior() => new(_logger);

    [Fact]
    public async Task Handle_SuccessfulRequest_ReturnsResponse()
    {
        var expected = new TestResponse("ok");
        RequestHandlerDelegate<TestResponse> next = () => Task.FromResult(expected);

        var result = await CreateBehavior().Handle(new TestRequest(), next, CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_FailingRequest_RethrowsException()
    {
        RequestHandlerDelegate<TestResponse> next = () => throw new InvalidOperationException("boom");

        var act = () => CreateBehavior().Handle(new TestRequest(), next, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task Handle_SuccessfulRequest_LogsHandlingAndHandled()
    {
        RequestHandlerDelegate<TestResponse> next = () => Task.FromResult(new TestResponse("ok"));

        await CreateBehavior().Handle(new TestRequest(), next, CancellationToken.None);

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Handling")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_FailingRequest_LogsError()
    {
        RequestHandlerDelegate<TestResponse> next = () => throw new InvalidOperationException("boom");

        try { await CreateBehavior().Handle(new TestRequest(), next, CancellationToken.None); } catch { }

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
