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
    /// Fix common LLM JSON errors so we can parse almost-valid responses:
    ///   "value"']}    →  "value"]}          (stray single-quotes/backticks)
    ///   "value",]     →  "value"]           (trailing commas)
    ///   "value"]"}    →  "value"]}          (stray double-quote before })
    ///   {"a":1,"b":[  →  {"a":1,"b":[]}    (unclosed brackets)
    /// </summary>
    internal static string SanitizeLlmJson(string json)
    {
        // Pass 1: Remove stray quotes/backticks outside of JSON string values
        var sb = new StringBuilder(json.Length);
        var bracketDepth = 0;
        var braceDepth = 0;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            // Skip stray single-quotes and backticks outside strings
            if (c is '\'' or '`')
                continue;

            // Track bracket/brace depth for unclosed-bracket repair
            if (c == '{') braceDepth++;
            else if (c == '}') braceDepth--;
            else if (c == '[') bracketDepth++;
            else if (c == ']') bracketDepth--;

            // Skip stray double-quotes outside strings (e.g. "]"} → ]})
            if (c == '"')
            {
                // Look ahead: if this starts a valid JSON string, copy it through
                var isStringStart = false;
                for (var j = i + 1; j < json.Length; j++)
                {
                    if (json[j] == '\\' && j + 1 < json.Length)
                    {
                        j++; // skip escaped char
                        continue;
                    }
                    if (json[j] == '"')
                    {
                        isStringStart = true;
                        break;
                    }
                    // If we hit a structural char before closing quote,
                    // check if we're past reasonable string length
                    if (json[j] is '{' or '}' or '[' or ']' && j - i <= 2)
                    {
                        // Stray quote — e.g. "} or "] with no string content
                        isStringStart = false;
                        break;
                    }
                }

                if (!isStringStart)
                    continue; // drop the stray double-quote

                // Copy the full string (opening quote through closing quote)
                sb.Append(c);
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
                continue;
            }

            sb.Append(c);
        }

        // Pass 2: Remove trailing commas before ] or }
        var result = sb.ToString();
        result = System.Text.RegularExpressions.Regex.Replace(result, @",\s*([}\]])", "$1");

        // Pass 3: Close unclosed brackets/braces
        // Re-count since pass 1 & 2 may have changed things
        bracketDepth = 0;
        braceDepth = 0;
        foreach (var c in result)
        {
            if (c == '[') bracketDepth++;
            else if (c == ']') bracketDepth--;
            else if (c == '{') braceDepth++;
            else if (c == '}') braceDepth--;
        }

        for (var i = 0; i < bracketDepth; i++) result += ']';
        for (var i = 0; i < braceDepth; i++) result += '}';

        return result;
    }

    private record OllamaResponse(OllamaMessage? Message, int? EvalCount);
    private record OllamaMessage(string? Role, string? Content);
    private record SentimentJson(double Score, double Confidence, List<string>? KeyReasons);
}
