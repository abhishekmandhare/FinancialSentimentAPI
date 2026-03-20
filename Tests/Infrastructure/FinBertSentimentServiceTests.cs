using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Services;
using Domain.ValueObjects;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests.Infrastructure;

public class FinBertSentimentServiceTests
{
    private static FinBertSentimentService CreateService(HttpClient httpClient) =>
        new(httpClient, NullLogger<FinBertSentimentService>.Instance);

    private static HttpClient CreateMockClient(object responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new MockHttpMessageHandler(
            JsonSerializer.Serialize(responseBody), statusCode);
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8001") };
    }

    [Fact]
    public async Task AnalyzeAsync_PositiveSentiment_MapsCorrectly()
    {
        var response = new[]
        {
            new { label = "positive", score = 0.92 },
            new { label = "negative", score = 0.03 },
            new { label = "neutral", score = 0.05 }
        };

        using var client = CreateMockClient(response);
        var service = CreateService(client);

        var result = await service.AnalyzeAsync("Apple stock surges", new StockSymbol("AAPL"));

        result.Score.Should().BeApproximately(0.89, 0.01); // 0.92 - 0.03
        result.Confidence.Should().BeApproximately(0.95, 0.01); // 1 - 0.05
        result.ModelVersion.Should().Be("finbert");
        result.KeyReasons.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzeAsync_NegativeSentiment_MapsToNegativeScore()
    {
        var response = new[]
        {
            new { label = "positive", score = 0.05 },
            new { label = "negative", score = 0.88 },
            new { label = "neutral", score = 0.07 }
        };

        using var client = CreateMockClient(response);
        var service = CreateService(client);

        var result = await service.AnalyzeAsync("Massive layoffs at company", new StockSymbol("META"));

        result.Score.Should().BeApproximately(-0.83, 0.01); // 0.05 - 0.88
        result.Confidence.Should().BeApproximately(0.93, 0.01); // 1 - 0.07
    }

    [Fact]
    public async Task AnalyzeAsync_NeutralSentiment_MapsToLowConfidence()
    {
        var response = new[]
        {
            new { label = "positive", score = 0.15 },
            new { label = "negative", score = 0.10 },
            new { label = "neutral", score = 0.75 }
        };

        using var client = CreateMockClient(response);
        var service = CreateService(client);

        var result = await service.AnalyzeAsync("Market opened today", new StockSymbol("SPY"));

        result.Score.Should().BeApproximately(0.05, 0.01); // 0.15 - 0.10
        result.Confidence.Should().BeApproximately(0.25, 0.01); // 1 - 0.75
    }

    [Fact]
    public async Task AnalyzeAsync_ApiError_Throws()
    {
        using var client = CreateMockClient(new { }, HttpStatusCode.InternalServerError);
        var service = CreateService(client);

        var act = () => service.AnalyzeAsync("Some text", new StockSymbol("AAPL"));

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private class MockHttpMessageHandler(string responseContent, HttpStatusCode statusCode)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
