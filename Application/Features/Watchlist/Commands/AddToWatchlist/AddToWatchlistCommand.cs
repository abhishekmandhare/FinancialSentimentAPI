using MediatR;

namespace Application.Features.Watchlist.Commands.AddToWatchlist;

public record AddToWatchlistCommand(string Symbol) : IRequest<WatchlistSymbolDto>;
