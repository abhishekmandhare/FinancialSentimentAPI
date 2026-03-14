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
    public void SanitizeLlmJson_FixesCommonErrors(string input, string expected)
    {
        var result = OllamaSentimentService.SanitizeLlmJson(input);
        result.Should().Be(expected);
    }
}
