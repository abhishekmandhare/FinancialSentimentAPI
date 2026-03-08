namespace Application.Features.Symbols;

/// <summary>
/// Result of a bulk seed operation — tells the caller what was added vs skipped.
/// </summary>
public record SeedSymbolsResultDto(
    string Group,
    int Added,
    int Skipped,
    IReadOnlyList<string> AddedSymbols);
