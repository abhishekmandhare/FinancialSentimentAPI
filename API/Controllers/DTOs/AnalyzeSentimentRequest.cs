namespace API.Controllers.DTOs;

/// <summary>
/// HTTP request body for POST /api/sentiment/analyze.
/// Lives in the API layer — decoupled from the Application command.
/// The controller maps this to AnalyzeSentimentCommand.
/// </summary>
public record AnalyzeSentimentRequest(
    string Symbol,
    string Text,
    string? SourceUrl);
