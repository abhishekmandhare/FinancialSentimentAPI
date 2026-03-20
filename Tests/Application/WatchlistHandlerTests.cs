using Application.Configuration;
using Application.Exceptions;
using Application.Features.Watchlist.Commands.AddToWatchlist;
using Application.Features.Watchlist.Commands.RemoveFromWatchlist;
using Application.Features.Watchlist.Queries.GetWatchlist;
using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using FluentAssertions;
// FluentValidation used via full qualifier in assertions
using NSubstitute;

namespace Tests.Application;

public class AddToWatchlistHandlerTests
{
    private readonly ITrackedSymbolRepository _repository = Substitute.For<ITrackedSymbolRepository>();
    private readonly ISymbolValidationService _validation = Substitute.For<ISymbolValidationService>();

    private AddToWatchlistCommandHandler CreateHandler() => new(_repository, _validation);

    [Fact]
    public async Task Handle_NewValidSymbol_AddsWithWatchlistSource()
    {
        _repository.GetBySymbolAsync("GOOG", Arg.Any<CancellationToken>()).Returns((TrackedSymbol?)null);
        _validation.IsValidSymbolAsync("GOOG", Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(
            new AddToWatchlistCommand("goog"), CancellationToken.None);

        result.Symbol.Should().Be("GOOG");
        await _repository.Received(1).AddAsync(
            Arg.Is<TrackedSymbol>(s => s.Symbol == "GOOG" && s.Source == "watchlist"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidSymbol_ThrowsValidationException()
    {
        _repository.GetBySymbolAsync("FAKE", Arg.Any<CancellationToken>()).Returns((TrackedSymbol?)null);
        _validation.IsValidSymbolAsync("FAKE", Arg.Any<CancellationToken>()).Returns(false);

        var act = () => CreateHandler().Handle(
            new AddToWatchlistCommand("FAKE"), CancellationToken.None);

        await act.Should().ThrowAsync<global::Application.Exceptions.ValidationException>();
    }

    [Fact]
    public async Task Handle_ExistingSeedSymbol_PromotesToWatchlist()
    {
        var existing = TrackedSymbol.Create("AAPL", "seed");
        _repository.GetBySymbolAsync("AAPL", Arg.Any<CancellationToken>()).Returns(existing);

        var result = await CreateHandler().Handle(
            new AddToWatchlistCommand("AAPL"), CancellationToken.None);

        result.Symbol.Should().Be("AAPL");
        existing.Source.Should().Be("watchlist");
        await _repository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingWatchlistSymbol_ReturnsWithoutModifying()
    {
        var existing = TrackedSymbol.Create("TSLA", "watchlist");
        _repository.GetBySymbolAsync("TSLA", Arg.Any<CancellationToken>()).Returns(existing);

        var result = await CreateHandler().Handle(
            new AddToWatchlistCommand("TSLA"), CancellationToken.None);

        result.Symbol.Should().Be("TSLA");
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<TrackedSymbol>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<TrackedSymbol>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewSymbol_DoesNotCallValidationForExisting()
    {
        var existing = TrackedSymbol.Create("AAPL", "seed");
        _repository.GetBySymbolAsync("AAPL", Arg.Any<CancellationToken>()).Returns(existing);

        await CreateHandler().Handle(new AddToWatchlistCommand("AAPL"), CancellationToken.None);

        await _validation.DidNotReceive().IsValidSymbolAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

public class RemoveFromWatchlistHandlerTests
{
    private readonly ITrackedSymbolRepository _repository = Substitute.For<ITrackedSymbolRepository>();

    private RemoveFromWatchlistCommandHandler CreateHandler() => new(_repository);

    [Fact]
    public async Task Handle_WatchlistSymbol_RemovesSuccessfully()
    {
        var existing = TrackedSymbol.Create("GOOG", "watchlist");
        _repository.GetBySymbolAsync("GOOG", Arg.Any<CancellationToken>()).Returns(existing);
        _repository.RemoveAsync("GOOG", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateHandler().Handle(
            new RemoveFromWatchlistCommand("goog"), CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _repository.Received(1).RemoveAsync("GOOG", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SeedSymbol_ThrowsNotFoundException()
    {
        var existing = TrackedSymbol.Create("AAPL", "seed");
        _repository.GetBySymbolAsync("AAPL", Arg.Any<CancellationToken>()).Returns(existing);

        var act = () => CreateHandler().Handle(
            new RemoveFromWatchlistCommand("AAPL"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NonExistentSymbol_ThrowsNotFoundException()
    {
        _repository.GetBySymbolAsync("XXXX", Arg.Any<CancellationToken>()).Returns((TrackedSymbol?)null);

        var act = () => CreateHandler().Handle(
            new RemoveFromWatchlistCommand("XXXX"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class GetWatchlistHandlerTests
{
    private readonly ITrackedSymbolRepository _trackedRepo = Substitute.For<ITrackedSymbolRepository>();
    private readonly ISentimentRepository _sentimentRepo = Substitute.For<ISentimentRepository>();
    private readonly ISymbolSnapshotRepository _snapshotRepo = Substitute.For<ISymbolSnapshotRepository>();

    private GetWatchlistQueryHandler CreateHandler() => new(_trackedRepo, _sentimentRepo, _snapshotRepo, new SentimentScoringOptions());

    [Fact]
    public async Task Handle_NoWatchlistSymbols_ReturnsEmpty()
    {
        _trackedRepo.GetBySourceAsync("watchlist", Arg.Any<CancellationToken>())
            .Returns(new List<TrackedSymbol>());

        var result = await CreateHandler().Handle(new GetWatchlistQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithSymbols_ReturnsEnrichedDtos()
    {
        var symbols = new List<TrackedSymbol>
        {
            TrackedSymbol.Create("GOOG", "watchlist"),
            TrackedSymbol.Create("TSLA", "watchlist")
        };
        _trackedRepo.GetBySourceAsync("watchlist", Arg.Any<CancellationToken>()).Returns(symbols);

        // GOOG has sentiment data
        var googAnalyses = new List<SentimentAnalysis>
        {
            SentimentAnalysis.Create(new StockSymbol("GOOG"), "Good news", null, 0.8, 0.9, [], "test")
        };
        _sentimentRepo.GetForStatsAsync(
            Arg.Is<StockSymbol>(s => s.Value == "GOOG"), 7, Arg.Any<CancellationToken>())
            .Returns(googAnalyses.AsReadOnly() as IReadOnlyList<SentimentAnalysis>);

        // TSLA has no sentiment data
        _sentimentRepo.GetForStatsAsync(
            Arg.Is<StockSymbol>(s => s.Value == "TSLA"), 7, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SentimentAnalysis>() as IReadOnlyList<SentimentAnalysis>);

        var result = await CreateHandler().Handle(new GetWatchlistQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Symbol.Should().Be("GOOG");
        result[0].Score.Should().BeApproximately(0.8, 0.01);
        result[0].ArticleCount.Should().Be(1);
        result[1].Symbol.Should().Be("TSLA");
        result[1].Score.Should().Be(0);
        result[1].ArticleCount.Should().Be(0);
    }
}
