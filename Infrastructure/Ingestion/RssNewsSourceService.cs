using System.Xml.Linq;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ingestion;

/// <summary>
/// Fetches financial news via Yahoo Finance RSS feeds.
/// No API key required — uses publicly available RSS endpoints.
///
/// RSS XML structure:
///   channel → item → title, description, link, pubDate
///
/// Text sent for analysis = title + description (enough for sentiment signal).
/// </summary>
public class RssNewsSourceService(
    HttpClient httpClient,
    ILogger<RssNewsSourceService> logger)
    : INewsSourceService
{
    public async Task<IReadOnlyList<FetchedArticle>> FetchArticlesAsync(
        StockSymbol symbol,
        DateTime since,
        CancellationToken ct = default)
    {
        var url = $"https://feeds.finance.yahoo.com/rss/2.0/headline?s={symbol.Value}&region=US&lang=en-US";

        try
        {
            var xml = await httpClient.GetStringAsync(url, ct);
            return ParseRss(xml, since);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch RSS feed for {Symbol}", symbol.Value);
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
                var title       = item.Element("title")?.Value ?? string.Empty;
                var description = item.Element("description")?.Value ?? string.Empty;
                var link        = item.Element("link")?.Value;
                var pubDateStr  = item.Element("pubDate")?.Value;

                var pubDate = DateTime.TryParse(pubDateStr, out var d)
                    ? d.ToUniversalTime()
                    : DateTime.UtcNow;

                var text = $"{title}. {description}".Trim();

                return new { Article = new FetchedArticle(text, link, pubDate), PubDate = pubDate };
            })
            .Where(x => x.PubDate > since && !string.IsNullOrWhiteSpace(x.Article.Text))
            .Select(x => x.Article)
            .ToList();
    }
}
