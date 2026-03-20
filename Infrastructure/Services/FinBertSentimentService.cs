using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Services;
using Domain.ValueObjects;
using Infrastructure.Monitoring;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class FinBertSentimentService(
    HttpClient httpClient,
    ILogger<FinBertSentimentService> logger)
    : IAiSentimentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiSentimentResult> AnalyzeAsync(
        string text,
        StockSymbol symbol,
        CancellationToken ct = default)
    {
        var requestBody = new { text };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/predict")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        var requestStart = Stopwatch.GetTimestamp();

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            AppMetrics.FinBertErrors.Add(1);
            logger.LogError(ex, "FinBERT request failed for {Symbol}", symbol.Value);
            throw;
        }
        finally
        {
            AppMetrics.FinBertRequestDuration.Record(Stopwatch.GetElapsedTime(requestStart).TotalSeconds);
        }

        if (!response.IsSuccessStatusCode)
        {
            AppMetrics.FinBertErrors.Add(1);
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("FinBERT API error {StatusCode}: {Body}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var predictions = await response.Content.ReadFromJsonAsync<List<FinBertPrediction>>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Empty response from FinBERT.");

        return MapToResult(predictions, symbol);
    }

    private static AiSentimentResult MapToResult(List<FinBertPrediction> predictions, StockSymbol symbol)
    {
        var posScore = predictions.FirstOrDefault(p =>
            string.Equals(p.Label, "positive", StringComparison.OrdinalIgnoreCase))?.Score ?? 0.0;
        var negScore = predictions.FirstOrDefault(p =>
            string.Equals(p.Label, "negative", StringComparison.OrdinalIgnoreCase))?.Score ?? 0.0;
        var neuScore = predictions.FirstOrDefault(p =>
            string.Equals(p.Label, "neutral", StringComparison.OrdinalIgnoreCase))?.Score ?? 0.0;

        // Score: positive_prob - negative_prob → [-1, +1]
        var score = Math.Clamp(posScore - negScore, -1.0, 1.0);

        // Confidence: 1 - neutral_prob (high neutral = low confidence)
        var confidence = Math.Clamp(1.0 - neuScore, 0.0, 1.0);

        var winningLabel = predictions.OrderByDescending(p => p.Score).First();
        var reasons = new List<string>
        {
            $"FinBERT classified as {winningLabel.Label} ({winningLabel.Score:P0}) for {symbol.Value}",
            $"Sentiment breakdown: positive={posScore:P0}, negative={negScore:P0}, neutral={neuScore:P0}"
        };

        return new AiSentimentResult(
            Score: Math.Round(score, 4),
            Confidence: Math.Round(confidence, 4),
            KeyReasons: reasons,
            ModelVersion: "finbert");
    }

    private record FinBertPrediction(
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("score")] double Score);
}
