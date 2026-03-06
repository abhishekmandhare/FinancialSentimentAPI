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
/// Uses prompt caching: the static system prompt (instructions + JSON schema)
/// is marked with cache_control so Anthropic caches it server-side for 5 minutes.
/// Cache reads cost ~10% of normal input token price, significantly reducing costs
/// when processing article bursts (multiple articles analyzed within the cache window).
/// </summary>
public class AnthropicSentimentService(
    HttpClient httpClient,
    IOptions<AnthropicOptions> options,
    ILogger<AnthropicSentimentService> logger)
    : IAiSentimentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Static system prompt — marked for caching. Must be byte-identical on every request
    // for the cache to hit. Do not interpolate dynamic values into this string.
    private const string SystemPrompt = """
        You are a financial analyst specializing in equity sentiment analysis.
        Focus on: earnings, revenue, guidance, competitive position, management commentary.
        Ignore general market sentiment unless directly tied to the specified stock symbol.

        Return ONLY valid JSON in this exact format, with no other text:
        {
          "score": <float between -1.0 (very negative) and 1.0 (very positive)>,
          "confidence": <float between 0.0 and 1.0>,
          "keyReasons": ["<reason 1>", "<reason 2>", "<reason 3>"]
        }
        """;

    public async Task<AiSentimentResult> AnalyzeAsync(
        string text,
        StockSymbol symbol,
        CancellationToken ct = default)
    {
        var opts = options.Value;

        var requestBody = new
        {
            model      = opts.Model,
            max_tokens = opts.MaxTokens,
            system = new[]
            {
                new
                {
                    type = "text",
                    text = SystemPrompt,
                    cache_control = new { type = "ephemeral" }
                }
            },
            messages = new[]
            {
                new
                {
                    role    = "user",
                    content = $"Analyze the sentiment of the following text specifically regarding {symbol.Value}:\n\n{text}"
                }
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
        request.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadFromJsonAsync<AnthropicResponse>(
            JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from Anthropic API.");

        var content = responseBody.Content?.FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("No text content in Anthropic response.");

        LogCacheUsage(responseBody.Usage);

        return ParseSentimentResult(content, opts.Model);
    }

    private void LogCacheUsage(UsageInfo? usage)
    {
        if (usage is null) return;

        if (usage.CacheReadInputTokens > 0)
            logger.LogDebug("Prompt cache hit: {CacheRead} cached tokens, {Input} input tokens",
                usage.CacheReadInputTokens, usage.InputTokens);
        else
            logger.LogDebug("Prompt cache miss (write): {CacheWrite} tokens cached, {Input} input tokens",
                usage.CacheCreationInputTokens, usage.InputTokens);
    }

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
        string? Model,
        UsageInfo? Usage);

    private record ContentBlock(string? Type, string? Text);

    private record UsageInfo(
        int InputTokens,
        int OutputTokens,
        int CacheCreationInputTokens,
        int CacheReadInputTokens);

    private record SentimentJson(
        double Score,
        double Confidence,
        List<string>? KeyReasons);
}
