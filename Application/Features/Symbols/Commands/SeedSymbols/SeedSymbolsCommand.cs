using MediatR;

namespace Application.Features.Symbols.Commands.SeedSymbols;

public record SeedSymbolsCommand(string Group) : IRequest<SeedSymbolsResultDto>;
