using Domain.Entities;
using Domain.Exceptions;
using FluentAssertions;

namespace Tests.Domain;

public class TrackedSymbolTests
{
    [Fact]
    public void Create_ValidSymbol_ReturnsEntity()
    {
        var symbol = TrackedSymbol.Create("AAPL");

        symbol.Symbol.Should().Be("AAPL");
        symbol.Id.Should().NotBe(Guid.Empty);
        symbol.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_LowercaseSymbol_NormalisesToUppercase()
    {
        var symbol = TrackedSymbol.Create("msft");
        symbol.Symbol.Should().Be("MSFT");
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
    public void Create_SymbolExceeds10Chars_ThrowsDomainException()
    {
        var act = () => TrackedSymbol.Create("TOOLONGSYMBOL");
        act.Should().Throw<DomainException>().WithMessage("*10 characters*");
    }

    [Fact]
    public void Create_ExactlyTenChars_Succeeds()
    {
        var symbol = TrackedSymbol.Create("ABCDEFGHIJ");
        symbol.Symbol.Should().Be("ABCDEFGHIJ");
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var a = TrackedSymbol.Create("AAPL");
        var b = TrackedSymbol.Create("AAPL");
        a.Id.Should().NotBe(b.Id);
    }
}
