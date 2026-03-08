namespace Application.Services;

/// <summary>
/// Pre-filters articles before they are sent for AI sentiment analysis.
/// Rejects obviously irrelevant content (campus events, login pages, etc.)
/// to avoid wasting AI resources on non-financial articles.
/// </summary>
public interface IArticleRelevanceFilter
{
    /// <summary>
    /// Determines whether an article is relevant enough to warrant AI analysis.
    /// </summary>
    /// <param name="article">The article to evaluate.</param>
    /// <returns>True if the article should be analyzed; false if it should be skipped.</returns>
    bool IsRelevant(ArticleToAnalyze article);
}
