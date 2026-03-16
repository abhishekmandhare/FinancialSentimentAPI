using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Infrastructure.Monitoring;

/// <summary>
/// Central registry for custom application metrics exposed via OpenTelemetry.
/// All background workers and services record metrics through this singleton.
/// </summary>
public sealed class AppMetrics
{
    public const string ServiceName = "FinancialSentimentAPI";

    // ActivitySource for custom tracing spans
    public static readonly ActivitySource ActivitySource = new(ServiceName);

    // Meter for custom metrics
    private static readonly Meter Meter = new(ServiceName);

    // ── Ingestion metrics ────────────────────────────────────────────────────

    public static readonly Counter<long> ArticlesFetched =
        Meter.CreateCounter<long>("ingestion.articles.fetched", "articles",
            "Total articles fetched from news sources");

    public static readonly Counter<long> ArticlesFiltered =
        Meter.CreateCounter<long>("ingestion.articles.filtered", "articles",
            "Articles filtered as irrelevant");

    public static readonly Counter<long> ArticlesQueued =
        Meter.CreateCounter<long>("ingestion.articles.queued", "articles",
            "New articles queued for analysis");

    public static readonly Counter<long> DedupHits =
        Meter.CreateCounter<long>("ingestion.dedup.hits", "articles",
            "Articles skipped by deduplication");

    public static readonly Histogram<double> IngestionCycleDuration =
        Meter.CreateHistogram<double>("ingestion.cycle.duration", "s",
            "Duration of a full ingestion cycle");

    public static readonly Counter<long> IngestionErrors =
        Meter.CreateCounter<long>("ingestion.errors", "errors",
            "Errors during ingestion per symbol");

    // ── Analysis metrics ─────────────────────────────────────────────────────

    public static readonly Histogram<double> AnalysisDuration =
        Meter.CreateHistogram<double>("analysis.duration", "s",
            "Duration of a single AI sentiment analysis");

    public static readonly Counter<long> AnalysisTotal =
        Meter.CreateCounter<long>("analysis.total", "analyses",
            "Total analyses by result (success, failure, fallback)");

    // ── AI / Ollama metrics ──────────────────────────────────────────────────

    public static readonly Counter<long> OllamaParseFailures =
        Meter.CreateCounter<long>("ollama.parse.failures", "failures",
            "Ollama responses that failed JSON parsing");

    public static readonly Histogram<double> OllamaRequestDuration =
        Meter.CreateHistogram<double>("ollama.request.duration", "s",
            "Duration of Ollama HTTP requests");

    // ── News source metrics ──────────────────────────────────────────────────

    public static readonly Counter<long> RedditRateLimits =
        Meter.CreateCounter<long>("reddit.rate_limits", "events",
            "Reddit 429 Too Many Requests responses");

    public static readonly Counter<long> NewsSourceErrors =
        Meter.CreateCounter<long>("news_source.errors", "errors",
            "Errors fetching from news sources");
}
