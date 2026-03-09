using Application.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ingestion;

/// <summary>
/// Filters out articles that are obviously irrelevant to financial sentiment analysis.
///
/// Three checks (all conservative — when in doubt, keep the article):
///   1. Minimum content length — skip near-empty articles
///   2. URL pattern filter — skip non-news URLs (event registrations, login pages)
///   3. Keyword check — require at least one financial term OR the symbol/company name
/// </summary>
public class ArticleRelevanceFilter(ILogger<ArticleRelevanceFilter> logger) : IArticleRelevanceFilter
{
    /// <summary>
    /// Articles shorter than this are too short to carry meaningful sentiment signal.
    /// </summary>
    internal const int MinContentLength = 50;

    /// <summary>
    /// URL path segments that indicate non-news content.
    /// </summary>
    private static readonly string[] IrrelevantUrlPatterns =
    [
        "/event", "/events/", "/register", "/registration",
        "/login", "/signin", "/sign-in", "/signup", "/sign-up",
        "/campus", "/calendar", "/careers", "/jobs",
        "/subscribe", "/newsletter"
    ];

    /// <summary>
    /// Reddit subreddits that are relevant to financial sentiment analysis.
    /// Posts from other subreddits are filtered out to prevent noise.
    /// </summary>
    private static readonly string[] AllowedRedditSubreddits =
    [
        "/r/stocks", "/r/wallstreetbets", "/r/investing", "/r/stockmarket",
        "/r/options", "/r/finance", "/r/cryptocurrency", "/r/bitcoin",
        "/r/economy", "/r/valueinvesting", "/r/dividends", "/r/securityanalysis",
        "/r/personalfinance", "/r/financialindependence"
    ];

    /// <summary>
    /// Financial terms that signal an article is worth analyzing.
    /// Kept broad and lowercase for case-insensitive matching.
    /// </summary>
    private static readonly string[] FinancialKeywords =
    [
        "stock", "share", "shares", "equity", "equities",
        "earnings", "revenue", "profit", "loss", "dividend",
        "market", "trading", "trader", "investor", "investment",
        "bull", "bear", "rally", "selloff", "sell-off",
        "ipo", "merger", "acquisition", "buyout",
        "analyst", "forecast", "guidance", "outlook",
        "sec", "filing", "10-k", "10-q", "8-k",
        "price target", "upgrade", "downgrade", "overweight", "underweight",
        "quarterly", "annual", "fiscal", "financial",
        "ceo", "cfo", "cto", "executive",
        "nasdaq", "nyse", "s&p", "dow jones", "wall street",
        "bond", "yield", "interest rate", "fed", "inflation",
        "valuation", "p/e", "eps", "ebitda", "cash flow",
        "growth", "decline", "surge", "plunge", "soar", "tumble",
        "beat", "miss", "exceed", "disappoint",
        "buy", "sell", "hold", "outperform", "underperform",
        // Crypto-specific terms
        "crypto", "cryptocurrency", "bitcoin", "ethereum", "blockchain",
        "token", "altcoin", "defi", "mining", "halving",
        "staking", "wallet", "exchange", "binance", "coinbase",
        "solana", "cardano", "dogecoin", "ripple", "litecoin"
    ];

    public bool IsRelevant(ArticleToAnalyze article)
    {
        if (!PassesMinimumLength(article))
        {
            logger.LogDebug(
                "Filtered article for {Symbol}: content too short ({Length} chars). URL: {Url}",
                article.Symbol, article.Text.Length, article.SourceUrl ?? "unknown");
            return false;
        }

        if (!PassesUrlFilter(article))
        {
            logger.LogDebug(
                "Filtered article for {Symbol}: irrelevant URL pattern. URL: {Url}",
                article.Symbol, article.SourceUrl ?? "unknown");
            return false;
        }

        if (!PassesKeywordCheck(article))
        {
            logger.LogDebug(
                "Filtered article for {Symbol}: no financial keywords found. URL: {Url}",
                article.Symbol, article.SourceUrl ?? "unknown");
            return false;
        }

        return true;
    }

    internal static bool PassesMinimumLength(ArticleToAnalyze article)
        => article.Text.Length >= MinContentLength;

    internal static bool PassesUrlFilter(ArticleToAnalyze article)
    {
        if (string.IsNullOrWhiteSpace(article.SourceUrl))
            return true; // No URL to check — keep the article

        var lowerUrl = article.SourceUrl.ToLowerInvariant();

        if (IrrelevantUrlPatterns.Any(pattern => lowerUrl.Contains(pattern)))
            return false;

        // Reddit posts must be from a finance-related subreddit
        if (lowerUrl.Contains("reddit.com/r/"))
            return AllowedRedditSubreddits.Any(sub => lowerUrl.Contains(sub));

        return true;
    }

    internal static bool PassesKeywordCheck(ArticleToAnalyze article)
    {
        var lowerText = article.Text.ToLowerInvariant();
        var lowerSymbol = article.Symbol.ToLowerInvariant();

        // Check if the full symbol appears (e.g. "BTC-USD", "AAPL")
        if (ContainsWholeWord(lowerText, lowerSymbol))
            return true;

        // For crypto symbols like "BTC-USD", also check the base ticker "BTC"
        if (lowerSymbol.EndsWith("-usd") && ContainsWholeWord(lowerText, lowerSymbol[..^4]))
            return true;

        // Check for any financial keyword
        return FinancialKeywords.Any(keyword => keyword.Length <= 3
            ? ContainsWholeWord(lowerText, keyword)
            : lowerText.Contains(keyword));
    }

    /// <summary>
    /// Checks if a word appears as a whole word (not as a substring of another word).
    /// Boundaries are non-alphanumeric characters or start/end of string.
    /// </summary>
    private static bool ContainsWholeWord(string text, string word)
    {
        var index = 0;
        while ((index = text.IndexOf(word, index, StringComparison.Ordinal)) >= 0)
        {
            var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var afterPos = index + word.Length;
            var after = afterPos >= text.Length || !char.IsLetterOrDigit(text[afterPos]);

            if (before && after)
                return true;

            index += word.Length;
        }

        return false;
    }
}
