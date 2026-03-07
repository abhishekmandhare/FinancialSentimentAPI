using Domain.ValueObjects;
using FluentAssertions;
using Infrastructure.Ingestion;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Tests.Infrastructure;

public class CompositeNewsSourceServiceTests
{
    private static readonly StockSymbol Aapl = StockSymbol.Create("AAPL");
    private static readonly DateTime Since = DateTime.UtcNow.AddHours(-1);

    private static CompositeNewsSourceService BuildSut(params INewsSourceService[] sources) =>
        new(sources, Substitute.For<ILogger<CompositeNewsSourceService>>());

    // ── FetchArticlesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task FetchArticlesAsync_MergesArticlesFromAllSources()
    {
        var article1 = new FetchedArticle("Yahoo headline", "https://yahoo.example/1", DateTime.UtcNow);
        var article2 = new FetchedArticle("Google headline", "https://google.example/2", DateTime.UtcNow);
        var article3 = new FetchedArticle("Reddit headline", "https://reddit.example/3", DateTime.UtcNow);

        var source1 = Substitute.For<INewsSourceService>();
        var source2 = Substitute.For<INewsSourceService>();
        var source3 = Substitute.For<INewsSourceService>();

        source1.FetchArticlesAsync(Aapl, Since, Arg.Any<CancellationToken>())
            .Returns([article1]);
        source2.FetchArticlesAsync(Aapl, Since, Arg.Any<CancellationToken>())
            .Returns([article2]);
        source3.FetchArticlesAsync(Aapl, Since, Arg.Any<CancellationToken>())
            .Returns([article3]);

        var sut = BuildSut(source1, source2, source3);

        var result = await sut.FetchArticlesAsync(Aapl, Since);

        result.Should().HaveCount(3)
            .And.Contain(article1)
            .And.Contain(article2)
            .And.Contain(article3);
    }

    [Fact]
    public async Task FetchArticlesAsync_WhenOneSourceFails_ReturnsArticlesFromOtherSources()
    {
        var article = new FetchedArticle("Good headline", "https://good.example/1", DateTime.UtcNow);

        var goodSource = Substitute.For<INewsSourceService>();
        var badSource  = Substitute.For<INewsSourceService>();

        goodSource.FetchArticlesAsync(Aapl, Since, Arg.Any<CancellationToken>())
            .Returns([article]);
        badSource.FetchArticlesAsync(Aapl, Since, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("network error"));

        var sut = BuildSut(goodSource, badSource);

        var result = await sut.FetchArticlesAsync(Aapl, Since);

        result.Should().ContainSingle()
            .Which.Should().Be(article);
    }

    [Fact]
    public async Task FetchArticlesAsync_WhenAllSourcesFail_ReturnsEmpty()
    {
        var bad1 = Substitute.For<INewsSourceService>();
        var bad2 = Substitute.For<INewsSourceService>();

        bad1.FetchArticlesAsync(Aapl, Since, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("network error"));
        bad2.FetchArticlesAsync(Aapl, Since, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("timed out"));

        var sut = BuildSut(bad1, bad2);

        var result = await sut.FetchArticlesAsync(Aapl, Since);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchArticlesAsync_WithNoSources_ReturnsEmpty()
    {
        var sut = BuildSut();

        var result = await sut.FetchArticlesAsync(Aapl, Since);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchArticlesAsync_CallsAllSourcesWithCorrectArguments()
    {
        var source1 = Substitute.For<INewsSourceService>();
        var source2 = Substitute.For<INewsSourceService>();

        source1.FetchArticlesAsync(Arg.Any<StockSymbol>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([]);
        source2.FetchArticlesAsync(Arg.Any<StockSymbol>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var sut = BuildSut(source1, source2);
        using var cts = new CancellationTokenSource();

        await sut.FetchArticlesAsync(Aapl, Since, cts.Token);

        await source1.Received(1).FetchArticlesAsync(Aapl, Since, cts.Token);
        await source2.Received(1).FetchArticlesAsync(Aapl, Since, cts.Token);
    }

    [Fact]
    public async Task FetchArticlesAsync_WhenSourceReturnsEmpty_DoesNotAddArticles()
    {
        var article = new FetchedArticle("Only headline", "https://example.com/1", DateTime.UtcNow);

        var source1 = Substitute.For<INewsSourceService>();
        var source2 = Substitute.For<INewsSourceService>();

        source1.FetchArticlesAsync(Aapl, Since, Arg.Any<CancellationToken>())
            .Returns([article]);
        source2.FetchArticlesAsync(Aapl, Since, Arg.Any<CancellationToken>())
            .Returns([]);

        var sut = BuildSut(source1, source2);

        var result = await sut.FetchArticlesAsync(Aapl, Since);

        result.Should().ContainSingle()
            .Which.Should().Be(article);
    }
}
