using Application.Features.Sentiment.Commands.AnalyzeSentiment;
using Application.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ingestion;

/// <summary>
/// Background service (Sentiment Engine side of the pipeline):
///   - Consumes articles from IArticleQueue
///   - Dispatches AnalyzeSentimentCommand through the full MediatR pipeline
///     (validation → logging → handler → AI → persist → notify)
///   - Respects MaxConcurrentAnalyses to avoid overwhelming the AI rate limits
///
/// Separation from SentimentIngestionWorker means you can scale this independently:
///   - More analysis workers = more parallel AI calls
///   - Extracting to a separate service = swap IArticleQueue to GCP Pub/Sub
/// </summary>
public class SentimentAnalysisWorker(
    IServiceScopeFactory scopeFactory,
    IArticleQueue articleQueue,
    IOptionsMonitor<IngestionOptions> options,
    ILogger<SentimentAnalysisWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Sentiment analysis worker started.");

        var semaphore = new SemaphoreSlim(options.CurrentValue.MaxConcurrentAnalyses);
        var tasks     = new List<Task>();

        await foreach (var article in articleQueue.ConsumeAsync(stoppingToken))
        {
            await semaphore.WaitAsync(stoppingToken);

            var task = Task.Run(async () =>
            {
                try
                {
                    await AnalyzeAsync(article, stoppingToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }, stoppingToken);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }

    private async Task AnalyzeAsync(ArticleToAnalyze article, CancellationToken ct)
    {
        try
        {
            using var scope   = scopeFactory.CreateScope();
            var sender        = scope.ServiceProvider.GetRequiredService<ISender>();

            var response = await sender.Send(new AnalyzeSentimentCommand(
                article.Symbol,
                article.Text,
                article.SourceUrl), ct);

            logger.LogInformation(
                "Analyzed article for {Symbol} — {Label} ({Score:F2}) in {DurationMs}ms from {SourceUrl}",
                article.Symbol, response.Label, response.Score,
                response.DurationMs ?? 0, article.SourceUrl ?? "unknown source");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze article for {Symbol}", article.Symbol);
        }
    }
}
