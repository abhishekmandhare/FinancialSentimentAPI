using Application.Services;
using FluentAssertions;
using Infrastructure.Ingestion;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Tests.Infrastructure;

public class ArticleRelevanceFilterTests
{
    private readonly ArticleRelevanceFilter _filter;

    public ArticleRelevanceFilterTests()
    {
        var logger = Substitute.For<ILogger<ArticleRelevanceFilter>>();
        _filter = new ArticleRelevanceFilter(logger);
    }

    private static ArticleToAnalyze MakeArticle(
        string text,
        string symbol = "AAPL",
        string? url = "https://finance.yahoo.com/news/article-1") =>
        new(symbol, text, url, DateTime.UtcNow);

    // --- Minimum content length ---

    [Fact]
    public void IsRelevant_ShortContent_ReturnsFalse()
    {
        var article = MakeArticle("Short text");
        _filter.IsRelevant(article).Should().BeFalse();
    }

    [Fact]
    public void IsRelevant_EmptyContent_ReturnsFalse()
    {
        var article = MakeArticle(string.Empty);
        _filter.IsRelevant(article).Should().BeFalse();
    }

    [Fact]
    public void IsRelevant_ExactlyMinLength_WithKeyword_ReturnsTrue()
    {
        // Build a string that is exactly MinContentLength and contains a financial keyword
        var text = "AAPL stock " + new string('x', ArticleRelevanceFilter.MinContentLength - 11);
        text.Length.Should().Be(ArticleRelevanceFilter.MinContentLength);

        var article = MakeArticle(text);
        _filter.IsRelevant(article).Should().BeTrue();
    }

    // --- URL pattern filter ---

    [Theory]
    [InlineData("https://example.com/events/campus-fair")]
    [InlineData("https://example.com/register?id=123")]
    [InlineData("https://example.com/registration/event")]
    [InlineData("https://example.com/login")]
    [InlineData("https://example.com/signin")]
    [InlineData("https://example.com/careers/apply")]
    [InlineData("https://example.com/jobs/listing")]
    [InlineData("https://example.com/calendar/2026")]
    [InlineData("https://example.com/campus/news")]
    [InlineData("https://example.com/subscribe")]
    [InlineData("https://example.com/newsletter")]
    public void IsRelevant_IrrelevantUrl_ReturnsFalse(string url)
    {
        var article = MakeArticle(
            "Apple stock surges on strong quarterly earnings report, beating analyst estimates significantly.",
            url: url);
        _filter.IsRelevant(article).Should().BeFalse();
    }

