using Domain.ValueObjects;
using FluentAssertions;

namespace Tests.Domain;

public class StockSymbolTests
{
    [Theory]
    [InlineData("AAPL")]
    [InlineData("msft")]   // should be uppercased
    [InlineData("BRK.B")]
    [InlineData("A")]
    public void Create_ValidSymbol_NormalisesToUppercase(string input)
    {
        var symbol = new StockSymbol(input);
        symbol.Value.Should().Be(input.ToUpperInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Create_EmptySymbol_ThrowsArgumentException(string? input)
    {
        var act = () => new StockSymbol(input!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Create_SymbolExceeds10Chars_ThrowsArgumentException()
    {
        var act = () => new StockSymbol("TOOLONGSYMBOL");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*10 characters*");
    }

    [Fact]
    public void ImplicitConversion_ReturnsValue()
    {
        var symbol = new StockSymbol("AAPL");
        string value = symbol;
        value.Should().Be("AAPL");
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = new StockSymbol("AAPL");
        var b = new StockSymbol("aapl"); // uppercased in constructor
        a.Should().Be(b);
    }
}
