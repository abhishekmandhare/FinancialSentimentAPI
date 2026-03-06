namespace API;

/// <summary>
/// Centralises rate limit policy names so Program.cs and controllers
/// reference the same string without duplication.
/// </summary>
public static class RateLimitPolicies
{
    public const string AnalyzeEndpoint = "analyze-endpoint";
}
