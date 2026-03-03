using Application.Common.Models;
using MediatR;

namespace Application.Features.Sentiment.Queries.GetSentimentHistory;

/// <summary>
/// Query: never changes state. Returns paginated sentiment history for a symbol.
/// Optional date filters allow time-boxed views of history.
/// </summary>
public record GetSentimentHistoryQuery(
    string Symbol,
    int Page = 1,
    int PageSize = 20,
    DateTime? From = null,
    DateTime? To = null) : IRequest<PagedResult<SentimentHistoryDto>>;
