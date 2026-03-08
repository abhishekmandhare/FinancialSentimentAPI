using System.Net;
using System.Text;
using Domain.ValueObjects;
using FluentAssertions;
using Infrastructure.Ingestion;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Tests.Infrastructure;

public class GoogleNewsSourceServiceTests
{
    private static readonly DateTime Since = DateTime.UtcNow.AddHours(-1);

    private static string BuildRssFeed(params (string title, string url, DateTime pubDate)[] items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("<rss version=\"2.0\"><channel>");
        foreach (var (title, url, pubDate) in items)
        {
            sb.AppendLine("<item>");
            sb.AppendLine($"  <title>{title}</title>");
            sb.AppendLine($"  <link>{url}</link>");
            sb.AppendLine($"  <pubDate>{pubDate:R}</pubDate>");
            sb.AppendLine("</item>");
        }
        sb.AppendLine("</channel></rss>");
        return sb.ToString();
    }

    private static GoogleNewsSourceService BuildSut(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var messageHandler = new FakeHttpMessageHandler(handler);
        var httpClient = new HttpClient(messageHandler);
        var logger = Substitute.For<ILogger<GoogleNewsSourceService>>();
        return new GoogleNewsSourceService(httpClient, logger);
    }

    // ── Crypto symbol uses "crypto news" suffix ──────────────────────────

    [Fact]
    public async Task FetchArticlesAsync_CryptoSymbol_UsesCryptoNewsSuffix()
    {
        var btc = new StockSymbol("BTC-USD");
        string? capturedUrl = null;

        var feed = BuildRssFeed(("Bitcoin surges", "https://news.example/1", DateTime.UtcNow));

        var sut = BuildSut(req =>
        {
            capturedUrl = req.RequestUri!.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed, Encoding.UTF8, "application/xml")
            };
        });

        await sut.FetchArticlesAsync(btc, Since);

        capturedUrl.Should().NotBeNull();
        capturedUrl.Should().Contain("crypto news");
        capturedUrl.Should().NotContain("stock news");
    }

    [Fact]
    public async Task FetchArticlesAsync_RegularSymbol_UsesStockNewsSuffix()
    {
        var aapl = new StockSymbol("AAPL");
        string? capturedUrl = null;

        var feed = BuildRssFeed(("Apple earnings", "https://news.example/1", DateTime.UtcNow));

        var sut = BuildSut(req =>
        {
            capturedUrl = req.RequestUri!.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed, Encoding.UTF8, "application/xml")
            };
        });

        await sut.FetchArticlesAsync(aapl, Since);

        capturedUrl.Should().NotBeNull();
        capturedUrl.Should().Contain("stock news");
        capturedUrl.Should().NotContain("crypto news");
    }

    [Theory]
    [InlineData("ETH-USD")]
    [InlineData("SOL-USD")]
    [InlineData("DOGE-USD")]
    public async Task FetchArticlesAsync_VariousCryptoSymbols_AllUseCryptoNewsSuffix(string symbolValue)
    {
        var symbol = new StockSymbol(symbolValue);
        string? capturedUrl = null;

        var feed = BuildRssFeed(("Crypto headline", "https://news.example/1", DateTime.UtcNow));

        var sut = BuildSut(req =>
        {
            capturedUrl = req.RequestUri!.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed, Encoding.UTF8, "application/xml")
            };
        });

        await sut.FetchArticlesAsync(symbol, Since);

        capturedUrl.Should().Contain("crypto news");
    }

    [Theory]
    [InlineData("AAPL")]
    [InlineData("MSFT")]
    [InlineData("GOOGL")]
    public async Task FetchArticlesAsync_VariousStockSymbols_AllUseStockNewsSuffix(string symbolValue)
    {
        var symbol = new StockSymbol(symbolValue);
        string? capturedUrl = null;

        var feed = BuildRssFeed(("Stock headline", "https://news.example/1", DateTime.UtcNow));

        var sut = BuildSut(req =>
        {
            capturedUrl = req.RequestUri!.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(feed, Encoding.UTF8, "application/xml")
            };
        });

        await sut.FetchArticlesAsync(symbol, Since);

        capturedUrl.Should().Contain("stock news");
    }

    private class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
