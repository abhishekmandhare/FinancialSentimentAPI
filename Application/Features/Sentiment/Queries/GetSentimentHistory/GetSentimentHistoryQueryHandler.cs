using Application.Common.Models;
using Domain.Interfaces;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Sentiment.Queries.GetSentimentHistory;

public class GetSentimentHistoryQueryHandler(ISentimentRepository repository)
    : IRequestHandler<GetSentimentHistoryQuery, PagedResult<SentimentHistoryDto>>
{
    public async Task<PagedResult<SentimentHistoryDto>> Handle(
        GetSentimentHistoryQuery query,
        CancellationToken ct)
    {
        var symbol = new StockSymbol(query.Symbol);

        var (items, totalCount) = await repository.GetHistoryAsync(
            symbol,
            query.Page,
            query.PageSize,
            query.From,
            query.To,
            ct);

        var dtos = items.Select(a => new SentimentHistoryDto(
            a.Id,
            a.Score.Value,
            a.Label.ToString(),
            a.Confidence,
            a.KeyReasons,
            a.SourceUrl,
            a.ModelVersion,
            a.AnalyzedAt)).ToList();

        return new PagedResult<SentimentHistoryDto>(dtos, totalCount, query.Page, query.PageSize);
    }
}
