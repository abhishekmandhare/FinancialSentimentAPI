using MediatR;

namespace Application.Features.Symbols.Queries.GetTrackedSymbols;

public record GetTrackedSymbolsQuery : IRequest<IReadOnlyList<TrackedSymbolDto>>;
