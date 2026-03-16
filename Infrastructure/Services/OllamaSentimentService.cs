using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Services;
using Domain.ValueObjects;
using Infrastructure.Monitoring;
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

        var requestStart = Stopwatch.GetTimestamp();
        var response = await httpClient.SendAsync(request, ct);
        AppMetrics.OllamaRequestDuration.Record(Stopwatch.GetElapsedTime(requestStart).TotalSeconds);

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

        // LLMs occasionally produce almost-valid JSON with stray characters
        // (e.g. trailing ' or ` after a quoted string). Strip common garbage.
        json = SanitizeLlmJson(json);

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
            AppMetrics.OllamaParseFailures.Add(1);
            logger.LogWarning(ex, "Failed to parse Ollama response as JSON, returning neutral fallback. Raw content: {Content}", content);
            return new AiSentimentResult(
                Score:        0.0,
                Confidence:   0.1,
                KeyReasons:   ["AI response was not valid JSON — treated as neutral"],
                ModelVersion: model);
        }
    }

    /// <summary>
    /// Fix common LLM JSON errors: stray quotes/backticks between tokens,
    /// trailing commas before ] or }. Handles cases like:
    ///   "value"']}  →  "value"]}
    ///   "value",]   →  "value"]
    /// </summary>
    internal static string SanitizeLlmJson(string json)
    {
        // Remove stray single-quotes and backticks that appear between valid JSON tokens
        // e.g. "text"']} → "text"]}
        var sb = new StringBuilder(json.Length);
        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];
            if (c is '\'' or '`')
            {
                // Skip stray quotes that aren't inside a JSON string value
                // (we only reach here outside of string context since we
                //  walk character-by-character without entering strings)
                continue;
            }
            sb.Append(c);

            // Skip over JSON string contents so we don't strip quotes inside values
            if (c == '"')
            {
                for (var j = i + 1; j < json.Length; j++)
                {
                    sb.Append(json[j]);
                    if (json[j] == '\\' && j + 1 < json.Length)
                    {
                        j++;
                        sb.Append(json[j]);
                    }
                    else if (json[j] == '"')
                    {
                        i = j;
                        break;
                    }
                }
            }
        }

        // Remove trailing commas before ] or } — e.g. "value",] → "value"]
        var result = sb.ToString();
        result = System.Text.RegularExpressions.Regex.Replace(result, @",\s*([}\]])", "$1");

        return result;
    }

    private record OllamaResponse(OllamaMessage? Message, int? EvalCount);
    private record OllamaMessage(string? Role, string? Content);
    private record SentimentJson(double Score, double Confidence, List<string>? KeyReasons);
}
