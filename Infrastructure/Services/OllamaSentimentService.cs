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
        You are a financial sentiment scoring API. You receive text and a stock symbol.
        You MUST respond with ONLY a JSON object — no explanations, no apologies, no commentary.

        If the text is irrelevant to the stock symbol, return a neutral score with low confidence.
        NEVER refuse. NEVER return plain text. ALWAYS return this exact JSON format:

        {"score": 0.0, "confidence": 0.0, "keyReasons": ["reason"]}

        Fields:
        - score: float from -1.0 (very negative) to 1.0 (very positive)
        - confidence: float from 0.0 (no relevance) to 1.0 (highly relevant)
        - keyReasons: 1-3 short strings explaining the score

        If the text has nothing to do with the stock, return:
        {"score": 0.0, "confidence": 0.1, "keyReasons": ["Text not relevant to the stock symbol"]}
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
            logger.LogWarning(ex, "Failed to parse Ollama response as JSON, returning neutral fallback. Raw content: {Content}", content);
            return new AiSentimentResult(
                Score:        0.0,
                Confidence:   0.1,
                KeyReasons:   ["AI response was not valid JSON — treated as neutral"],
                ModelVersion: model);
        }
    }

    private record OllamaResponse(OllamaMessage? Message, int? EvalCount);
    private record OllamaMessage(string? Role, string? Content);
    private record SentimentJson(double Score, double Confidence, List<string>? KeyReasons);
}
