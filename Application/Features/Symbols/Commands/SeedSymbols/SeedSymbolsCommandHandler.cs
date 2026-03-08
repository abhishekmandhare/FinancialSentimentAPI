using Domain;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Symbols.Commands.SeedSymbols;

public class SeedSymbolsCommandHandler(ITrackedSymbolRepository repository)
    : IRequestHandler<SeedSymbolsCommand, SeedSymbolsResultDto>
{
    public async Task<SeedSymbolsResultDto> Handle(
        SeedSymbolsCommand request, CancellationToken cancellationToken)
    {
        var groupEnum = Enum.Parse<SymbolGroup>(
            SeedSymbolsCommandValidator.NormaliseGroupName(request.Group), ignoreCase: true);

        var symbols = SymbolGroupDefinitions.GetSymbols(groupEnum)!;

        var added = new List<string>();
        var skipped = 0;

        foreach (var symbol in symbols)
        {
            if (await repository.ExistsAsync(symbol, cancellationToken))
            {
                skipped++;
                continue;
            }

            var entity = TrackedSymbol.Create(symbol);
            await repository.AddAsync(entity, cancellationToken);
            added.Add(symbol);
        }

        return new SeedSymbolsResultDto(
            Group: request.Group,
            Added: added.Count,
            Skipped: skipped,
            AddedSymbols: added);
    }
}
