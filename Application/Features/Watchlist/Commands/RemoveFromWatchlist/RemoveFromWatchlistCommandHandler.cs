using Application.Exceptions;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Watchlist.Commands.RemoveFromWatchlist;

public class RemoveFromWatchlistCommandHandler(ITrackedSymbolRepository repository)
    : IRequestHandler<RemoveFromWatchlistCommand>
{
    public async Task Handle(
        RemoveFromWatchlistCommand request, CancellationToken cancellationToken)
    {
        var upper = request.Symbol.ToUpperInvariant();
        var existing = await repository.GetBySymbolAsync(upper, cancellationToken);

        if (existing is null || existing.Source != "watchlist")
            throw new NotFoundException("Watchlist symbol", upper);

        await repository.RemoveAsync(upper, cancellationToken);
    }
}
