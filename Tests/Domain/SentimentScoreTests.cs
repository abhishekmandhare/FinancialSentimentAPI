using Domain.Enums;
using Domain.Exceptions;
using Domain.ValueObjects;
using FluentAssertions;

namespace Tests.Domain;

public class SentimentScoreTests
{
    [Theory]
    [InlineData(-1.0)]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(0.82)]
    [InlineData(-0.55)]
    public void Create_ValidScore_Succeeds(double value)
    {
        var score = new SentimentScore(value);
        score.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(-1.01)]
    [InlineData(1.01)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void Create_OutOfRange_ThrowsDomainException(double value)
    {
        var act = () => new SentimentScore(value);
        act.Should().Throw<DomainException>()
            .WithMessage("*between -1.0 and 1.0*");
    }

    [Theory]
    [InlineData(0.21,  SentimentLabel.Positive)]
    [InlineData(1.0,   SentimentLabel.Positive)]
    [InlineData(0.0,   SentimentLabel.Neutral)]
    [InlineData(0.2,   SentimentLabel.Neutral)]
    [InlineData(-0.2,  SentimentLabel.Neutral)]
    [InlineData(-0.21, SentimentLabel.Negative)]
    [InlineData(-1.0,  SentimentLabel.Negative)]
    public void DeriveLabel_ReturnsCorrectLabel(double value, SentimentLabel expected)
    {
        var score = new SentimentScore(value);
        score.DeriveLabel().Should().Be(expected);
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        new SentimentScore(0.5).Should().Be(new SentimentScore(0.5));
    }
}
