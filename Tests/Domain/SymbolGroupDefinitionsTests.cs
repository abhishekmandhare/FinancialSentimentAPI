using Domain;
using Domain.Enums;
using FluentAssertions;

namespace Tests.Domain;

public class SymbolGroupDefinitionsTests
{
    [Theory]
    [InlineData(SymbolGroup.UsBluechip, 30)]
    [InlineData(SymbolGroup.AsxBluechip, 20)]
    [InlineData(SymbolGroup.Crypto, 15)]
    [InlineData(SymbolGroup.Tech, 20)]
    [InlineData(SymbolGroup.Etfs, 10)]
    public void GetSymbols_ValidGroup_ReturnsExpectedCount(SymbolGroup group, int expectedCount)
    {
        var symbols = SymbolGroupDefinitions.GetSymbols(group);

        symbols.Should().NotBeNull();
        symbols.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void GetSymbols_AllSymbolsAreUpperCase()
    {
        foreach (var group in SymbolGroupDefinitions.AvailableGroups)
        {
            var symbols = SymbolGroupDefinitions.GetSymbols(group)!;
            symbols.Should().OnlyContain(s => s == s.ToUpperInvariant(),
                $"all symbols in {group} should be uppercase");
        }
    }

    [Fact]
    public void GetSymbols_AllSymbolsAreWithinLengthLimit()
    {
        foreach (var group in SymbolGroupDefinitions.AvailableGroups)
        {
            var symbols = SymbolGroupDefinitions.GetSymbols(group)!;
            symbols.Should().OnlyContain(s => s.Length <= 10,
                $"all symbols in {group} should be 10 chars or fewer");
        }
    }

    [Fact]
    public void AvailableGroups_ContainsAllExpectedGroups()
    {
        SymbolGroupDefinitions.AvailableGroups.Should().Contain(SymbolGroup.UsBluechip);
        SymbolGroupDefinitions.AvailableGroups.Should().Contain(SymbolGroup.AsxBluechip);
        SymbolGroupDefinitions.AvailableGroups.Should().Contain(SymbolGroup.Crypto);
        SymbolGroupDefinitions.AvailableGroups.Should().Contain(SymbolGroup.Tech);
        SymbolGroupDefinitions.AvailableGroups.Should().Contain(SymbolGroup.Etfs);
    }

    [Fact]
    public void AvailableGroups_HasFiveGroups()
    {
        SymbolGroupDefinitions.AvailableGroups.Should().HaveCount(5);
    }

    [Fact]
    public void GetSymbols_CryptoGroup_ContainsBtcAndEth()
    {
        var symbols = SymbolGroupDefinitions.GetSymbols(SymbolGroup.Crypto)!;
        symbols.Should().Contain("BTC-USD");
        symbols.Should().Contain("ETH-USD");
    }

    [Fact]
    public void GetSymbols_NoDuplicatesWithinGroup()
    {
        foreach (var group in SymbolGroupDefinitions.AvailableGroups)
        {
            var symbols = SymbolGroupDefinitions.GetSymbols(group)!;
            symbols.Should().OnlyHaveUniqueItems($"{group} should not have duplicate symbols");
        }
    }
}
