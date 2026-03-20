using MediatR;

namespace Application.Features.Watchlist.Commands.RemoveFromWatchlist;

public record RemoveFromWatchlistCommand(string Symbol) : IRequest;
