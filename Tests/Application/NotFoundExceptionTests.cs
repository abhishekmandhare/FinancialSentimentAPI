using Application.Exceptions;
using FluentAssertions;

namespace Tests.Application;

public class NotFoundExceptionTests
{
    [Fact]
    public void Constructor_FormatsMessage_WithResourceAndKey()
    {
        var ex = new NotFoundException("SentimentAnalysis", "AAPL");
        ex.Message.Should().Contain("SentimentAnalysis");
        ex.Message.Should().Contain("AAPL");
        ex.Message.Should().Contain("not found");
    }

    [Fact]
    public void Constructor_WithGuidKey_IncludesKeyInMessage()
    {
        var key = Guid.NewGuid();
        var ex = new NotFoundException("TrackedSymbol", key);
        ex.Message.Should().Contain(key.ToString());
    }
}
