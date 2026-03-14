namespace Application.Services;

/// <summary>
/// Prevents duplicate articles from being queued for analysis.
///
/// Simple version: backed by an in-memory HashSet (single instance).
/// Future version: backed by Redis or database — swap one DI registration.
/// This interface is the seam that makes stateless deployments (k8s) mechanical.
/// </summary>
public interface IArticleDeduplicator
{
    /// <summary>
    /// Returns true if the article has already been processed.
    /// </summary>
    Task<bool> IsSeenAsync(ArticleToAnalyze article, CancellationToken ct = default);

    /// <summary>
    /// Records an article as processed so future calls to IsSeenAsync return true.
    /// </summary>
    Task MarkSeenAsync(ArticleToAnalyze article, CancellationToken ct = default);
}
