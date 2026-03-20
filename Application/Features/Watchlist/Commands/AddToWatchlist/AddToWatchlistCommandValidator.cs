using FluentValidation;

namespace Application.Features.Watchlist.Commands.AddToWatchlist;

public class AddToWatchlistCommandValidator : AbstractValidator<AddToWatchlistCommand>
{
    public AddToWatchlistCommandValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty().WithMessage("Symbol is required.")
            .MaximumLength(10).WithMessage("Symbol cannot exceed 10 characters.")
            .Matches(@"^[A-Za-z0-9\-\.]+$").WithMessage("Symbol may only contain letters, digits, hyphens, and dots.");
    }
}
