using Domain.Interfaces;
using MediatR;

namespace Application.Features.Symbols.Queries.GetTrackedSymbols;

public class GetTrackedSymbolsQueryHandler(ITrackedSymbolRepository repository)
    : IRequestHandler<GetTrackedSymbolsQuery, IReadOnlyList<TrackedSymbolDto>>
{
    public async Task<IReadOnlyList<TrackedSymbolDto>> Handle(
        GetTrackedSymbolsQuery request, CancellationToken cancellationToken)
    {
        var symbols = await repository.GetAllAsync(cancellationToken);
        return symbols
            .Select(s => new TrackedSymbolDto(s.Symbol, s.AddedAt))
            .ToList();
    }
}
