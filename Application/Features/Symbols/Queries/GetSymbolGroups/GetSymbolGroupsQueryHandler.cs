using Domain;
using Domain.Enums;
using MediatR;

namespace Application.Features.Symbols.Queries.GetSymbolGroups;

public class GetSymbolGroupsQueryHandler
    : IRequestHandler<GetSymbolGroupsQuery, IReadOnlyList<SymbolGroupDto>>
{
    public Task<IReadOnlyList<SymbolGroupDto>> Handle(
        GetSymbolGroupsQuery request, CancellationToken cancellationToken)
    {
        var groups = SymbolGroupDefinitions.AvailableGroups
            .Select(g => new SymbolGroupDto(
                Name: FormatGroupName(g),
                Symbols: SymbolGroupDefinitions.GetSymbols(g)!))
            .ToList();

        return Task.FromResult<IReadOnlyList<SymbolGroupDto>>(groups);
    }

    private static string FormatGroupName(SymbolGroup group)
    {
        var name = group.ToString();
        var chars = new List<char>();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
                chars.Add('-');
            chars.Add(char.ToLowerInvariant(name[i]));
        }
        return new string(chars.ToArray());
    }
}
