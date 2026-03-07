using MediatR;

namespace Application.Features.Symbols.Commands.RemoveTrackedSymbol;

public record RemoveTrackedSymbolCommand(string Symbol) : IRequest;
