using Application.Features.Symbols.Commands.SeedSymbols;
using Domain.Entities;
using Domain.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace Tests.Application;

public class SeedSymbolsHandlerTests
{
    private readonly ITrackedSymbolRepository _repository = Substitute.For<ITrackedSymbolRepository>();

    private SeedSymbolsCommandHandler CreateHandler() => new(_repository);

    [Fact]
    public async Task Handle_ValidGroup_AddsAllNewSymbols()
    {
        _repository.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(
            new SeedSymbolsCommand("crypto"), CancellationToken.None);

        result.Group.Should().Be("crypto");
        result.Added.Should().Be(15);
        result.Skipped.Should().Be(0);
        result.AddedSymbols.Should().Contain("BTC-USD");
        result.AddedSymbols.Should().Contain("ETH-USD");
        await _repository.Received(15).AddAsync(Arg.Any<TrackedSymbol>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithExistingSymbols_SkipsDuplicates()
    {
        _repository.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repository.ExistsAsync("BTC-USD", Arg.Any<CancellationToken>()).Returns(true);
        _repository.ExistsAsync("ETH-USD", Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(
            new SeedSymbolsCommand("crypto"), CancellationToken.None);

        result.Added.Should().Be(13);
        result.Skipped.Should().Be(2);
        result.AddedSymbols.Should().NotContain("BTC-USD");
        result.AddedSymbols.Should().NotContain("ETH-USD");
    }

    [Fact]
    public async Task Handle_AllSymbolsExist_ReturnsZeroAdded()
    {
        _repository.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(
            new SeedSymbolsCommand("etfs"), CancellationToken.None);

        result.Added.Should().Be(0);
        result.Skipped.Should().Be(10);
        result.AddedSymbols.Should().BeEmpty();
        await _repository.DidNotReceive().AddAsync(Arg.Any<TrackedSymbol>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_KebabCaseGroupName_ParsesCorrectly()
    {
        _repository.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(
            new SeedSymbolsCommand("us-bluechip"), CancellationToken.None);

        result.Group.Should().Be("us-bluechip");
        result.Added.Should().Be(30);
        result.AddedSymbols.Should().Contain("AAPL");
        result.AddedSymbols.Should().Contain("MSFT");
    }

    [Fact]
    public async Task Handle_UsBluechipGroup_ContainsExpectedSymbols()
    {
        _repository.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(
            new SeedSymbolsCommand("us-bluechip"), CancellationToken.None);

        result.AddedSymbols.Should().Contain("AAPL");
        result.AddedSymbols.Should().Contain("JPM");
        result.AddedSymbols.Should().Contain("NVDA");
    }

    [Fact]
    public async Task Handle_TechGroup_ContainsExpectedSymbols()
    {
        _repository.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(
            new SeedSymbolsCommand("tech"), CancellationToken.None);

        result.AddedSymbols.Should().Contain("AMD");
        result.AddedSymbols.Should().Contain("ADBE");
        result.AddedSymbols.Should().Contain("CRM");
    }

    [Fact]
    public async Task Handle_AsxBluechipGroup_ContainsExpectedSymbols()
    {
        _repository.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(
            new SeedSymbolsCommand("asx-bluechip"), CancellationToken.None);

        result.AddedSymbols.Should().Contain("BHP.AX");
        result.AddedSymbols.Should().Contain("CBA.AX");
        result.Added.Should().Be(20);
    }
}

public class SeedSymbolsCommandValidatorTests
{
    private readonly SeedSymbolsCommandValidator _validator = new();

    [Theory]
    [InlineData("us-bluechip")]
    [InlineData("asx-bluechip")]
    [InlineData("crypto")]
    [InlineData("tech")]
    [InlineData("etfs")]
    public void Validate_ValidGroupName_Succeeds(string group)
    {
        var result = _validator.Validate(new SeedSymbolsCommand(group));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonexistent")]
    [InlineData("invalid-group-name")]
    public void Validate_InvalidGroupName_Fails(string group)
    {
        var result = _validator.Validate(new SeedSymbolsCommand(group));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void NormaliseGroupName_KebabCase_ReturnsPascalCase()
    {
        SeedSymbolsCommandValidator.NormaliseGroupName("us-bluechip").Should().Be("UsBluechip");
        SeedSymbolsCommandValidator.NormaliseGroupName("asx-bluechip").Should().Be("AsxBluechip");
        SeedSymbolsCommandValidator.NormaliseGroupName("crypto").Should().Be("Crypto");
    }
}
