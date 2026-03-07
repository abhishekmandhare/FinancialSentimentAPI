using Application.Features.Symbols;
using Application.Features.Symbols.Commands.AddTrackedSymbol;
using Application.Features.Symbols.Commands.RemoveTrackedSymbol;
using Application.Features.Symbols.Queries.GetTrackedSymbols;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Application.Exceptions;

namespace Tests.Application;

public class GetTrackedSymbolsHandlerTests
{
    private readonly ITrackedSymbolRepository _repository = Substitute.For<ITrackedSymbolRepository>();

    [Fact]
    public async Task Handle_ReturnsAllSymbolsMappedToDtos()
    {
        var symbols = new List<TrackedSymbol>
        {
            TrackedSymbol.Create("AAPL"),
            TrackedSymbol.Create("MSFT")
        };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(symbols);

        var handler = new GetTrackedSymbolsQueryHandler(_repository);
        var result = await handler.Handle(new GetTrackedSymbolsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(r => r.Symbol).Should().BeEquivalentTo(["AAPL", "MSFT"]);
    }

    [Fact]
    public async Task Handle_WhenNoSymbols_ReturnsEmptyList()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<TrackedSymbol>());

        var handler = new GetTrackedSymbolsQueryHandler(_repository);
        var result = await handler.Handle(new GetTrackedSymbolsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}

public class AddTrackedSymbolHandlerTests
{
    private readonly ITrackedSymbolRepository _repository = Substitute.For<ITrackedSymbolRepository>();

    private AddTrackedSymbolCommandHandler CreateHandler() => new(_repository);

    [Fact]
    public async Task Handle_NewSymbol_AddsAndReturnsDto()
    {
        _repository.ExistsAsync("NVDA", Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(
            new AddTrackedSymbolCommand("nvda"), CancellationToken.None);

        result.Symbol.Should().Be("NVDA");
        result.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        await _repository.Received(1).AddAsync(Arg.Any<TrackedSymbol>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewSymbol_UppercasesSymbol()
    {
        _repository.ExistsAsync("TSLA", Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(
            new AddTrackedSymbolCommand("tsla"), CancellationToken.None);

        result.Symbol.Should().Be("TSLA");
    }

    [Fact]
    public async Task Handle_DuplicateSymbol_ThrowsDomainException()
    {
        _repository.ExistsAsync("AAPL", Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await CreateHandler().Handle(
            new AddTrackedSymbolCommand("AAPL"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*AAPL*already being tracked*");
    }

    [Fact]
    public async Task Handle_DuplicateSymbol_DoesNotCallAdd()
    {
        _repository.ExistsAsync("AAPL", Arg.Any<CancellationToken>()).Returns(true);

        try
        {
            await CreateHandler().Handle(new AddTrackedSymbolCommand("AAPL"), CancellationToken.None);
        }
        catch (DomainException) { }

        await _repository.DidNotReceive().AddAsync(Arg.Any<TrackedSymbol>(), Arg.Any<CancellationToken>());
    }
}

public class RemoveTrackedSymbolHandlerTests
{
    private readonly ITrackedSymbolRepository _repository = Substitute.For<ITrackedSymbolRepository>();

    private RemoveTrackedSymbolCommandHandler CreateHandler() => new(_repository);

    [Fact]
    public async Task Handle_ExistingSymbol_RemovesSuccessfully()
    {
        _repository.RemoveAsync("AAPL", Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await CreateHandler().Handle(
            new RemoveTrackedSymbolCommand("AAPL"), CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _repository.Received(1).RemoveAsync("AAPL", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingSymbol_UppercasesBeforeRemoving()
    {
        _repository.RemoveAsync("MSFT", Arg.Any<CancellationToken>()).Returns(true);

        await CreateHandler().Handle(
            new RemoveTrackedSymbolCommand("msft"), CancellationToken.None);

        await _repository.Received(1).RemoveAsync("MSFT", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonExistentSymbol_ThrowsNotFoundException()
    {
        _repository.RemoveAsync("UNKNOWN", Arg.Any<CancellationToken>()).Returns(false);

        var act = async () => await CreateHandler().Handle(
            new RemoveTrackedSymbolCommand("UNKNOWN"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
