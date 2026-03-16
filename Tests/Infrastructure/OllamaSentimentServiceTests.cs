using FluentAssertions;
using Infrastructure.Services;

namespace Tests.Infrastructure;

public class OllamaSentimentServiceTests
{
    [Theory]
    [InlineData(
        """{"score": -0.3, "confidence": 0.8, "keyReasons": ["reason"']}""",
        """{"score": -0.3, "confidence": 0.8, "keyReasons": ["reason"]}""")]
    [InlineData(
        """{"score": 0.5, "keyReasons": ["a", "b",]}""",
        """{"score": 0.5, "keyReasons": ["a", "b"]}""")]
    [InlineData(
        """{"score": 0.0, "keyReasons": [`"reason"`]}""",
        """{"score": 0.0, "keyReasons": ["reason"]}""")]
    [InlineData(
        """{"score": 0.5, "confidence": 0.9, "keyReasons": ["it's good"]}""",
        """{"score": 0.5, "confidence": 0.9, "keyReasons": ["it's good"]}""")]
    [InlineData(
        """{"score": 1.0}""",
        """{"score": 1.0}""")]
    [InlineData(
        """{"score": 0.4, "confidence": 0.8, "keyReasons": ["TSLA mentioned", "neutral news"]""",
        """{"score": 0.4, "confidence": 0.8, "keyReasons": ["TSLA mentioned", "neutral news"]}""")]
    [InlineData(
        """{"score": 0.8, "confidence": 0.9, "keyReasons": ["Positive mention", "Seeking Alpha reference"]"}""",
        """{"score": 0.8, "confidence": 0.9, "keyReasons": ["Positive mention", "Seeking Alpha reference"]}""")]
    [InlineData(
        """{"score": 0.4, "confidence": 0.9, "keyReasons": ["Positive tone towards AMZN stock", "Question asks about buying AMZN", "Insider Monkey is a reputable source"]"}""",
        """{"score": 0.4, "confidence": 0.9, "keyReasons": ["Positive tone towards AMZN stock", "Question asks about buying AMZN", "Insider Monkey is a reputable source"]}""")]
    public void SanitizeLlmJson_FixesCommonErrors(string input, string expected)
    {
        var result = OllamaSentimentService.SanitizeLlmJson(input);
        result.Should().Be(expected);
    }
}
