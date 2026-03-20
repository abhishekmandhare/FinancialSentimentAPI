namespace Application.Features.Sentiment.Queries.GetTrendingSymbols;

public record TrendingSymbolDto(
    string Symbol,
    double CurrentAvgScore,
    double PreviousAvgScore,
    double Delta,
    string Direction,
    string Trend,
    double Dispersion,
    int ArticleCount);
