using Application.Exceptions;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Symbols.Commands.RemoveTrackedSymbol;

public class RemoveTrackedSymbolCommandHandler(ITrackedSymbolRepository repository)
    : IRequestHandler<RemoveTrackedSymbolCommand>
{
    public async Task Handle(RemoveTrackedSymbolCommand request, CancellationToken cancellationToken)
    {
        var upper = request.Symbol.ToUpperInvariant();
        var removed = await repository.RemoveAsync(upper, cancellationToken);

        if (!removed)
            throw new NotFoundException("TrackedSymbol", upper);
    }
}
