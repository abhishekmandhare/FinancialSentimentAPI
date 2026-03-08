using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.ValueObjects;
using FluentAssertions;

namespace Tests.Domain;

public class SentimentAnalysisEdgeCaseTests
{
    private static readonly StockSymbol Symbol = new("AAPL");

    [Fact]
    public void Create_EmptyModelVersion_ThrowsDomainException()
    {
        var act = () => SentimentAnalysis.Create(Symbol, "Valid text.", null, 0.5, 0.9, [], "");
        act.Should().Throw<DomainException>().WithMessage("*Model version*");
    }

    [Fact]
    public void Create_WhitespaceModelVersion_ThrowsDomainException()
    {
        var act = () => SentimentAnalysis.Create(Symbol, "Valid text.", null, 0.5, 0.9, [], "   ");
        act.Should().Throw<DomainException>().WithMessage("*Model version*");
    }

    [Fact]
    public void Create_ConfidenceExactlyZero_Succeeds()
    {
        var analysis = SentimentAnalysis.Create(Symbol, "Valid text.", null, 0.5, 0.0, [], "model-v1");
        analysis.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Create_ConfidenceExactlyOne_Succeeds()
    {
        var analysis = SentimentAnalysis.Create(Symbol, "Valid text.", null, 0.5, 1.0, [], "model-v1");
        analysis.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void Create_NullSourceUrl_StoresNull()
    {
        var analysis = SentimentAnalysis.Create(Symbol, "Valid text.", null, 0.5, 0.9, [], "model-v1");
        analysis.SourceUrl.Should().BeNull();
    }

    [Fact]
    public void Create_WithSourceUrl_StoresUrl()
    {
        var analysis = SentimentAnalysis.Create(
            Symbol, "Valid text.", "https://example.com", 0.5, 0.9, [], "model-v1");
        analysis.SourceUrl.Should().Be("https://example.com");
    }

    [Fact]
    public void Create_EmptyKeyReasons_AcceptsEmptyList()
    {
        var analysis = SentimentAnalysis.Create(Symbol, "Valid text.", null, 0.5, 0.9, [], "model-v1");
        analysis.KeyReasons.Should().BeEmpty();
    }

    [Fact]
    public void Create_TextExactly5000Chars_Succeeds()
    {
        var text = new string('x', 5000);
        var analysis = SentimentAnalysis.Create(Symbol, text, null, 0.5, 0.9, [], "model-v1");
        analysis.OriginalText.Should().HaveLength(5000);
    }

    [Theory]
    [InlineData(-1.0, SentimentLabel.Negative)]
    [InlineData(-0.21, SentimentLabel.Negative)]
    [InlineData(-0.2, SentimentLabel.Neutral)]
    [InlineData(0.0, SentimentLabel.Neutral)]
    [InlineData(0.2, SentimentLabel.Neutral)]
    [InlineData(0.21, SentimentLabel.Positive)]
    [InlineData(1.0, SentimentLabel.Positive)]
    public void Create_ScoreBoundaries_DerivesCorrectLabel(double score, SentimentLabel expected)
    {
        var analysis = SentimentAnalysis.Create(Symbol, "Valid text.", null, score, 0.9, [], "model-v1");
        analysis.Label.Should().Be(expected);
    }

    [Fact]
    public void Create_WhitespaceText_ThrowsDomainException()
    {
        var act = () => SentimentAnalysis.Create(Symbol, "   ", null, 0.5, 0.9, [], "model-v1");
        act.Should().Throw<DomainException>().WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Create_ScoreAtBoundaryNegativeOne_Succeeds()
    {
        var analysis = SentimentAnalysis.Create(Symbol, "Valid text.", null, -1.0, 0.9, [], "model-v1");
        analysis.Score.Value.Should().Be(-1.0);
    }

    [Fact]
    public void Create_ScoreAtBoundaryPositiveOne_Succeeds()
    {
        var analysis = SentimentAnalysis.Create(Symbol, "Valid text.", null, 1.0, 0.9, [], "model-v1");
        analysis.Score.Value.Should().Be(1.0);
    }
}
