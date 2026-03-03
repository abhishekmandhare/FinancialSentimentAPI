using Domain.Enums;
using Domain.Exceptions;

namespace Domain.ValueObjects;

public record SentimentScore
{
    public double Value { get; }

    public SentimentScore(double value)
    {
        if (value < -1.0 || value > 1.0)
            throw new DomainException($"Sentiment score must be between -1.0 and 1.0. Got: {value}.");

        Value = value;
    }

    /// <summary>
    /// Derives a label from the score. Thresholds are configurable —
    /// different business contexts may define "positive" differently.
    /// </summary>
    public SentimentLabel DeriveLabel(double negativeThreshold = -0.2, double positiveThreshold = 0.2) =>
        Value switch
        {
            var v when v > positiveThreshold => SentimentLabel.Positive,
            var v when v < negativeThreshold => SentimentLabel.Negative,
            _                                => SentimentLabel.Neutral
        };

    public override string ToString() => Value.ToString("F2");
}
