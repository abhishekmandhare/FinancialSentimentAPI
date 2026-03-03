using Application.Services;
using Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ingestion;

/// <summary>
/// Background service (Ingestor side of the pipeline):
///   - Polls tracked symbols on a configurable interval
///   - Fetches new articles from news sources since last fetch
///   - Deduplicates by URL hash
///   - Publishes new articles to IArticleQueue for the analysis worker
///
/// Separation from SentimentAnalysisWorker mirrors the design diagram:
///   Ingestor (I/O-bound, scales with sources) is independent from
///   Sentiment Engine (AI-bound, scales with concurrency).
/// </summary>
public class SentimentIngestionWorker(
    IServiceScopeFactory scopeFactory,
    IArticleQueue articleQueue,
    IOptionsMonitor<IngestionOptions> options,
    ILogger<SentimentIngestionWorker> logger)
    : BackgroundService
{
    // In-memory dedup store. For multi-instance deployments, move to Redis or DB.
    private readonly HashSet<string> _processedUrls = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Sentiment ingestion worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunIngestionCycleAsync(stoppingToken);

            var interval = TimeSpan.FromMinutes(options.CurrentValue.PollingIntervalMinutes);
            logger.LogInformation("Ingestion cycle complete. Next run in {Minutes} minutes.", interval.TotalMinutes);

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RunIngestionCycleAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var symbolsProvider  = scope.ServiceProvider.GetRequiredService<ITrackedSymbolsProvider>();
        var newsSourceService = scope.ServiceProvider.GetRequiredService<INewsSourceService>();

        var symbols = await symbolsProvider.GetActiveSymbolsAsync(ct);
        var since   = DateTime.UtcNow.AddMinutes(-options.CurrentValue.PollingIntervalMinutes * 2);

        foreach (var symbol in symbols)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var articles = await newsSourceService.FetchArticlesAsync(symbol, since, ct);
                var newCount = 0;

                foreach (var article in articles)
                {
                    var key = article.SourceUrl ?? article.Text.GetHashCode().ToString();

                    if (_processedUrls.Contains(key))
                        continue;

                    _processedUrls.Add(key);

                    await articleQueue.PublishAsync(
                        new ArticleToAnalyze(symbol.Value, article.Text, article.SourceUrl, article.PublishedAt),
                        ct);

                    newCount++;
                }

                if (newCount > 0)
                    logger.LogInformation("Queued {Count} new articles for {Symbol}", newCount, symbol.Value);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during ingestion cycle for {Symbol}", symbol.Value);
            }
        }
    }
}
