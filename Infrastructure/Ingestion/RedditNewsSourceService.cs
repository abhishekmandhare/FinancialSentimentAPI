using System.Net;
using System.Xml;
using System.Xml.Linq;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ingestion;

/// <summary>
/// Fetches financial news via Reddit RSS search feed.
/// Searches /r/stocks for all symbols, and additionally /r/cryptocurrency for crypto symbols.
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

    private const int MaxRetries = 2;
    private static readonly TimeSpan BaseBackoff = TimeSpan.FromSeconds(5);

    public async Task<IReadOnlyList<FetchedArticle>> FetchArticlesAsync(
        StockSymbol symbol,
        DateTime since,
        CancellationToken ct = default)
    {
        // For crypto symbols, search by base ticker (e.g. "BTC" not "BTC-USD")
        // because Reddit users don't use Yahoo Finance ticker format.
        var searchTerm = symbol.IsCrypto ? symbol.BaseTicker : symbol.Value;

        var subreddits = symbol.IsCrypto
            ? new[] { "stocks", "cryptocurrency" }
            : new[] { "stocks" };

        var allArticles = new List<FetchedArticle>();

        foreach (var subreddit in subreddits)
        {
            var url = $"https://www.reddit.com/r/{subreddit}/search.rss?q={Uri.EscapeDataString(searchTerm)}&restrict_sr=on&sort=new&t=day";

            try
            {
                var xml = await FetchWithRetryAsync(url, subreddit, symbol.Value, ct);
                if (xml is not null)
                    allArticles.AddRange(ParseAtom(xml, since));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (XmlException ex)
            {
                logger.LogWarning(ex, "Failed to parse Reddit RSS feed from r/{Subreddit} for {Symbol}", subreddit, symbol.Value);
            }

            // Reddit rate-limits unauthenticated RSS requests aggressively.
            // A short delay between subreddit fetches prevents 429 errors.
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }

        // Deduplicate by URL
        return allArticles
            .GroupBy(a => a.SourceUrl)
            .Select(g => g.First())
            .ToList();
    }

    private async Task<string?> FetchWithRetryAsync(string url, string subreddit, string symbol, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(url, ct);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt == MaxRetries)
                    {
                        logger.LogWarning("Reddit rate-limited r/{Subreddit} for {Symbol} after {Attempts} retries — skipping",
                            subreddit, symbol, MaxRetries + 1);
                        return null;
                    }

                    var delay = BaseBackoff * (attempt + 1);
                    logger.LogDebug("Reddit 429 for r/{Subreddit} {Symbol} — backing off {Seconds}s (attempt {Attempt}/{Max})",
                        subreddit, symbol, delay.TotalSeconds, attempt + 1, MaxRetries + 1);
                    await Task.Delay(delay, ct);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(ct);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                logger.LogDebug(ex, "HTTP error fetching r/{Subreddit} for {Symbol} — retrying", subreddit, symbol);
                await Task.Delay(BaseBackoff * (attempt + 1), ct);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Failed to fetch Reddit RSS feed from r/{Subreddit} for {Symbol}", subreddit, symbol);
                return null;
            }
        }

        return null;
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