    [Fact]
    public void IsRelevant_NullUrl_DoesNotFilter()
    {
        var article = MakeArticle(
            "Apple stock surges on strong quarterly earnings report, beating analyst estimates significantly.",
            url: null);
        _filter.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_NormalNewsUrl_DoesNotFilter()
    {
        var article = MakeArticle(
            "Apple stock surges on strong quarterly earnings report, beating analyst estimates significantly.",
            url: "https://finance.yahoo.com/news/apple-earnings-2026");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    // --- Keyword check ---

    [Fact]
    public void IsRelevant_ContainsSymbol_ReturnsTrue()
    {
        var article = MakeArticle(
            "AAPL continues its upward trend amid positive market conditions and increased consumer demand.",
            symbol: "AAPL");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_ContainsFinancialKeywords_ReturnsTrue()
    {
        var article = MakeArticle(
            "The company reported strong quarterly earnings, with revenue exceeding analyst expectations by a wide margin.");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_StockPriceArticle_ReturnsTrue()
    {
        var article = MakeArticle(
            "Apple shares surged 5% after the company announced a record dividend and a massive stock buyback program.");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_AnalystUpgrade_ReturnsTrue()
    {
        var article = MakeArticle(
            "Goldman Sachs analyst upgrades Apple with a new price target of $250, citing strong iPhone demand outlook.");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_CampusEvent_ReturnsFalse()
    {
        var article = MakeArticle(
            "Join us for the annual spring campus carnival this Saturday. Food, games, and live music for all students and families.",
            url: "https://example.com/news/campus-carnival");
        _filter.IsRelevant(article).Should().BeFalse();
    }

    [Fact]
    public void IsRelevant_SportsArticle_ReturnsFalse()
    {
        var article = MakeArticle(
            "The university football team defeated their rivals in a thrilling overtime game, securing their spot in the playoffs.");
        _filter.IsRelevant(article).Should().BeFalse();
    }

    [Fact]
    public void IsRelevant_RecipeArticle_ReturnsFalse()
    {
        var article = MakeArticle(
            "Try this delicious homemade pasta recipe with fresh tomatoes, basil, and mozzarella cheese for a perfect Italian dinner.");
        _filter.IsRelevant(article).Should().BeFalse();
    }

    [Fact]
    public void IsRelevant_MergerAcquisition_ReturnsTrue()
    {
        var article = MakeArticle(
            "The proposed merger between the two tech giants faces regulatory scrutiny from the FTC and European regulators.");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_FedInterestRate_ReturnsTrue()
    {
        var article = MakeArticle(
            "The Fed is expected to hold interest rates steady at its next meeting, according to market expectations and recent data.");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    // --- Case insensitivity ---

    [Fact]
    public void IsRelevant_KeywordsCaseInsensitive_ReturnsTrue()
    {
        var article = MakeArticle(
            "QUARTERLY EARNINGS exceeded expectations with REVENUE growth of 15% year-over-year across all segments.");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_SymbolCaseInsensitive_ReturnsTrue()
    {
        var article = MakeArticle(
            "According to analysts, aapl remains a strong buy despite market volatility and macroeconomic uncertainty today.",
            symbol: "AAPL");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    // --- Static helper method tests ---

    [Fact]
    public void PassesMinimumLength_BelowThreshold_ReturnsFalse()
    {
        var article = MakeArticle("Too short");
        ArticleRelevanceFilter.PassesMinimumLength(article).Should().BeFalse();
    }

    [Fact]
    public void PassesUrlFilter_CleanUrl_ReturnsTrue()
    {
        var article = MakeArticle("text", url: "https://reuters.com/article/apple-earnings");
        ArticleRelevanceFilter.PassesUrlFilter(article).Should().BeTrue();
    }

    [Fact]
    public void PassesUrlFilter_EventUrl_ReturnsFalse()
    {
        var article = MakeArticle("text", url: "https://example.com/events/signup");
        ArticleRelevanceFilter.PassesUrlFilter(article).Should().BeFalse();
    }

    [Fact]
    public void PassesKeywordCheck_NoFinancialContent_ReturnsFalse()
    {
        var article = MakeArticle(
            "Beautiful sunny weather expected this weekend with clear skies and mild temperatures across the region.",
            symbol: "XYZ");
        ArticleRelevanceFilter.PassesKeywordCheck(article).Should().BeFalse();
    }

    [Fact]
    public void PassesKeywordCheck_WithSymbolInText_ReturnsTrue()
    {
        var article = MakeArticle(
            "According to experts, GOOGL is poised for a breakout this quarter based on recent performance indicators.",
            symbol: "GOOGL");
        ArticleRelevanceFilter.PassesKeywordCheck(article).Should().BeTrue();
    }

    // --- Reddit subreddit filter ---

    [Fact]
    public void IsRelevant_RedditStocksSubreddit_ReturnsTrue()
    {
        var article = MakeArticle(
            "SPY hits all-time high as market rallies on strong earnings and economic growth outlook this quarter.",
            symbol: "SPY",
            url: "https://www.reddit.com/r/stocks/comments/abc123/spy_hits_all_time_high/");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_RedditIrrelevantSubreddit_ReturnsFalse()
    {
        var article = MakeArticle(
            "Finally some luck! Found a rare game at the thrift store today for only five dollars, what a great buy.",
            symbol: "SPY",
            url: "https://www.reddit.com/r/gamecollecting/comments/1rnmzh0/finally_some_luck/");
        _filter.IsRelevant(article).Should().BeFalse();
    }

    [Fact]
    public void IsRelevant_RedditWallStreetBets_ReturnsTrue()
    {
        var article = MakeArticle(
            "YOLO'd my savings into SPY calls, this market rally is insane and earnings season is looking bullish overall.",
            symbol: "SPY",
            url: "https://www.reddit.com/r/wallstreetbets/comments/xyz789/yolo_spy/");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void PassesUrlFilter_RedditAllowedSubreddit_ReturnsTrue()
    {
        var article = MakeArticle("text", url: "https://www.reddit.com/r/investing/comments/abc/post");
        ArticleRelevanceFilter.PassesUrlFilter(article).Should().BeTrue();
    }

    [Fact]
    public void PassesUrlFilter_RedditDisallowedSubreddit_ReturnsFalse()
    {
        var article = MakeArticle("text", url: "https://www.reddit.com/r/gaming/comments/abc/post");
        ArticleRelevanceFilter.PassesUrlFilter(article).Should().BeFalse();
    }

    // --- Crypto article filtering ---

    [Theory]
    [InlineData("Bitcoin surges past $100k as institutional adoption accelerates and demand grows across global markets.")]
    [InlineData("Ethereum network upgrade drives renewed interest in defi protocols and staking yields across the ecosystem.")]
    [InlineData("Crypto market cap hits new record amid Bitcoin rally and strong altcoin performance this quarter overall.")]
    public void IsRelevant_CryptoArticle_WithCryptoKeywords_ReturnsTrue(string text)
    {
        var article = MakeArticle(text, symbol: "BTC-USD");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void PassesKeywordCheck_CryptoSymbol_MatchesBaseTicker()
    {
        // "BTC" should match for symbol "BTC-USD"
        var article = MakeArticle(
            "BTC breaks through resistance level as trading volume spikes on major exchanges worldwide this week.",
            symbol: "BTC-USD");
        ArticleRelevanceFilter.PassesKeywordCheck(article).Should().BeTrue();
    }

    [Fact]
    public void PassesKeywordCheck_CryptoSymbol_MatchesFullSymbol()
    {
        var article = MakeArticle(
            "BTC-USD price action shows bullish momentum with strong support levels holding across all timeframes today.",
            symbol: "BTC-USD");
        ArticleRelevanceFilter.PassesKeywordCheck(article).Should().BeTrue();
    }

    [Fact]
    public void PassesKeywordCheck_CryptoSymbol_NoMatchWithoutKeywords()
    {
        var article = MakeArticle(
            "Beautiful sunny weather expected this weekend with clear skies and mild temperatures across the region.",
            symbol: "BTC-USD");
        ArticleRelevanceFilter.PassesKeywordCheck(article).Should().BeFalse();
    }

    [Fact]
    public void IsRelevant_RedditCryptocurrencySubreddit_ReturnsTrue()
    {
        var article = MakeArticle(
            "Bitcoin mining difficulty reaches all-time high as hash rate continues to climb steadily this month overall.",
            symbol: "BTC-USD",
            url: "https://www.reddit.com/r/cryptocurrency/comments/abc123/bitcoin_mining/");
        _filter.IsRelevant(article).Should().BeTrue();
    }

    [Fact]
    public void IsRelevant_RedditBitcoinSubreddit_ReturnsTrue()
    {
        var article = MakeArticle(
            "Bitcoin halving event approaches, with market analysts predicting significant price movement and volatility ahead.",
            symbol: "BTC-USD",
            url: "https://www.reddit.com/r/bitcoin/comments/abc123/halving_prediction/");
        _filter.IsRelevant(article).Should().BeTrue();
    }
}
