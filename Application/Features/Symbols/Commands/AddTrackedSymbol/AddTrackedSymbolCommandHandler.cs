using Domain.Entities;
using Domain.Exceptions;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Symbols.Commands.AddTrackedSymbol;

public class AddTrackedSymbolCommandHandler(ITrackedSymbolRepository repository)
    : IRequestHandler<AddTrackedSymbolCommand, TrackedSymbolDto>
{
    public async Task<TrackedSymbolDto> Handle(
        AddTrackedSymbolCommand request, CancellationToken cancellationToken)
    {
        var upper = request.Symbol.ToUpperInvariant();

        if (await repository.ExistsAsync(upper, cancellationToken))
            throw new DomainException($"Symbol '{upper}' is already being tracked.");

        var entity = TrackedSymbol.Create(upper);
        await repository.AddAsync(entity, cancellationToken);

        return new TrackedSymbolDto(entity.Symbol, entity.AddedAt);
    }
}
