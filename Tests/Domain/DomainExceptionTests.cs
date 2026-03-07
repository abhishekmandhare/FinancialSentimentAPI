using Domain.Exceptions;
using FluentAssertions;

namespace Tests.Domain;

public class DomainExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var ex = new DomainException("Something went wrong.");
        ex.Message.Should().Be("Something went wrong.");
    }

    [Fact]
    public void Constructor_WithInnerException_SetsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new DomainException("outer", inner);

        ex.Message.Should().Be("outer");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void Constructor_Default_HasDefaultMessage()
    {
        var ex = new DomainException();
        ex.Message.Should().NotBeNullOrEmpty();
    }
}
