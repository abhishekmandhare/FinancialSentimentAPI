using Domain.Entities;
using Domain.Exceptions;
using FluentAssertions;

namespace Tests.Domain;

public class TrackedSymbolTests
{
    [Fact]
    public void Create_ValidSymbol_ReturnsEntityWithCorrectValues()
    {
        var symbol = TrackedSymbol.Create("AAPL");

        symbol.Symbol.Should().Be("AAPL");
        symbol.Id.Should().NotBeEmpty();
        symbol.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_LowercaseSymbol_ConvertsToUppercase()
    {
        var symbol = TrackedSymbol.Create("msft");

        symbol.Symbol.Should().Be("MSFT");
    }

    [Fact]
    public void Create_MixedCaseSymbol_ConvertsToUppercase()
    {
        var symbol = TrackedSymbol.Create("GoOg");

        symbol.Symbol.Should().Be("GOOG");
    }

    [Fact]
    public void Create_EmptySymbol_ThrowsDomainException()
    {
        var act = () => TrackedSymbol.Create("");

        act.Should().Throw<DomainException>().WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Create_WhitespaceSymbol_ThrowsDomainException()
    {
        var act = () => TrackedSymbol.Create("   ");

        act.Should().Throw<DomainException>().WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Create_NullSymbol_ThrowsDomainException()
    {
        var act = () => TrackedSymbol.Create(null!);

        act.Should().Throw<DomainException>().WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Create_SymbolExceeds10Characters_ThrowsDomainException()
    {
        var act = () => TrackedSymbol.Create("ABCDEFGHIJK");

        act.Should().Throw<DomainException>().WithMessage("*cannot exceed 10 characters*");
    }

    [Fact]
    public void Create_SymbolExactly10Characters_Succeeds()
    {
        var act = () => TrackedSymbol.Create("ABCDEFGHIJ");

        act.Should().NotThrow();
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var a = TrackedSymbol.Create("AAPL");
        var b = TrackedSymbol.Create("AAPL");

        a.Id.Should().NotBe(b.Id);
    }
}
