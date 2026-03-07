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
        var next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
        next().Returns(expected);

        var result = await CreateBehavior().Handle(new TestRequest(), next, CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_SuccessfulRequest_CallsNext()
    {
        var next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
        next().Returns(new TestResponse("ok"));

        await CreateBehavior().Handle(new TestRequest(), next, CancellationToken.None);

        await next.Received(1)();
    }

    [Fact]
    public async Task Handle_FailingRequest_RethrowsException()
    {
        var next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
        next().Returns<TestResponse>(_ => throw new InvalidOperationException("boom"));

        var act = async () => await CreateBehavior().Handle(new TestRequest(), next, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }
}
