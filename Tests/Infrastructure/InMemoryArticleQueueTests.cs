using Application.Services;
using FluentAssertions;
using Infrastructure.Ingestion;

namespace Tests.Infrastructure;

public class InMemoryArticleQueueTests
{
    [Fact]
    public async Task PublishAndConsume_SingleArticle_ReceivesArticle()
    {
        var queue = new InMemoryArticleQueue();
        var article = new ArticleToAnalyze("AAPL", "Apple beat earnings.", "https://example.com", DateTime.UtcNow);

        await queue.PublishAsync(article);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = new List<ArticleToAnalyze>();

        await foreach (var item in queue.ConsumeAsync(cts.Token))
        {
            received.Add(item);
            break; // Only expect one
        }

        received.Should().HaveCount(1);
        received[0].Symbol.Should().Be("AAPL");
        received[0].Text.Should().Be("Apple beat earnings.");
    }

    [Fact]
    public async Task PublishAndConsume_MultipleArticles_ReceivesAll()
    {
        var queue = new InMemoryArticleQueue();
        var articles = new[]
        {
            new ArticleToAnalyze("AAPL", "Article 1", null, DateTime.UtcNow),
            new ArticleToAnalyze("MSFT", "Article 2", null, DateTime.UtcNow),
            new ArticleToAnalyze("GOOGL", "Article 3", null, DateTime.UtcNow),
        };

        foreach (var article in articles)
            await queue.PublishAsync(article);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = new List<ArticleToAnalyze>();

        await foreach (var item in queue.ConsumeAsync(cts.Token))
        {
            received.Add(item);
            if (received.Count == 3) break;
        }

        received.Should().HaveCount(3);
        received.Select(a => a.Symbol).Should().ContainInOrder("AAPL", "MSFT", "GOOGL");
    }

    [Fact]
    public async Task Consume_Cancellation_StopsConsuming()
    {
        var queue = new InMemoryArticleQueue();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var received = new List<ArticleToAnalyze>();
        var act = async () =>
        {
            await foreach (var item in queue.ConsumeAsync(cts.Token))
                received.Add(item);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        received.Should().BeEmpty();
    }
}
