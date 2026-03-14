using Application.Services;

namespace Infrastructure.Ingestion;

/// <summary>
/// In-memory deduplication using a thread-safe HashSet.
/// Suitable for single-instance deployments.
///
/// For multi-instance / Kubernetes deployments, swap this registration
/// for a Redis- or database-backed implementation.
/// </summary>
public class InMemoryArticleDeduplicator : IArticleDeduplicator
{
    private readonly HashSet<string> _seen = [];
    private readonly Lock _lock = new();

    public Task<bool> IsSeenAsync(ArticleToAnalyze article, CancellationToken ct = default)
    {
        var key = GetKey(article);

        lock (_lock)
        {
            return Task.FromResult(_seen.Contains(key));
        }
    }

    public Task MarkSeenAsync(ArticleToAnalyze article, CancellationToken ct = default)
    {
        var key = GetKey(article);

        lock (_lock)
        {
            _seen.Add(key);
        }

        return Task.CompletedTask;
    }

    private static string GetKey(ArticleToAnalyze article)
        => article.SourceUrl ?? article.Text.GetHashCode().ToString();
}
