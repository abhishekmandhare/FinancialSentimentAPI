using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Application.Services;

namespace Infrastructure.Ingestion;

/// <summary>
/// In-process implementation of IArticleQueue using System.Threading.Channels.
/// Channels are the idiomatic .NET producer/consumer primitive — built-in, no NuGet needed.
///
/// Future: swap this class for a GCP Pub/Sub or RabbitMQ implementation.
/// The interface seam means zero changes to Ingestor or Analysis Worker code.
/// </summary>
public class InMemoryArticleQueue : IArticleQueue
{
    private readonly Channel<ArticleToAnalyze> _channel =
        Channel.CreateUnbounded<ArticleToAnalyze>(
            new UnboundedChannelOptions { SingleReader = true });

    public async Task PublishAsync(ArticleToAnalyze article, CancellationToken ct = default)
        => await _channel.Writer.WriteAsync(article, ct);

    public async IAsyncEnumerable<ArticleToAnalyze> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var article in _channel.Reader.ReadAllAsync(ct))
            yield return article;
    }
}
