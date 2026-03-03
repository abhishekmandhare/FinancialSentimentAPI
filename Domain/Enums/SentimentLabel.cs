namespace Domain.Enums;

/// <summary>
/// Closed set of sentiment categories.
/// Derived by the domain from SentimentScore — never assigned directly by the AI.
/// </summary>
public enum SentimentLabel
{
    Positive,
    Neutral,
    Negative
}
