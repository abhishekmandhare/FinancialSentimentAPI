namespace Application.Features.Watchlist;

public record WatchlistSymbolDto(
    string Symbol,
    DateTime AddedAt,
    double Score,
    string Trend,
    double Dispersion,
    int ArticleCount);
