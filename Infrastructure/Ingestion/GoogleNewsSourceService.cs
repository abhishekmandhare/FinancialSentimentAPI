using System.Xml.Linq;
using System.Xml;
using System.Net.Http;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ingestion;

/// <summary>
/// Fetches financial news via Google News RSS feeds.
/// No API key required — uses publicly available RSS endpoints.
///
/// RSS XML structure:
///   channel → item → title, description, link, pubDate
///
/// Text sent for analysis = title + description (enough for sentiment signal).
/// </summary>
public class GoogleNewsSourceService(
    HttpClient httpClient,
    ILogger<GoogleNewsSourceService> logger)
    : INewsSourceService
{
    public async Task<IReadOnlyList<FetchedArticle>> FetchArticlesAsync(
        StockSymbol symbol,
        DateTime since,
        CancellationToken ct = default)
    {
        var suffix = symbol.IsCrypto ? "crypto news" : "stock news";
        var url = $"https://news.google.com/rss/search?q={Uri.EscapeDataString(symbol.Value + " " + suffix)}&hl=en-US&gl=US&ceid=US:en";

        try
        {
            var xml = await httpClient.GetStringAsync(url, ct);
            return ParseRss(xml, since);
        }
        catch (Exception ex) when (
            ex is HttpRequestException
            || ex is TaskCanceledException
            || ex is OperationCanceledException
            || ex is XmlException)
        {
            logger.LogWarning(ex, "Failed to fetch Google News RSS feed for {Symbol}", symbol.Value);
            return [];
        }
    }

    private static List<FetchedArticle> ParseRss(string xml, DateTime since)
    {
        var doc   = XDocument.Parse(xml);
        var items = doc.Descendants("item");

        return items
            .Select(item =>
            {
                var title      = item.Element("title")?.Value ?? string.Empty;
                var link       = item.Element("link")?.Value;
                var pubDateStr = item.Element("pubDate")?.Value;

                var pubDate = DateTime.TryParse(pubDateStr, out var d)
                    ? d.ToUniversalTime()
                    : DateTime.UtcNow;

                return new { Article = new FetchedArticle(title.Trim(), link, pubDate), PubDate = pubDate };
            })
            .Where(x => x.PubDate > since && !string.IsNullOrWhiteSpace(x.Article.Text))
            .Select(x => x.Article)
            .ToList();
    }
}
