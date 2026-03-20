namespace Application.Services;

public interface ISymbolValidationService
{
    Task<bool> IsValidSymbolAsync(string symbol, CancellationToken ct = default);
}
