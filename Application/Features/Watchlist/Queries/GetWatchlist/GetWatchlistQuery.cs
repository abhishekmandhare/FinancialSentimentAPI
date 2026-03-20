using MediatR;

namespace Application.Features.Watchlist.Queries.GetWatchlist;

public record GetWatchlistQuery : IRequest<IReadOnlyList<WatchlistSymbolDto>>;
