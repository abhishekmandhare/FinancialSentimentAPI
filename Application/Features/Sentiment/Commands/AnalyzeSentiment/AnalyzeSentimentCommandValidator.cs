using FluentValidation;

namespace Application.Features.Sentiment.Commands.AnalyzeSentiment;

/// <summary>
/// Validates input at the application boundary — before any domain or infrastructure work.
/// FluentValidation rules are readable, composable, and testable in isolation.
/// </summary>
public class AnalyzeSentimentCommandValidator : AbstractValidator<AnalyzeSentimentCommand>
{
    public AnalyzeSentimentCommandValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty().WithMessage("Symbol is required.")
            .MaximumLength(10).WithMessage("Symbol cannot exceed 10 characters.")
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("Symbol must contain only letters, digits, hyphens, or dots.");

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Text is required.")
            .MinimumLength(10).WithMessage("Text must be at least 10 characters.")
            .MaximumLength(5000).WithMessage("Text cannot exceed 5000 characters.");

        RuleFor(x => x.SourceUrl)
            .Must(url => url is null || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("SourceUrl must be a valid absolute URI.");
    }
}
