using MediatR;

namespace Application.Features.Sentiment.Queries.GetTrendingSymbols;

public record GetTrendingSymbolsQuery(int Hours = 24, int Limit = 10)
    : IRequest<IReadOnlyList<TrendingSymbolDto>>;
