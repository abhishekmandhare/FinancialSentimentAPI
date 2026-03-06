using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Services;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public class OllamaSentimentService(
    HttpClient httpClient,
    IOptions<OllamaOptions> options,
    ILogger<OllamaSentimentService> logger)
    : IAiSentimentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

        // Ollama uses OpenAI-compatible /v1/chat/completions
        var requestBody = new
        {
            model = opts.Model,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = $"Analyze the sentiment of the following text specifically regarding {symbol.Value}:\n\n{text}" }
            },
            stream = false,
            options = new { num_predict = opts.MaxTokens }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Ollama API error {StatusCode}: {Body}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var responseBody = await response.Content.ReadFromJsonAsync<OllamaResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from Ollama.");

        var content = responseBody.Message?.Content
            ?? throw new InvalidOperationException("No content in Ollama response.");

        logger.LogDebug("Ollama response ({Model}): {Tokens} tokens", opts.Model, responseBody.EvalCount);

        return ParseSentimentResult(content, opts.Model);
    }

    private AiSentimentResult ParseSentimentResult(string content, string model)
    {
        // Extract JSON from response — model may wrap it in markdown code blocks
        var json = content;
        var jsonStart = content.IndexOf('{');
        var jsonEnd = content.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
            json = content[jsonStart..(jsonEnd + 1)];

        try
        {
            var parsed = JsonSerializer.Deserialize<SentimentJson>(json, JsonOptions)
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
            logger.LogWarning(ex, "Failed to parse Ollama response as JSON. Raw content: {Content}", content);
            throw new InvalidOperationException("AI service returned an unparseable response.", ex);
        }
    }

    private record OllamaResponse(OllamaMessage? Message, int? EvalCount);
    private record OllamaMessage(string? Role, string? Content);
    private record SentimentJson(double Score, double Confidence, List<string>? KeyReasons);
}
