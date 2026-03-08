using MediatR;

namespace Application.Features.Symbols.Queries.GetSymbolGroups;

public record GetSymbolGroupsQuery : IRequest<IReadOnlyList<SymbolGroupDto>>;
