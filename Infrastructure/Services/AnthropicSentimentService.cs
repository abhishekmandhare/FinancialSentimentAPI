using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Services;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

/// <summary>
/// Calls the Anthropic Messages API to analyze financial sentiment.
/// Uses a structured prompt that asks Claude to return JSON only,
/// keeping the response deterministic and parseable.
///
/// The prompt is financial-specific: it instructs Claude to assess sentiment
/// for the given symbol, not general market sentiment.
/// </summary>
public class AnthropicSentimentService(
    HttpClient httpClient,
    IOptions<AnthropicOptions> options,
    ILogger<AnthropicSentimentService> logger)
    : IAiSentimentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiSentimentResult> AnalyzeAsync(
        string text,
        StockSymbol symbol,
        CancellationToken ct = default)
    {
        var opts = options.Value;

        var prompt = BuildPrompt(text, symbol);

        var requestBody = new
        {
            model      = opts.Model,
            max_tokens = opts.MaxTokens,
            messages   = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Add("x-api-key", opts.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadFromJsonAsync<AnthropicResponse>(
            JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from Anthropic API.");

        var content = responseBody.Content?.FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("No text content in Anthropic response.");

        return ParseSentimentResult(content, opts.Model);
    }

    private static string BuildPrompt(string text, StockSymbol symbol) => $$"""
        You are a financial analyst specializing in equity sentiment analysis.

        Analyze the sentiment of the following text specifically regarding {{symbol.Value}}.
        Focus on: earnings, revenue, guidance, competitive position, management commentary.
        Ignore general market sentiment unless directly tied to {{symbol.Value}}.

        Text to analyze:
        {{text}}

        Return ONLY valid JSON in this exact format, with no other text:
        {
          "score": <float between -1.0 (very negative) and 1.0 (very positive)>,
          "confidence": <float between 0.0 and 1.0>,
          "keyReasons": ["<reason 1>", "<reason 2>", "<reason 3>"]
        }
        """;

    private AiSentimentResult ParseSentimentResult(string content, string model)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<SentimentJson>(content, JsonOptions)
                ?? throw new InvalidOperationException("Failed to parse sentiment JSON.");

            var reasons = parsed.KeyReasons?.Take(3).ToList()
                ?? ["No reasons provided."];

            return new AiSentimentResult(
                Score:        Math.Clamp(parsed.Score, -1.0, 1.0),
                Confidence:   Math.Clamp(parsed.Confidence, 0.0, 1.0),
                KeyReasons:   reasons,
                ModelVersion: model);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse AI response as JSON. Raw content: {Content}", content);
            throw new InvalidOperationException("AI service returned an unparseable response.", ex);
        }
    }

    private record AnthropicResponse(
        List<ContentBlock>? Content,
        string? Model);

    private record ContentBlock(string? Type, string? Text);

    private record SentimentJson(
        double Score,
        double Confidence,
        List<string>? KeyReasons);
}
