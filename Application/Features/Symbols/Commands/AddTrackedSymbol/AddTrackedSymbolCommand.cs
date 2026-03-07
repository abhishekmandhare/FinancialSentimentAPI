using MediatR;

namespace Application.Features.Symbols.Commands.AddTrackedSymbol;

public record AddTrackedSymbolCommand(string Symbol) : IRequest<TrackedSymbolDto>;
