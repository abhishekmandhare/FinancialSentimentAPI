using System.Net;
using System.Text;
using Domain.ValueObjects;
using FluentAssertions;
using Infrastructure.Ingestion;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Tests.Infrastructure;

public class RedditNewsSourceServiceTests
{
    private static readonly DateTime Since = DateTime.UtcNow.AddHours(-1);

    private static string BuildAtomFeed(params (string title, string url, DateTime updated)[] entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<feed xmlns="http://www.w3.org/2005/Atom">""");
        foreach (var (title, url, updated) in entries)
        {
            sb.AppendLine("<entry>");
            sb.AppendLine($"  <title>{title}</title>");
            sb.AppendLine($"""  <link href="{url}" />""");
            sb.AppendLine($"  <updated>{updated:O}</updated>");
            sb.AppendLine("</entry>");
        }
        sb.AppendLine("</feed>");
        return sb.ToString();
    }

    private static RedditNewsSourceService BuildSut(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var messageHandler = new FakeHttpMessageHandler(handler);
        var httpClient = new HttpClient(messageHandler);
        var logger = Substitute.For<ILogger<RedditNewsSourceService>>();
        return new RedditNewsSourceService(httpClient, logger);
    }

    // ── Crypto symbol searches both subreddits ───────────────────────────

    [Fact]
    public async Task FetchArticlesAsync_CryptoSymbol_SearchesBothStocksAndCryptocurrency()
    {
        var btc = new StockSymbol("BTC-USD");
        var requestedUrls = new List<string>();

        var feed = BuildAtomFeed(("Bitcoin rises", "https://reddit.com/r/test/1", DateTime.UtcNow));

        var sut = BuildSut(req =>
        {
            requestedUrls.Add(req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed, Encoding.UTF8, "application/xml")
            };
        });

        await sut.FetchArticlesAsync(btc, Since);

        requestedUrls.Should().HaveCount(2);
        requestedUrls.Should().Contain(u => u.Contains("/r/stocks/"));
        requestedUrls.Should().Contain(u => u.Contains("/r/cryptocurrency/"));
    }

    [Fact]
    public async Task FetchArticlesAsync_RegularSymbol_SearchesOnlyStocks()
    {
        var aapl = new StockSymbol("AAPL");
        var requestedUrls = new List<string>();

        var feed = BuildAtomFeed(("Apple earnings", "https://reddit.com/r/test/1", DateTime.UtcNow));

        var sut = BuildSut(req =>
        {
            requestedUrls.Add(req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed, Encoding.UTF8, "application/xml")
            };
        });

        await sut.FetchArticlesAsync(aapl, Since);

        requestedUrls.Should().ContainSingle();
        requestedUrls[0].Should().Contain("/r/stocks/");
    }

    [Fact]
    public async Task FetchArticlesAsync_CryptoSymbol_DeduplicatesByUrl()
    {
        var eth = new StockSymbol("ETH-USD");
        var sharedUrl = "https://reddit.com/r/shared/1";

        var feed = BuildAtomFeed(("Ethereum news", sharedUrl, DateTime.UtcNow));

        var sut = BuildSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(feed, Encoding.UTF8, "application/xml")
        });

        var result = await sut.FetchArticlesAsync(eth, Since);

        // Both subreddits return the same article URL — should be deduplicated to 1
        result.Should().ContainSingle();
    }

    [Fact]
    public async Task FetchArticlesAsync_CryptoSymbol_CombinesUniqueArticles()
    {
        var btc = new StockSymbol("BTC-USD");
        var callCount = 0;

        var feed1 = BuildAtomFeed(("Bitcoin from stocks", "https://reddit.com/r/stocks/1", DateTime.UtcNow));
        var feed2 = BuildAtomFeed(("Bitcoin from crypto", "https://reddit.com/r/crypto/2", DateTime.UtcNow));

        var sut = BuildSut(req =>
        {
            var feed = Interlocked.Increment(ref callCount) == 1 ? feed1 : feed2;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed, Encoding.UTF8, "application/xml")
            };
        });

        var result = await sut.FetchArticlesAsync(btc, Since);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task FetchArticlesAsync_CryptoSymbol_WhenOneSubredditFails_ReturnsOther()
    {
        var btc = new StockSymbol("BTC-USD");
        var callCount = 0;

        var feed = BuildAtomFeed(("Bitcoin news", "https://reddit.com/r/stocks/1", DateTime.UtcNow));

        var sut = BuildSut(req =>
        {
            if (Interlocked.Increment(ref callCount) == 2)
                throw new HttpRequestException("network error");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed, Encoding.UTF8, "application/xml")
            };
        });

        var result = await sut.FetchArticlesAsync(btc, Since);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task FetchArticlesAsync_AllSubredditsKeepRestrictSrOn()
    {
        var btc = new StockSymbol("BTC-USD");
        var requestedUrls = new List<string>();

        var feed = BuildAtomFeed();

        var sut = BuildSut(req =>
        {
            requestedUrls.Add(req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed, Encoding.UTF8, "application/xml")
            };
        });

        await sut.FetchArticlesAsync(btc, Since);

        requestedUrls.Should().AllSatisfy(u => u.Should().Contain("restrict_sr=on"));
    }

    /// <summary>
    /// Simple fake HTTP message handler for testing.
    /// </summary>
    private class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
