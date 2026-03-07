using Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ingestion;

/// <summary>
/// Fans out FetchArticlesAsync to multiple INewsSourceService implementations in parallel,
/// merges results, and swallows per-source failures so a single bad source does not
/// block the rest of the pipeline.
///
/// Deduplication by URL is handled downstream in SentimentIngestionWorker.
/// </summary>
public class CompositeNewsSourceService(
    IEnumerable<INewsSourceService> sources,
    ILogger<CompositeNewsSourceService> logger)
    : INewsSourceService
{
    private readonly IReadOnlyList<INewsSourceService> _sources = sources.ToList();

    public async Task<IReadOnlyList<FetchedArticle>> FetchArticlesAsync(
        StockSymbol symbol,
        DateTime since,
        CancellationToken ct = default)
    {
        var tasks = _sources.Select(source => FetchSafeAsync(source, symbol, since, ct));
        var results = await Task.WhenAll(tasks);

        return results.SelectMany(r => r).ToList();
    }

    private async Task<IReadOnlyList<FetchedArticle>> FetchSafeAsync(
        INewsSourceService source,
        StockSymbol symbol,
        DateTime since,
        CancellationToken ct)
    {
        try
        {
            return await source.FetchArticlesAsync(symbol, since, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Propagate cooperative cancellation so the overall operation can be cancelled.
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "News source {Source} failed for {Symbol} — skipping",
                source.GetType().Name, symbol.Value);
            return [];
        }
    }
}
