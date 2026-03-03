using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Services;

/// <summary>
/// Strongly-typed configuration for the Anthropic API.
/// ValidateDataAnnotations() + ValidateOnStart() in DI means bad config
/// fails at startup, not at the first API call at runtime.
/// ApiKey comes from User Secrets (dev) or environment variables (prod) — never appsettings.
/// </summary>
public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    [Required]
    public string BaseUrl { get; init; } = "https://api.anthropic.com/";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Required]
    public string Model { get; init; } = "claude-haiku-4-5-20251001";

    [Range(1, 4096)]
    public int MaxTokens { get; init; } = 512;
}
