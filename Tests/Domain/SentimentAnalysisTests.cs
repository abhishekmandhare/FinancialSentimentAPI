using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Exceptions;
using Domain.ValueObjects;
using FluentAssertions;

namespace Tests.Domain;

public class SentimentAnalysisTests
{
    private static readonly StockSymbol Symbol = new("AAPL");

    private static SentimentAnalysis CreateValid(double score = 0.8) =>
        SentimentAnalysis.Create(
            Symbol,
            "Apple crushed Q4 earnings expectations.",
            "https://reuters.com/article",
            score,
            0.9,
            ["Strong revenue", "Beat estimates"],
            "claude-haiku-4-5-20251001");

    [Fact]
    public void Create_ValidInput_ReturnsEntityWithCorrectValues()
    {
        var analysis = CreateValid(0.8);

        analysis.Symbol.Should().Be(Symbol);
        analysis.Score.Value.Should().Be(0.8);
        analysis.Label.Should().Be(SentimentLabel.Positive);
        analysis.Confidence.Should().Be(0.9);
        analysis.KeyReasons.Should().HaveCount(2);
        analysis.AnalyzedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_DerivesLabelFromScore_NotAssignedExternally()
    {
        var positive = CreateValid(0.5);
        var neutral  = CreateValid(0.0);
        var negative = CreateValid(-0.5);

        positive.Label.Should().Be(SentimentLabel.Positive);
        neutral.Label.Should().Be(SentimentLabel.Neutral);
        negative.Label.Should().Be(SentimentLabel.Negative);
    }

    [Fact]
    public void Create_RaisesDomainEvent()
    {
        var analysis = CreateValid();

        analysis.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SentimentAnalysisCreatedEvent>();
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var analysis = CreateValid();
        analysis.ClearDomainEvents();
        analysis.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Create_EmptyText_ThrowsDomainException()
    {
        var act = () => SentimentAnalysis.Create(Symbol, "", null, 0.5, 0.9, [], "model");
        act.Should().Throw<DomainException>().WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Create_TextExceeds5000Chars_ThrowsDomainException()
    {
        var longText = new string('x', 5001);
        var act = () => SentimentAnalysis.Create(Symbol, longText, null, 0.5, 0.9, [], "model");
        act.Should().Throw<DomainException>().WithMessage("*5000 characters*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Create_InvalidConfidence_ThrowsDomainException(double confidence)
    {
        var act = () => SentimentAnalysis.Create(Symbol, "Valid text here.", null, 0.5, confidence, [], "model");
        act.Should().Throw<DomainException>().WithMessage("*Confidence*");
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var a = CreateValid();
        var b = CreateValid();
        a.Id.Should().NotBe(b.Id);
    }
}
