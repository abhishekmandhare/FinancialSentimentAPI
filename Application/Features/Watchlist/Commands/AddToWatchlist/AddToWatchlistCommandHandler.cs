using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using FluentValidation.Results;
using MediatR;

namespace Application.Features.Watchlist.Commands.AddToWatchlist;

public class AddToWatchlistCommandHandler(
    ITrackedSymbolRepository repository,
    ISymbolValidationService validationService)
    : IRequestHandler<AddToWatchlistCommand, WatchlistSymbolDto>
{
    public async Task<WatchlistSymbolDto> Handle(
        AddToWatchlistCommand request, CancellationToken cancellationToken)
    {
        var upper = request.Symbol.ToUpperInvariant();

        var existing = await repository.GetBySymbolAsync(upper, cancellationToken);

        if (existing is not null)
        {
            if (existing.Source == "watchlist")
                return new WatchlistSymbolDto(existing.Symbol, existing.AddedAt, 0, "Stable", 0, 0);

            // Symbol exists as "seed" — promote to "watchlist"
            existing.UpdateSource("watchlist");
            await repository.UpdateAsync(existing, cancellationToken);
            return new WatchlistSymbolDto(existing.Symbol, existing.AddedAt, 0, "Stable", 0, 0);
        }

        // New symbol — validate with Yahoo Finance
        var isValid = await validationService.IsValidSymbolAsync(upper, cancellationToken);
        if (!isValid)
            throw new Application.Exceptions.ValidationException(
                [new ValidationFailure("Symbol", $"Symbol '{upper}' is not a valid stock or crypto symbol.")]);

        var entity = TrackedSymbol.Create(upper, "watchlist");
        await repository.AddAsync(entity, cancellationToken);

        return new WatchlistSymbolDto(entity.Symbol, entity.AddedAt, 0, "Stable", 0, 0);
    }
}
