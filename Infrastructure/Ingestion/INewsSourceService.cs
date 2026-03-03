using Domain.ValueObjects;

namespace Infrastructure.Ingestion;

public record FetchedArticle(
    string Text,
    string? SourceUrl,
    DateTime PublishedAt);

/// <summary>
/// Abstraction over any news data source (RSS, NewsAPI, Finnhub, SEC EDGAR).
/// The ingestion worker calls this — swapping sources requires no changes to the worker.
/// </summary>
public interface INewsSourceService
{
    Task<IReadOnlyList<FetchedArticle>> FetchArticlesAsync(
        StockSymbol symbol,
        DateTime since,
        CancellationToken ct = default);
}
