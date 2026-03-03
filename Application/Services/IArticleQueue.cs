namespace Application.Services;

/// <summary>
/// Represents a news article waiting to be sentiment-analyzed.
/// </summary>
public record ArticleToAnalyze(
    string Symbol,
    string Text,
    string? SourceUrl,
    DateTime PublishedAt);

/// <summary>
/// Decouples the Ingestor (producer) from the Sentiment Analysis Worker (consumer).
///
/// Simple version: backed by System.Threading.Channels (in-process).
/// Future version: backed by GCP Pub/Sub or RabbitMQ — swap one DI registration.
/// This interface is the seam that makes microservice extraction mechanical.
/// </summary>
public interface IArticleQueue
{
    Task PublishAsync(ArticleToAnalyze article, CancellationToken ct = default);
    IAsyncEnumerable<ArticleToAnalyze> ConsumeAsync(CancellationToken ct);
}
