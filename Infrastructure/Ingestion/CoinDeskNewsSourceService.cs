using System.Xml.Linq;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ingestion;

/// <summary>
/// Fetches crypto news from CoinDesk's public RSS feed.
/// Only activates for crypto symbols — skips traditional stocks.
///
/// RSS XML structure:
///   channel → item → title, description, link, pubDate
///
/// Text sent for analysis = title + description.
/// </summary>
public class CoinDeskNewsSourceService(
    HttpClient httpClient,
    ILogger<CoinDeskNewsSourceService> logger)
    : INewsSourceService
{
    private const string FeedUrl = "https://www.coindesk.com/arc/outboundfeeds/rss/";

    public async Task<IReadOnlyList<FetchedArticle>> FetchArticlesAsync(
        StockSymbol symbol,
        DateTime since,
        CancellationToken ct = default)
    {
        if (!symbol.IsCrypto)
            return [];

        try
        {
            var xml = await httpClient.GetStringAsync(FeedUrl, ct);
            return ParseRss(xml, since, symbol);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch CoinDesk RSS feed for {Symbol}", symbol.Value);
            return [];
        }
    }

    private static List<FetchedArticle> ParseRss(string xml, DateTime since, StockSymbol symbol)
    {
        var doc = XDocument.Parse(xml);
        var items = doc.Descendants("item");
        var baseTicker = symbol.BaseTicker.ToLowerInvariant();

        return items
            .Select(item =>
            {
                var title = item.Element("title")?.Value ?? string.Empty;
                var description = item.Element("description")?.Value ?? string.Empty;
                var link = item.Element("link")?.Value;
                var pubDateStr = item.Element("pubDate")?.Value;

                var pubDate = DateTime.TryParse(pubDateStr, out var d)
                    ? d.ToUniversalTime()
                    : DateTime.UtcNow;

                var text = $"{title}. {description}".Trim();

                return new { Article = new FetchedArticle(text, link, pubDate), PubDate = pubDate, Text = text };
            })
            .Where(x => x.PubDate > since
                && !string.IsNullOrWhiteSpace(x.Text)
                && x.Text.Contains(baseTicker, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Article)
            .ToList();
    }
}
