using Domain.Enums;
using FluentValidation;

namespace Application.Features.Symbols.Commands.SeedSymbols;

public class SeedSymbolsCommandValidator : AbstractValidator<SeedSymbolsCommand>
{
    public SeedSymbolsCommandValidator()
    {
        RuleFor(x => x.Group)
            .NotEmpty().WithMessage("Group name is required.")
            .Must(BeAValidGroup).WithMessage(
                $"Unknown group. Available groups: {string.Join(", ", Enum.GetNames<SymbolGroup>().Select(FormatGroupName))}");
    }

    private static bool BeAValidGroup(string group) =>
        Enum.TryParse<SymbolGroup>(NormaliseGroupName(group), ignoreCase: true, out _);

    /// <summary>
    /// Converts kebab-case group names (e.g. "us-bluechip") to PascalCase enum names (e.g. "UsBluechip").
    /// </summary>
    public static string NormaliseGroupName(string input) =>
        string.Concat(input.Split('-').Select(part =>
            part.Length == 0 ? "" : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));

    private static string FormatGroupName(string enumName)
    {
        // Convert PascalCase to kebab-case for display
        var chars = new List<char>();
        for (int i = 0; i < enumName.Length; i++)
        {
            if (i > 0 && char.IsUpper(enumName[i]))
                chars.Add('-');
            chars.Add(char.ToLowerInvariant(enumName[i]));
        }
        return new string(chars.ToArray());
    }
}
