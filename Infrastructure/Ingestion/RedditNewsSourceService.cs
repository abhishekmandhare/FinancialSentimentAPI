using System.Xml;
using System.Xml.Linq;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ingestion;

/// <summary>
/// Fetches financial news via Reddit RSS search feed for r/stocks.
/// No API key required — uses publicly available RSS endpoints.
///
/// RSS XML structure (Atom-flavoured):
///   feed → entry → title, link (href attr), updated
///
/// Text sent for analysis = title (enough for sentiment signal).
/// </summary>
public class RedditNewsSourceService(
    HttpClient httpClient,
    ILogger<RedditNewsSourceService> logger)
    : INewsSourceService
{
    // Atom namespace
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public async Task<IReadOnlyList<FetchedArticle>> FetchArticlesAsync(
        StockSymbol symbol,
        DateTime since,
        CancellationToken ct = default)
    {
        var url = $"https://www.reddit.com/r/stocks/search.rss?q={Uri.EscapeDataString(symbol.Value)}&sort=new&t=day";

        try
        {
            var xml = await httpClient.GetStringAsync(url, ct);
            return ParseAtom(xml, since);
        }
        catch (OperationCanceledException)
        {
            // Respect cancellation and allow it to propagate.
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to fetch Reddit RSS feed for {Symbol}", symbol.Value);
            return [];
        }
        catch (XmlException ex)
        {
            logger.LogWarning(ex, "Failed to parse Reddit RSS feed for {Symbol}", symbol.Value);
            return [];
        }
    }

    private static List<FetchedArticle> ParseAtom(string xml, DateTime since)
    {
        var doc     = XDocument.Parse(xml);
        var entries = doc.Descendants(Atom + "entry");

        return entries
            .Select(entry =>
            {
                var title      = entry.Element(Atom + "title")?.Value ?? string.Empty;
                var link       = entry.Element(Atom + "link")?.Attribute("href")?.Value;
                var updatedStr = entry.Element(Atom + "updated")?.Value;

                var pubDate = DateTime.TryParse(updatedStr, out var d)
                    ? d.ToUniversalTime()
                    : DateTime.UtcNow;

                return new { Article = new FetchedArticle(title.Trim(), link, pubDate), PubDate = pubDate };
            })
            .Where(x => x.PubDate > since && !string.IsNullOrWhiteSpace(x.Article.Text))
            .Select(x => x.Article)
            .ToList();
    }
}
